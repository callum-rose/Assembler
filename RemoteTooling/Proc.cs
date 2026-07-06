using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Assembler.RemoteTooling;

/// <summary>
/// Thin wrappers over <see cref="Process"/> for shelling out to git / gh / claude / validate-game.sh.
/// An <c>env</c> entry with a <c>null</c> value removes that variable from the child (the C# equivalent
/// of <c>env -u VAR</c>).
/// </summary>
public static class Proc
{
	public readonly record struct Result(int ExitCode, string StdOut, string StdErr);

	/// <summary>Run to completion, buffering stdout and stderr separately.</summary>
	public static Result Capture(
		string file,
		IReadOnlyList<string> args,
		string? workingDir = null,
		IReadOnlyDictionary<string, string?>? env = null)
	{
		using var p = new Process { StartInfo = Psi(file, args, workingDir, env, redirect: true) };
		var so = new StringBuilder();
		var se = new StringBuilder();
		p.OutputDataReceived += (_, e) => { if (e.Data is not null) { so.AppendLine(e.Data); } };
		p.ErrorDataReceived += (_, e) => { if (e.Data is not null) { se.AppendLine(e.Data); } };
		p.Start();
		p.BeginOutputReadLine();
		p.BeginErrorReadLine();
		p.WaitForExit();
		return new Result(p.ExitCode, so.ToString(), se.ToString());
	}

	/// <summary>
	/// Run to completion, delivering each stdout line to <paramref name="onStdout"/> and each stderr
	/// line to <paramref name="onStderr"/> as they arrive — kept separate so a caller can parse a
	/// structured stdout stream (e.g. claude's <c>stream-json</c>) live while still surfacing stderr.
	/// The child is killed if this process is interrupted, so it never orphans.
	/// </summary>
	public static int StreamLines(
		string file,
		IReadOnlyList<string> args,
		IReadOnlyDictionary<string, string?>? env,
		Action<string> onStdout,
		Action<string>? onStderr = null)
	{
		using var p = new Process { StartInfo = Psi(file, args, workingDir: null, env, redirect: true) };
		using var _ = KillOnExit(p);
		var gate = new object();
		p.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (gate) { onStdout(e.Data); } } };
		p.ErrorDataReceived += (_, e) => { if (e.Data is not null && onStderr is not null) { lock (gate) { onStderr(e.Data); } } };
		p.Start();
		p.BeginOutputReadLine();
		p.BeginErrorReadLine();
		p.WaitForExit();
		return p.ExitCode;
	}

	/// <summary>Run to completion, delivering each stdout/stderr line to <paramref name="onLine"/> as it arrives.</summary>
	public static int Stream(
		string file,
		IReadOnlyList<string> args,
		string? workingDir,
		IReadOnlyDictionary<string, string?>? env,
		Action<string> onLine)
	{
		using var p = new Process { StartInfo = Psi(file, args, workingDir, env, redirect: true) };
		using var _ = KillOnExit(p);
		var gate = new object();
		void Emit(string? line) { if (line is not null) { lock (gate) { onLine(line); } } }
		p.OutputDataReceived += (_, e) => Emit(e.Data);
		p.ErrorDataReceived += (_, e) => Emit(e.Data);
		p.Start();
		p.BeginOutputReadLine();
		p.BeginErrorReadLine();
		p.WaitForExit();
		return p.ExitCode;
	}

	/// <summary>Run to completion, letting the child inherit our stdout/stderr.</summary>
	public static int Run(
		string file,
		IReadOnlyList<string> args,
		string? workingDir = null,
		IReadOnlyDictionary<string, string?>? env = null)
	{
		using var p = new Process { StartInfo = Psi(file, args, workingDir, env, redirect: false) };
		p.Start();
		p.WaitForExit();
		return p.ExitCode;
	}

	/// <summary>Is <paramref name="name"/> an executable on PATH (or, if it contains a slash, an existing file)?</summary>
	public static bool Which(string name)
	{
		if (name.Contains('/'))
		{
			return File.Exists(name);
		}

		var path = Environment.GetEnvironmentVariable("PATH") ?? "";
		return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
			.Any(dir => File.Exists(Path.Combine(dir, name)));
	}

	// Ensure a long-running child (claude, or validate-game.sh booting Unity) is torn down if this
	// process is interrupted (Ctrl-C / SIGINT) or terminated (SIGTERM, e.g. `launchctl unload`), rather
	// than leaking as an orphan that keeps running. PosixSignalRegistration handlers run synchronously
	// before the default terminate action, so the child is killed before we exit — AppDomain.ProcessExit
	// doesn't reliably fire (or finish) under a blocking WaitForExit on SIGTERM. Returns a handle that
	// unregisters on Dispose.
	private static IDisposable KillOnExit(Process p)
	{
		void Kill()
		{
			try
			{
				if (!p.HasExited)
				{
					p.Kill(entireProcessTree: true);
				}
			}
			catch { /* already gone / racing exit */ }
		}

		var registrations = new List<IDisposable>();
		foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGQUIT })
		{
			try
			{
				// Leave ctx.Cancel = false so the default terminate action still proceeds after we kill.
				registrations.Add(PosixSignalRegistration.Create(signal, _ => Kill()));
			}
			catch (Exception ex) when (ex is PlatformNotSupportedException or ArgumentException)
			{
				// Signal not supported on this platform — skip it.
			}
		}

		// Backstop for a normal / unexpected-exception exit path.
		EventHandler onExit = (_, _) => Kill();
		AppDomain.CurrentDomain.ProcessExit += onExit;

		return new Deregister(() =>
		{
			AppDomain.CurrentDomain.ProcessExit -= onExit;
			foreach (var registration in registrations)
			{
				registration.Dispose();
			}
		});
	}

	private sealed class Deregister(Action dispose) : IDisposable
	{
		public void Dispose() => dispose();
	}

	private static ProcessStartInfo Psi(
		string file,
		IReadOnlyList<string> args,
		string? workingDir,
		IReadOnlyDictionary<string, string?>? env,
		bool redirect)
	{
		var psi = new ProcessStartInfo
		{
			FileName = file,
			UseShellExecute = false,
			RedirectStandardOutput = redirect,
			RedirectStandardError = redirect,
		};

		if (workingDir is not null)
		{
			psi.WorkingDirectory = workingDir;
		}

		foreach (var arg in args)
		{
			psi.ArgumentList.Add(arg);
		}

		if (env is not null)
		{
			foreach (var (key, value) in env)
			{
				if (value is null)
				{
					psi.Environment.Remove(key);
				}
				else
				{
					psi.Environment[key] = value;
				}
			}
		}

		return psi;
	}
}

using System.Reflection;

namespace Assembler.RemoteTooling;

/// <summary>
/// Exposes the compiled-in version so a deployed daemon can be asked which build is
/// actually running (<c>assembler-remote version</c>). The value flows from the
/// <c>&lt;Version&gt;</c> property in <c>Assembler.RemoteTooling.csproj</c>, so there is a
/// single source of truth that the build stamps into the assembly.
/// </summary>
public static class BuildInfo
{
	/// <summary>The daemon/CLI version, e.g. <c>1.0.0</c>.</summary>
	public static string Version =>
		typeof(BuildInfo).Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			// InformationalVersion can carry a build-metadata suffix (e.g. "1.0.0+<sha>"); keep just the version.
			.Split('+')[0]
		?? typeof(BuildInfo).Assembly.GetName().Version?.ToString()
		?? "unknown";
}

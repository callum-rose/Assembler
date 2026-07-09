#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assembler.AssetGeneration.EditorCommon
{
	/// <summary>
	/// The running-flag + status + <see cref="CancellationTokenSource"/> lifecycle every generation
	/// window repeated around its async "Generate" handler, including the identical
	/// cancelled/error catch and the dispose-in-finally. Drive a run with
	/// <c>_ = run.RunAsync(async ct =&gt; { … })</c> from the button handler; gate the UI on
	/// <see cref="IsRunning"/> and cancel with <see cref="Cancel"/>.
	/// </summary>
	public sealed class WindowRunState
	{
		private readonly Action _repaint;
		private CancellationTokenSource? _cts;

		public WindowRunState(Action repaint, string idle = "Idle.")
		{
			_repaint = repaint;
			Status = idle;
		}

		public bool IsRunning { get; private set; }

		public string Status { get; private set; }

		/// <summary>Set the status line and repaint.</summary>
		public void SetStatus(string message)
		{
			Status = message;
			_repaint();
		}

		/// <summary>Cancel the in-flight run, if any.</summary>
		public void Cancel() => _cts?.Cancel();

		/// <summary>
		/// Run <paramref name="body"/> under a fresh cancellation token, reporting a cancellation as
		/// "Cancelled." and any other exception as "Error: …" (also logged). Re-entrancy is ignored.
		/// The token is disposed and the UI repainted on completion.
		/// </summary>
		public async Task RunAsync(Func<CancellationToken, Task> body)
		{
			if (IsRunning)
			{
				return;
			}

			IsRunning = true;
			_cts = new CancellationTokenSource();
			var ct = _cts.Token;

			try
			{
				await body(ct);
			}
			catch (OperationCanceledException)
			{
				SetStatus("Cancelled.");
			}
			catch (Exception e)
			{
				SetStatus($"Error: {e.Message}");
				Debug.LogException(e);
			}
			finally
			{
				IsRunning = false;
				_cts?.Dispose();
				_cts = null;
				_repaint();
			}
		}
	}
}

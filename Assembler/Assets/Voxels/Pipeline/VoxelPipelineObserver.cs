using System;
using System.Collections.Generic;

namespace Assembler.Voxels.Pipeline
{
	public interface IVoxelPipelineObserver
	{
		void OnStageStarted(string stageName);
		void OnStageFinished(string stageName, TimeSpan elapsed);
		void OnStageFailed(string stageName, Exception ex);
		void OnLog(string message);
		void OnStreamDelta(string delta);
	}

	public sealed class NullVoxelPipelineObserver : IVoxelPipelineObserver
	{
		public static readonly NullVoxelPipelineObserver Instance = new();
		public void OnStageStarted(string stageName) { }
		public void OnStageFinished(string stageName, TimeSpan elapsed) { }
		public void OnStageFailed(string stageName, Exception ex) { }
		public void OnLog(string message) { }
		public void OnStreamDelta(string delta) { }
	}

	/// <summary>
	/// Fans observer events out to multiple subscribers. Useful when both a UI
	/// observer and a logging observer want the same pipeline events.
	/// </summary>
	public sealed class CompositeVoxelPipelineObserver : IVoxelPipelineObserver
	{
		private readonly IVoxelPipelineObserver[] _observers;
		public CompositeVoxelPipelineObserver(params IVoxelPipelineObserver[] observers) => _observers = observers;

		public void OnStageStarted(string stageName) { foreach (var o in _observers)
			{
				o.OnStageStarted(stageName);
			}
		}
		public void OnStageFinished(string stageName, TimeSpan elapsed) { foreach (var o in _observers)
			{
				o.OnStageFinished(stageName, elapsed);
			}
		}
		public void OnStageFailed(string stageName, Exception ex) { foreach (var o in _observers)
			{
				o.OnStageFailed(stageName, ex);
			}
		}
		public void OnLog(string message) { foreach (var o in _observers)
			{
				o.OnLog(message);
			}
		}
		public void OnStreamDelta(string delta) { foreach (var o in _observers)
			{
				o.OnStreamDelta(delta);
			}
		}
	}
}

using System.Collections.Generic;
using Assembler.Parsing.Info;

namespace Assembler.Building.Debug
{
	/// <summary>
	/// Fixed-capacity ring buffer of recent trigger firings, fed by <c>GameBehaviour.Fired</c>. Oldest
	/// entries are overwritten once full so the log stays bounded regardless of how long a game runs.
	/// Pure data with no Unity dependency, so it can be unit-tested directly.
	/// </summary>
	public sealed class TriggerLog
	{
		public readonly struct Entry
		{
			/// <summary>Game frame on which the firing was recorded (from <c>IGameClock.FrameCount</c>).</summary>
			public readonly int Frame;

			/// <summary>The behaviour that fired, or null when it could not be mapped to a known descriptor.</summary>
			public readonly BehaviourDescriptor? Descriptor;

			/// <summary>Human label for the firing source when no descriptor is available (e.g. type name).</summary>
			public readonly string Source;

			/// <summary>Keys carried by the trigger context at fire time.</summary>
			public readonly IReadOnlyList<string> Keys;

			public Entry(int frame, BehaviourDescriptor? descriptor, string source, IReadOnlyList<string> keys)
			{
				Frame = frame;
				Descriptor = descriptor;
				Source = source;
				Keys = keys;
			}
		}

		private readonly Entry[] _buffer;
		private int _start;
		private int _count;

		public TriggerLog(int capacity)
		{
			_buffer = new Entry[capacity < 1 ? 1 : capacity];
		}

		public int Capacity => _buffer.Length;

		public int Count => _count;

		/// <summary>Appends an entry, overwriting the oldest once the buffer is full.</summary>
		public void Append(Entry entry)
		{
			var index = (_start + _count) % _buffer.Length;
			_buffer[index] = entry;

			if (_count < _buffer.Length)
			{
				_count++;
			}
			else
			{
				_start = (_start + 1) % _buffer.Length;
			}
		}

		public void Clear()
		{
			_start = 0;
			_count = 0;
		}

		/// <summary>Entries from oldest to newest.</summary>
		public IEnumerable<Entry> Entries()
		{
			for (var i = 0; i < _count; i++)
			{
				yield return _buffer[(_start + i) % _buffer.Length];
			}
		}
	}
}

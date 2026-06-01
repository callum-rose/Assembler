using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing.Info;

namespace Assembler.Building.Debug
{
	/// <summary>
	/// Bounded, de-duplicating record of trigger firings fed by <c>GameBehaviour.Fired</c>. Repeated
	/// firings of the same behaviour are coalesced into one entry that tracks a hit count and the last
	/// frame, so an every-frame trigger (e.g. a key-hold) stays a single row instead of swamping the log.
	/// Pure data with no Unity dependency, so it can be unit-tested directly.
	/// </summary>
	public sealed class TriggerLog
	{
		/// <summary>An aggregated firing: one per distinct behaviour, with a running count.</summary>
		public sealed class Entry
		{
			internal Entry(BehaviourDescriptor? descriptor, string source, IReadOnlyList<string> keys, int frame)
			{
				Descriptor = descriptor;
				Source = source;
				Keys = keys;
				Count = 1;
				FirstFrame = frame;
				LastFrame = frame;
			}

			/// <summary>The behaviour that fired, or null when it could not be mapped to a known descriptor.</summary>
			public BehaviourDescriptor? Descriptor { get; }

			/// <summary>Human label for the firing source when no descriptor is available (e.g. type name).</summary>
			public string Source { get; }

			/// <summary>Keys carried by the most recent firing's trigger context.</summary>
			public IReadOnlyList<string> Keys { get; private set; }

			/// <summary>How many times this behaviour has fired since the log was last cleared.</summary>
			public int Count { get; private set; }

			/// <summary>Game frame of the first firing.</summary>
			public int FirstFrame { get; }

			/// <summary>Game frame of the most recent firing.</summary>
			public int LastFrame { get; private set; }

			internal void Hit(int frame, IReadOnlyList<string> keys)
			{
				Count++;
				LastFrame = frame;
				Keys = keys;
			}
		}

		private readonly Dictionary<string, Entry> _entries = new();
		private readonly int _capacity;

		public TriggerLog(int capacity)
		{
			_capacity = capacity < 1 ? 1 : capacity;
		}

		/// <summary>Maximum number of distinct behaviours tracked before the least-recently-fired is dropped.</summary>
		public int Capacity => _capacity;

		/// <summary>Number of distinct behaviours currently tracked.</summary>
		public int Count => _entries.Count;

		/// <summary>Records a firing, coalescing into the existing entry for the same behaviour if present.</summary>
		public void Record(int frame, BehaviourDescriptor? descriptor, string source, IReadOnlyList<string> keys)
		{
			var key = descriptor != null ? descriptor.EntityId + "/" + descriptor.BehaviourId : source;

			if (_entries.TryGetValue(key, out var entry))
			{
				entry.Hit(frame, keys);
				return;
			}

			if (_entries.Count >= _capacity)
			{
				EvictLeastRecent();
			}

			_entries[key] = new Entry(descriptor, source, keys, frame);
		}

		public void Clear() => _entries.Clear();

		/// <summary>Tracked behaviours, most recently fired first.</summary>
		public IEnumerable<Entry> Entries() =>
			_entries.Values.OrderByDescending(e => e.LastFrame).ThenBy(e => e.Source);

		private void EvictLeastRecent()
		{
			string? oldestKey = null;
			var oldestFrame = int.MaxValue;

			foreach (var kv in _entries)
			{
				if (kv.Value.LastFrame < oldestFrame)
				{
					oldestFrame = kv.Value.LastFrame;
					oldestKey = kv.Key;
				}
			}

			if (oldestKey != null)
			{
				_entries.Remove(oldestKey);
			}
		}
	}
}

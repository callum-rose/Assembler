#if DEBUG_CONSOLE
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Building.Debug
{
	/// <summary>
	/// Framework-level, always-available debug overlay. Attached to the game root by <see cref="Builder"/>
	/// and handed the live registries/clock so it can introspect any running game generically — no
	/// per-game wiring. Toggle with the backtick key. Entirely compiled out unless DEBUG_CONSOLE is
	/// defined, so release builds carry none of it.
	/// </summary>
	/// <remarks>
	/// Inspect: live entities, their behaviours, and global/entity variable values; a bounded trigger
	/// firing log fed by <see cref="GameBehaviour.Fired"/>.
	/// Control: pause/resume, single-step, time-scale, force game-over, set a variable, manually fire a
	/// behaviour. Variable writes go through the same <see cref="IValueProvider"/> the behaviours use.
	/// </remarks>
	public sealed class DebugConsole : MonoBehaviour
	{
		private BehaviourRegistry _registry = null!;
		private IGameClock _clock = null!;
		private VariableRegistry _globals = null!;
		private GameController _controller = null!;

		private readonly TriggerLog _log = new(256);
		private readonly Dictionary<string, string> _edits = new();
		private readonly HashSet<BehaviourDescriptor> _expanded = new();

		// Whole overlay is rendered through a 2x GUI matrix so it is easier to read; panel rects are
		// therefore laid out in the halved "logical" coordinate space (Screen size / UiScale).
		private const float UiScale = 2f;

		private bool _open;
		private bool _logging;
		private string _logFilter = "";
		private Vector2 _scroll;
		private Vector2 _logScroll;
		private GUIStyle? _header;
		private GUIStyle? _rowButton;

		// Behaviour highlighted by a listener "ping", and the realtime moment the highlight expires.
		private BehaviourDescriptor? _pingDescriptor;
		private float _pingUntil;

		// Expand/ping change the control count, so they are queued from click handlers and applied on the
		// next Layout pass — mutating layout mid-event triggers IMGUI "mismatched LayoutGroup" warnings.
		private BehaviourDescriptor? _toggleRequest;
		private BehaviourDescriptor? _pingRequest;

		public void Initialise(BehaviourRegistry registry, IGameClock clock, VariableRegistry globals,
			GameController controller)
		{
			_registry = registry;
			_clock = clock;
			_globals = globals;
			_controller = controller;
		}

		private void OnEnable() => GameBehaviour.Fired += OnFired;

		private void OnDisable() => GameBehaviour.Fired -= OnFired;

		private void OnFired(GameBehaviour source, TriggerContext ctx)
		{
			if (!_logging || _registry == null || source == null)
			{
				return;
			}

			BehaviourDescriptor? descriptor = null;
			foreach (var kv in _registry.All)
			{
				if (ReferenceEquals(kv.Value, source))
				{
					descriptor = kv.Key;
					break;
				}
			}

			// The implicit game-over controller fires an every-frame tick; that noise swamps the log.
			if (descriptor?.EntityId == GameOverController.EntityId)
			{
				return;
			}

			_log.Record(_clock.FrameCount, descriptor, source.GetType().Name, ctx.Keys.ToArray());
		}

		private void OnGUI()
		{
			var e = Event.current;
			if (e.type == EventType.KeyDown && e.keyCode == KeyCode.BackQuote)
			{
				_open = !_open;
				e.Use();
				return;
			}

			if (!_open || _registry == null)
			{
				return;
			}

			_header ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
			_rowButton ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };

			// Apply queued expand/ping requests now, before laying out, so the control count stays
			// consistent across this frame's Layout and Repaint passes.
			if (e.type == EventType.Layout)
			{
				ApplyPendingRequests();
			}

			// Scale the whole overlay up; Unity transforms input by the inverse, so clicks still land.
			var prevMatrix = GUI.matrix;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(UiScale, UiScale, 1f));

			var logicalWidth = Screen.width / UiScale;
			var logicalHeight = Screen.height / UiScale;
			var panelHeight = logicalHeight - 20f;

			var rightWidth = Mathf.Min(360f, logicalWidth * 0.45f);
			var leftWidth = Mathf.Min(480f, logicalWidth - rightWidth - 30f);

			DrawMainPanel(new Rect(10f, 10f, leftWidth, panelHeight));
			DrawLogPanel(new Rect(logicalWidth - rightWidth - 10f, 10f, rightWidth, panelHeight));

			GUI.matrix = prevMatrix;
		}

		private void DrawMainPanel(Rect rect)
		{
			GUILayout.BeginArea(rect, GUI.skin.box);
			GUILayout.Label("Debug Console  (` to close)", _header);

			_scroll = GUILayout.BeginScrollView(_scroll);
			DrawClockControls();
			DrawGlobals();
			DrawEntities();
			GUILayout.EndScrollView();

			GUILayout.EndArea();
		}

		private void DrawLogPanel(Rect rect)
		{
			GUILayout.BeginArea(rect, GUI.skin.box);

			GUILayout.BeginHorizontal();
			GUILayout.Label("Trigger log", _header, GUILayout.ExpandWidth(true));
			_logging = GUILayout.Toggle(_logging, "record", GUILayout.Width(70f));
			if (GUILayout.Button("Clear", GUILayout.Width(50f)))
			{
				_log.Clear();
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("filter", GUILayout.Width(40f));
			_logFilter = GUILayout.TextField(_logFilter, GUILayout.ExpandWidth(true));
			GUILayout.EndHorizontal();

			_logScroll = GUILayout.BeginScrollView(_logScroll);
			DrawLogEntries();
			GUILayout.EndScrollView();

			GUILayout.EndArea();
		}

		private void DrawClockControls()
		{
			GUILayout.Label("Clock", _header);

			GUILayout.BeginHorizontal();
			if (GUILayout.Button(_clock.IsPaused ? "Resume" : "Pause"))
			{
				if (_clock.IsPaused)
				{
					_clock.Resume();
				}
				else
				{
					_clock.Pause();
				}
			}

			if (GUILayout.Button("Step"))
			{
				_clock.Pause(); // stepping only advances while paused
				_clock.Step();
			}

			GUILayout.Label($"frame {_clock.FrameCount}  t={_clock.Time:0.00}", GUILayout.ExpandWidth(true));
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label($"TimeScale {_clock.TimeScale:0.00}", GUILayout.Width(110f));
			foreach (var scale in new[] { 0.25f, 0.5f, 1f, 2f, 4f })
			{
				if (GUILayout.Button(scale.ToString("0.##", CultureInfo.InvariantCulture)))
				{
					_clock.TimeScale = scale;
				}
			}

			GUILayout.EndHorizontal();
			_clock.TimeScale = GUILayout.HorizontalSlider(_clock.TimeScale, 0f, 4f);

			if (GUILayout.Button("Force Game Over"))
			{
				ForceGameOver();
			}

			GUILayout.Space(6f);
		}

		private void ForceGameOver()
		{
			var descriptor = new BehaviourDescriptor(GameOverController.EntityId, GameOverController.EndBehaviourId);
			if (_registry.All.TryGetValue(descriptor, out var end) && end)
			{
				// Run the real end-game path so any downstream !gameover listeners fire too.
				end.Execute(TriggerContext.Empty);
			}
			else if (_controller)
			{
				_controller.EndGame();
			}
		}

		private void DrawGlobals()
		{
			GUILayout.Label("Global variables", _header);
			var any = false;
			foreach (var kv in _globals.Globals.OrderBy(kv => kv.Key))
			{
				any = true;
				DrawVariableRow("global:" + kv.Key, kv.Key, kv.Value);
			}

			if (!any)
			{
				GUILayout.Label("  (none)");
			}

			GUILayout.Space(6f);
		}

		private void DrawEntities()
		{
			GUILayout.Label("Entities", _header);

			// Re-read the live registry each frame so spawned/destroyed entities are reflected.
			var behavioursByEntity = new SortedDictionary<string, List<KeyValuePair<BehaviourDescriptor, GameBehaviour>>>();
			var entityComponents = new Dictionary<string, GameEntity>();
			var descriptorByBehaviour = new Dictionary<GameBehaviour, BehaviourDescriptor>();

			foreach (var kv in _registry.All)
			{
				if (!kv.Value)
				{
					continue;
				}

				descriptorByBehaviour[kv.Value] = kv.Key;

				var entityId = kv.Key.EntityId;
				if (!behavioursByEntity.TryGetValue(entityId, out var list))
				{
					behavioursByEntity[entityId] = list = new List<KeyValuePair<BehaviourDescriptor, GameBehaviour>>();
				}

				list.Add(kv);

				if (!entityComponents.ContainsKey(entityId))
				{
					var entity = kv.Value.GetComponent<GameEntity>();
					if (entity)
					{
						entityComponents[entityId] = entity;
					}
				}
			}

			foreach (var (entityId, behaviours) in behavioursByEntity)
			{
				GUILayout.Label(entityId, _header);

				if (entityComponents.TryGetValue(entityId, out var entity) && entity.VariableScope != null)
				{
					foreach (var variable in entity.VariableScope.All.OrderBy(v => v.Key))
					{
						DrawVariableRow($"{entityId}:{variable.Key}", variable.Key, variable.Value);
					}
				}

				foreach (var kv in behaviours.OrderBy(b => b.Key.BehaviourId))
				{
					DrawBehaviourRow(kv.Key, kv.Value, descriptorByBehaviour);
				}

				GUILayout.Space(4f);
			}
		}

		private void DrawBehaviourRow(BehaviourDescriptor descriptor, GameBehaviour behaviour,
			IReadOnlyDictionary<GameBehaviour, BehaviourDescriptor> descriptorByBehaviour)
		{
			var expanded = _expanded.Contains(descriptor);
			var pinged = descriptor.Equals(_pingDescriptor) && UnityEngine.Time.realtimeSinceStartup < _pingUntil;

			GUILayout.BeginHorizontal();
			var prevColor = GUI.backgroundColor;
			if (pinged)
			{
				GUI.backgroundColor = Color.yellow;
			}

			if (GUILayout.Button($"{(expanded ? "▼" : "▶")} {descriptor.BehaviourId} ({behaviour.GetType().Name})",
				_rowButton, GUILayout.ExpandWidth(true)))
			{
				Toggle(descriptor);
			}

			GUI.backgroundColor = prevColor;

			if (GUILayout.Button("Fire", GUILayout.Width(50f)))
			{
				behaviour.Execute(TriggerContext.Empty);
			}

			GUILayout.EndHorizontal();

			if (expanded)
			{
				DrawBehaviourDetails(behaviour, descriptorByBehaviour);
			}
		}

		private void DrawBehaviourDetails(GameBehaviour behaviour,
			IReadOnlyDictionary<GameBehaviour, BehaviourDescriptor> descriptorByBehaviour)
		{
			var tags = behaviour.Tags;
			GUILayout.Label(tags.Length > 0 ? "      tags: " + string.Join(", ", tags) : "      tags: (none)");

			GUILayout.Label("      notifies:");
			var any = false;
			foreach (var target in ResolveListenerTargets(behaviour))
			{
				any = true;
				var label = descriptorByBehaviour.TryGetValue(target, out var targetDescriptor)
					? $"{targetDescriptor.EntityId}/{targetDescriptor.BehaviourId}"
					: target.GetType().Name;

				GUILayout.BeginHorizontal();
				GUILayout.Space(24f);
				if (GUILayout.Button("→ " + label, _rowButton, GUILayout.ExpandWidth(true)))
				{
					Ping(target, descriptorByBehaviour);
				}

				GUILayout.EndHorizontal();
			}

			if (!any)
			{
				GUILayout.Label("        (none)");
			}
		}

		/// <summary>Resolved target behaviours of every listener wired to <paramref name="behaviour"/>, de-duplicated.</summary>
		private static IEnumerable<GameBehaviour> ResolveListenerTargets(GameBehaviour behaviour)
		{
			var seen = new HashSet<GameBehaviour>();
			foreach (var listener in behaviour.DebugListeners)
			{
				List<GameBehaviour> targets;
				try
				{
					// Tagged listeners resolve against an empty context and may throw or come up empty.
					targets = listener.DebugTargets().Where(t => t).ToList();
				}
				catch
				{
					continue;
				}

				foreach (var target in targets)
				{
					if (seen.Add(target))
					{
						yield return target;
					}
				}
			}
		}

		private void ApplyPendingRequests()
		{
			if (_toggleRequest is { } toggle)
			{
				if (!_expanded.Remove(toggle))
				{
					_expanded.Add(toggle);
				}

				_toggleRequest = null;
			}

			if (_pingRequest is { } ping)
			{
				_pingDescriptor = ping;
				_pingUntil = UnityEngine.Time.realtimeSinceStartup + 2.5f;
				_expanded.Add(ping); // expand the target so its details are visible when highlighted
				_pingRequest = null;
			}
		}

		private void Toggle(BehaviourDescriptor descriptor) => _toggleRequest = descriptor;

		private void Ping(GameBehaviour target, IReadOnlyDictionary<GameBehaviour, BehaviourDescriptor> descriptorByBehaviour)
		{
			if (descriptorByBehaviour.TryGetValue(target, out var descriptor))
			{
				_pingRequest = descriptor;
			}
		}

		private void DrawVariableRow(string editKey, string label, IValueProvider provider)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label($"  {label} = {DisplayValue(provider)}", GUILayout.ExpandWidth(true));

			if (IsEditable(provider))
			{
				if (!_edits.TryGetValue(editKey, out var text))
				{
					_edits[editKey] = text = DisplayValue(provider);
				}

				_edits[editKey] = GUILayout.TextField(text, GUILayout.Width(90f));

				if (GUILayout.Button("Set", GUILayout.Width(50f)))
				{
					TrySetVariable(provider, _edits[editKey]);
				}
			}

			GUILayout.EndHorizontal();
		}

		private void DrawLogEntries()
		{
			// Coalesced, most recently fired first.
			foreach (var entry in _log.Entries())
			{
				var descriptor = entry.Descriptor;
				var source = descriptor != null
					? $"{descriptor.EntityId}/{descriptor.BehaviourId}"
					: entry.Source;

				if (_logFilter.Length > 0 && source.IndexOf(_logFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}

				var keys = entry.Keys.Count == 0 ? "" : " [" + string.Join(", ", entry.Keys) + "]";
				var count = entry.Count > 1 ? $" x{entry.Count}" : "";
				GUILayout.Label($"  f{entry.LastFrame}{count}  {source}{keys}");
			}
		}

		private static string DisplayValue(IValueProvider provider)
		{
			try
			{
				return provider.Get(TriggerContext.Empty)?.ToString() ?? "null";
			}
			catch
			{
				return "<n/a>";
			}
		}

		private static bool IsEditable(IValueProvider provider)
		{
			var type = provider.GetType();
			if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ValueProvider<>))
			{
				return false;
			}

			var element = type.GetGenericArguments()[0];
			return element == typeof(int) || element == typeof(float)
				|| element == typeof(bool) || element == typeof(string);
		}

		private static void TrySetVariable(IValueProvider provider, string raw)
		{
			var typed = provider.GetType().GetInterfaces().FirstOrDefault(i =>
				i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValueProvider<>));
			if (typed == null)
			{
				return;
			}

			var element = typed.GetGenericArguments()[0];
			if (!TryParse(raw, element, out var value))
			{
				return;
			}

			typed.GetMethod(nameof(IValueProvider<object>.Set))!.Invoke(provider, new[] { value });
		}

		private static bool TryParse(string raw, System.Type type, out object? value)
		{
			if (type == typeof(int) && int.TryParse(raw, out var i))
			{
				value = i;
				return true;
			}

			if (type == typeof(float)
				&& float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
			{
				value = f;
				return true;
			}

			if (type == typeof(bool) && bool.TryParse(raw, out var b))
			{
				value = b;
				return true;
			}

			if (type == typeof(string))
			{
				value = raw;
				return true;
			}

			value = null;
			return false;
		}
	}
}
#endif

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

		private bool _open;
		private bool _logging;
		private Vector2 _scroll;
		private GUIStyle? _header;

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

			_log.Append(new TriggerLog.Entry(_clock.FrameCount, descriptor, source.GetType().Name,
				ctx.Keys.ToArray()));
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

			var width = Mathf.Min(480f, Screen.width - 20f);
			GUILayout.BeginArea(new Rect(10f, 10f, width, Screen.height - 20f), GUI.skin.box);
			GUILayout.Label("Debug Console  (` to close)", _header);

			_scroll = GUILayout.BeginScrollView(_scroll);
			DrawClockControls();
			DrawGlobals();
			DrawEntities();
			DrawTriggerLog();
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

			foreach (var kv in _registry.All)
			{
				if (!kv.Value)
				{
					continue;
				}

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
					GUILayout.BeginHorizontal();
					GUILayout.Label($"  • {kv.Key.BehaviourId} ({kv.Value.GetType().Name})",
						GUILayout.ExpandWidth(true));
					if (GUILayout.Button("Fire", GUILayout.Width(50f)))
					{
						kv.Value.Execute(TriggerContext.Empty);
					}

					GUILayout.EndHorizontal();
				}

				GUILayout.Space(4f);
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

		private void DrawTriggerLog()
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label("Trigger log", _header, GUILayout.ExpandWidth(true));
			_logging = GUILayout.Toggle(_logging, "record", GUILayout.Width(70f));
			if (GUILayout.Button("Clear", GUILayout.Width(50f)))
			{
				_log.Clear();
			}

			GUILayout.EndHorizontal();

			// Newest first.
			foreach (var entry in _log.Entries().Reverse())
			{
				var descriptor = entry.Descriptor;
				var source = descriptor != null
					? $"{descriptor.EntityId}/{descriptor.BehaviourId}"
					: entry.Source;
				var keys = entry.Keys.Count == 0 ? "" : " [" + string.Join(", ", entry.Keys) + "]";
				GUILayout.Label($"  f{entry.Frame}  {source}{keys}");
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

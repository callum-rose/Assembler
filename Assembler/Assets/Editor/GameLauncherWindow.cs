using System.Collections.Generic;
using System.IO;
using Assembler.Building;
using Assembler.Deserialisation;
using Assembler.Generation;
using Assembler.Input;
using UnityEditor;
using UnityEngine;

namespace Editor
{
	public class GameLauncherWindow : EditorWindow
	{
		private const string DescriptorsFolder = "Assets/ExampleGameDescriptors";
		private const string PendingLaunchKey = "GameLauncherWindow.PendingYamlPath";
		private const string PendingPlatformKey = "GameLauncherWindow.PendingPlatform";

		// Index 0 is "Auto" (let PlatformSelector decide); the rest map onto InputPlatform values + 1, so the
		// editor can simulate a platform without deploying to a device.
		private static readonly string[] PlatformOptions = { "Auto", "Desktop", "Gamepad", "Mobile", "Console" };

		private List<GameEntry> _entries = new();
		private int _selectedIndex = -1;
		private int _platformIndex;
		private Vector2 _listScroll;
		private Vector2 _descriptionScroll;

		[MenuItem("Assembler/Game Launcher")]
		public static void Open()
		{
			var window = GetWindow<GameLauncherWindow>("Game Launcher");
			window.minSize = new Vector2(600, 300);
		}

		private void OnEnable()
		{
			RefreshEntries();
		}

		private void OnFocus()
		{
			RefreshEntries();
		}

		private void RefreshEntries()
		{
			_entries = new List<GameEntry>();

			var parser = new GameFileParser();
			AddEntriesFrom(DescriptorsFolder, "Example", parser);
			AddEntriesFrom(DescriptorFileWriter.FolderPath, "Generated", parser);

			_entries.Sort((a, b) => string.Compare(a.Title, b.Title, System.StringComparison.OrdinalIgnoreCase));

			if (_selectedIndex >= _entries.Count) _selectedIndex = _entries.Count - 1;
			if (_selectedIndex < 0 && _entries.Count > 0) _selectedIndex = 0;
		}

		private void AddEntriesFrom(string folder, string source, GameFileParser parser)
		{
			if (!Directory.Exists(folder))
			{
				return;
			}

			foreach (var path in Directory.GetFiles(folder, "*.yaml"))
			{
				var normalised = path.Replace('\\', '/');
				var fileName = Path.GetFileNameWithoutExtension(normalised);
				string title = fileName;
				string description = "";

				try
				{
					var yaml = File.ReadAllText(normalised);
					var dto = parser.Parse(yaml);
					if (dto?.Game != null)
					{
						if (!string.IsNullOrWhiteSpace(dto.Game.Title))
						{
							title = dto.Game.Title!;
						}

						if (!string.IsNullOrWhiteSpace(dto.Game.Description))
						{
							description = dto.Game.Description!;
						}
					}
				}
				catch
				{
					description = "(Failed to parse descriptor.)";
				}

				_entries.Add(new GameEntry(normalised, fileName, title, description, source));
			}
		}

		private void OnGUI()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				DrawList();
				DrawDetails();
			}
		}

		private void DrawList()
		{
			using (new EditorGUILayout.VerticalScope(GUILayout.Width(220)))
			{
				using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
				{
					GUILayout.Label("Games", EditorStyles.boldLabel);
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
					{
						RefreshEntries();
					}
				}

				_listScroll = EditorGUILayout.BeginScrollView(_listScroll);
				for (int i = 0; i < _entries.Count; i++)
				{
					var entry = _entries[i];
					var style = i == _selectedIndex ? SelectedButtonStyle : EntryButtonStyle;
					if (GUILayout.Button(entry.Title, style))
					{
						_selectedIndex = i;
						_descriptionScroll = Vector2.zero;
					}
				}
				EditorGUILayout.EndScrollView();
			}
		}

		private void DrawDetails()
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
				{
					EditorGUILayout.LabelField("Select a game on the left.");
					return;
				}

				var entry = _entries[_selectedIndex];

				EditorGUILayout.LabelField(entry.Title, TitleStyle);
				EditorGUILayout.LabelField($"{entry.FileName}.yaml  ·  {entry.Source}", EditorStyles.miniLabel);

				EditorGUILayout.Space();

				_descriptionScroll = EditorGUILayout.BeginScrollView(_descriptionScroll);
				EditorGUILayout.LabelField(
					string.IsNullOrEmpty(entry.Description) ? "(No description)" : entry.Description,
					DescriptionStyle);
				EditorGUILayout.EndScrollView();

				EditorGUILayout.Space();

				_platformIndex = EditorGUILayout.Popup("Platform", _platformIndex, PlatformOptions);

				EditorGUILayout.Space();

				using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying))
				{
					if (GUILayout.Button("Play", GUILayout.Height(32)))
					{
						LaunchGame(entry.Path, _platformIndex);
					}
				}
			}
		}

		private static void LaunchGame(string yamlPath, int platformIndex)
		{
			if (EditorApplication.isPlaying)
			{
				Builder.Build(yamlPath, PlatformOverride(platformIndex));
			}
			else
			{
				SessionState.SetString(PendingLaunchKey, yamlPath);
				SessionState.SetInt(PendingPlatformKey, platformIndex);
				EditorApplication.EnterPlaymode();
			}
		}

		// Index 0 ("Auto") leaves selection to PlatformSelector; otherwise map onto the InputPlatform value.
		private static InputPlatform? PlatformOverride(int platformIndex) =>
			platformIndex <= 0 ? null : (InputPlatform)(platformIndex - 1);

		[InitializeOnLoadMethod]
		private static void Register()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange change)
		{
			if (change != PlayModeStateChange.EnteredPlayMode)
			{
				return;
			}

			var pending = SessionState.GetString(PendingLaunchKey, "");
			if (string.IsNullOrEmpty(pending))
			{
				return;
			}

			var platformIndex = SessionState.GetInt(PendingPlatformKey, 0);

			SessionState.EraseString(PendingLaunchKey);
			SessionState.EraseInt(PendingPlatformKey);

			try
			{
				Builder.Build(pending, PlatformOverride(platformIndex));
			}
			catch (System.Exception e)
			{
				Debug.LogError($"Failed to launch game '{pending}': {e}");
			}
		}

		private static GUIStyle? _entryButtonStyle;
		private static GUIStyle EntryButtonStyle => _entryButtonStyle ??= new GUIStyle(EditorStyles.miniButton)
		{
			alignment = TextAnchor.MiddleLeft,
			fixedHeight = 24,
		};

		private static GUIStyle? _selectedButtonStyle;
		private static GUIStyle SelectedButtonStyle
		{
			get
			{
				if (_selectedButtonStyle != null)
				{
					return _selectedButtonStyle;
				}

				_selectedButtonStyle = new GUIStyle(EntryButtonStyle)
				{
					fontStyle = FontStyle.Bold,
				};
				_selectedButtonStyle.normal.textColor = Color.white;
				_selectedButtonStyle.normal.background = MakeColorTexture(new Color(0.24f, 0.48f, 0.90f));
				return _selectedButtonStyle;
			}
		}

		private static GUIStyle? _titleStyle;
		private static GUIStyle TitleStyle => _titleStyle ??= new GUIStyle(EditorStyles.largeLabel)
		{
			fontStyle = FontStyle.Bold,
			fontSize = 16,
		};

		private static GUIStyle? _descriptionStyle;
		private static GUIStyle DescriptionStyle => _descriptionStyle ??= new GUIStyle(EditorStyles.label)
		{
			wordWrap = true,
		};

		private static Texture2D MakeColorTexture(Color color)
		{
			var tex = new Texture2D(1, 1);
			tex.SetPixel(0, 0, color);
			tex.Apply();
			tex.hideFlags = HideFlags.HideAndDontSave;
			return tex;
		}

		private record GameEntry(string Path, string FileName, string Title, string Description, string Source);
	}
}

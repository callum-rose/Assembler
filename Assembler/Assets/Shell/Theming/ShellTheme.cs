using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Assembler.Shell.Theming
{
	/// <summary>
	/// The shell's look in one asset: the colour roles, the typographic scale, the motion timings and the layout
	/// measurements. Nothing in the shell hard-codes a colour, a font size or a tween duration — it names a role,
	/// a style or a spec here, so a second asset re-skins the app (dark mode) without touching a prefab.
	/// </summary>
	[CreateAssetMenu(fileName = "ShellTheme", menuName = "Assembler/Shell/Theme")]
	public sealed class ShellTheme : ScriptableObject
	{
		/// <summary>
		/// Resources path (sans extension) of the theme the static <see cref="Theme"/> accessor falls back to
		/// when no composition root has bound one — which is every edit-mode preview.
		/// </summary>
		public const string DefaultResourcePath = "Shell/ShellTheme";

		[SerializeField] private ColorEntry[] colors = Array.Empty<ColorEntry>();
		[SerializeField] private TextStyle[] textStyles = Array.Empty<TextStyle>();
		[SerializeField] private MotionSettings motion = new();
		[SerializeField] private LayoutSettings layout = new();

		private Dictionary<ColorRole, Color>? _colorsByRole;
		private Dictionary<TextStyleId, TextStyle>? _stylesById;

		public MotionSettings Motion => motion;

		public LayoutSettings Layout => layout;

		/// <summary>
		/// The colour bound to <paramref name="role"/>. An unbound role returns magenta rather than throwing —
		/// a missing role should be loud on screen, not fatal at startup.
		/// </summary>
		public Color GetColor(ColorRole? role)
		{
			if (role == null)
			{
				Debug.LogWarning($"{name}: asked for a colour without naming a role.", this);
				return Color.magenta;
			}

			_colorsByRole ??= colors
				.Where(entry => entry.Role != null)
				.GroupBy(entry => entry.Role!)
				.ToDictionary(group => group.Key, group => group.First().Color);

			if (_colorsByRole.TryGetValue(role, out var color))
			{
				return color;
			}

			Debug.LogWarning($"{name}: no colour bound to role {role}.", this);
			return Color.magenta;
		}

		/// <summary>
		/// The style bound to <paramref name="id"/>, or null when the theme doesn't define one.
		/// </summary>
		public TextStyle? GetStyle(TextStyleId? id)
		{
			if (id == null)
			{
				return null;
			}

			_stylesById ??= textStyles
				.Where(style => style.Id != null)
				.GroupBy(style => style.Id!)
				.ToDictionary(group => group.Key, group => group.First());

			return _stylesById.TryGetValue(id, out var style) ? style : null;
		}

		/// <summary>
		/// Paints <paramref name="text"/> with the named style, resolving its colour role against this theme.
		/// </summary>
		public void ApplyStyle(TextStyleId? id, TMP_Text text)
		{
			var style = GetStyle(id);

			if (style is null)
			{
				var named = id == null ? "(no style named)" : id.ToString();
				Debug.LogWarning($"{name}: no text style bound to {named}.", this);
				return;
			}

			style.ApplyTo(text, GetColor(style.Color));
		}

		private void OnEnable()
		{
			InvalidateLookups();
		}

		private void OnValidate()
		{
			InvalidateLookups();
		}

		private void InvalidateLookups()
		{
			_colorsByRole = null;
			_stylesById = null;
		}

		/// <summary>One row of the palette. A list rather than a field per role so roles can be added freely.</summary>
		[Serializable]
		public sealed class ColorEntry
		{
			[SerializeField] private ColorRole? role;
			[SerializeField] private Color color = Color.magenta;

			public ColorRole? Role => role;

			public Color Color => color;
		}
	}
}

using System;
using Assembler.Shell.Controls;
using Assembler.Shell.Theming;
using Assembler.Shell.Theming.Binders;
using Assembler.Shell.Typography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Assembler.Shell.Editor.ShellBuildUtility;
using Object = UnityEngine.Object;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// Authors the shell's primitive prefabs — the hit target, the rules, the paper ground, the letterpress
	/// button and its three variants, the section header, the sheet frame and the drop-cap paragraph.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Re-runnable, like the rest of the shell's builders: an existing prefab is opened and reconfigured in
	/// place, so its GUID survives and every scene and variant already pointing at it keeps pointing at it.
	/// </para>
	/// <para>
	/// The measurements are the prototype's, in canvas units. Anything the theme already knows — the ledge, the
	/// hairline, the minimum hit target — is read from it rather than repeated here.
	/// </para>
	/// <para>
	/// Every graphic draws a sprite off <c>Shell/Art/UIAtlas.png</c> — the rounded plates, the keyline, the sheet
	/// chrome, the glyphs, and a plain white <c>Fill</c> for the square surfaces that need no shape at all. The
	/// square ones would look identical with no sprite, but an <see cref="Image"/> without one draws the built-in
	/// white texture and so breaks the batch either side of it; naming <c>Fill</c> keeps the whole shell on one
	/// texture. Shape is the sprite's business and colour is the role binder's, which is what lets a second theme
	/// re-skin the shell without a second sheet.
	/// </para>
	/// </remarks>
	public static class ShellPrefabBuilder
	{
		public const string PrefabsFolder = "Assets/Shell/Prefabs";

		public const string HitTargetPath = PrefabsFolder + "/HitTarget.prefab";
		public const string RulePath = PrefabsFolder + "/Rule.prefab";
		public const string PaperGroundPath = PrefabsFolder + "/PaperGround.prefab";
		public const string ButtonPath = PrefabsFolder + "/LetterpressButton.prefab";
		public const string ButtonAccentPath = PrefabsFolder + "/LetterpressButtonAccent.prefab";
		public const string ButtonQuietPath = PrefabsFolder + "/LetterpressButtonQuiet.prefab";
		public const string ButtonIconPath = PrefabsFolder + "/LetterpressButtonIcon.prefab";
		public const string SectionHeaderPath = PrefabsFolder + "/SectionHeader.prefab";
		public const string SheetFramePath = PrefabsFolder + "/SheetFrame.prefab";
		public const string LeadParagraphPath = PrefabsFolder + "/LeadParagraph.prefab";

		// The prototype's `.playbtn`: 15 units of padding above and below a 13-unit label, over a 4-unit ledge.
		private const float ButtonWidth = 350f;
		private const float ButtonHeight = 50f;

		// The prototype's `.sechead`: 14 above the rubric, 7 below it, then the double rule.
		private const float SectionHeaderTopPadding = 14f;
		private const float SectionHeaderBottomPadding = 7f;
		private const float SectionHeaderTitleHeight = 14f;

		// The atlas draws its icons on a 24-unit grid, so an icon at native size is 24 units square inside the
		// 44 the hit target guarantees.
		private const float IconSize = 24f;

		private const float SheetHeight = 320f;
		private const float SheetGrabWidth = 38f;
		private const float SheetGrabHeight = 4f;

		private const string LeadParagraphPlaceholder =
			"The lead story opens here, and the first letter of it is set as a drop cap that hangs through the " +
			"opening lines. Everything after it is indented to clear the cap, and closes back to the full " +
			"measure on the line below.";

		[MenuItem("Assembler/Shell/Build Shell Prefabs")]
		public static void BuildPrefabs()
		{
			EnsureFolder(PrefabsFolder);

			// The prefabs bind roles and styles by reference, so the members have to exist before anything is
			// authored against them.
			ShellAssetBuilder.CreateShellAssets();

			// A prefab that does not exist yet has to be built out of a real GameObject, and every GameObject
			// belongs to a scene — so the ones built here go in a scratch preview scene rather than in whatever
			// the editor happens to have open. Building in the open scene would leave it marked dirty even
			// though nothing in it changed, and the editor would then ask to save it at the next thing that
			// cares: in batch mode, a dialog nobody can answer.
			var workshop = EditorSceneManager.NewPreviewScene();

			try
			{
				BuildHitTarget(workshop);
				BuildRule(workshop);
				BuildPaperGround(workshop);
				BuildButton(workshop);
				BuildButtonVariants(workshop);
				BuildSectionHeader(workshop);
				BuildSheetFrame(workshop);
				BuildLeadParagraph(workshop);
			}
			finally
			{
				EditorSceneManager.ClosePreviewScene(workshop);
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"{nameof(ShellPrefabBuilder)}: shell primitives built under {PrefabsFolder}.");
		}

		private static void BuildHitTarget(Scene workshop)
		{
			using var prefab = PrefabScope.Open(HitTargetPath, "HitTarget", workshop);

			float minimum = Theme.Current.Layout.MinHitTarget;
			Pin(prefab.Root, Centre, Centre, Centre, Vector2.zero, new Vector2(minimum, minimum));

			var target = Ensure<HitTarget>(prefab.Root);
			target.raycastTarget = true;
		}

		private static void BuildRule(Scene workshop)
		{
			using var prefab = PrefabScope.Open(RulePath, "Rule", workshop);

			float hairline = Theme.Current.Layout.Hairline;
			Pin(prefab.Root, TopLeft, TopRight, TopCentre, Vector2.zero, new Vector2(0f, hairline));

			var line = Child(prefab.Root.transform, "Line");
			var lower = Child(prefab.Root.transform, "LineLower");

			Paint(line, "Rule", "Fill");
			Paint(lower, "Rule", "Fill");
			lower.SetActive(false);

			var rule = Ensure<Rule>(prefab.Root);
			SetField(rule, "line", line.GetComponent<RectTransform>());
			SetField(rule, "secondLine", lower.GetComponent<RectTransform>());
			SetInt(rule, "weight", (int)RuleWeight.Hairline);
			rule.Apply();
		}

		private static void BuildPaperGround(Scene workshop)
		{
			using var prefab = PrefabScope.Open(PaperGroundPath, "PaperGround", workshop);

			Stretch(prefab.Root);
			Paint(prefab.Root, "Paper", "Fill");
		}

		private static void BuildButton(Scene workshop)
		{
			using var prefab = PrefabScope.Open(ButtonPath, "LetterpressButton", workshop);

			var layout = Theme.Current.Layout;
			float ledge = layout.LetterpressLedge;
			float outline = layout.OutlineWidth;

			Pin(prefab.Root, Centre, Centre, Centre, Vector2.zero, new Vector2(ButtonWidth, ButtonHeight));
			Ensure<CanvasGroup>(prefab.Root);

			// Plate sits down and to the right of the face; the face's press consumes exactly that offset.
			var plate = Child(prefab.Root.transform, "Plate");
			Stretch(plate, new Vector2(ledge, 0f), new Vector2(0f, -ledge));
			Paint(plate, "Offset", "Plate");

			var face = Child(prefab.Root.transform, "Face");
			Stretch(face, new Vector2(0f, ledge), new Vector2(-ledge, 0f));
			Paint(face, "ButtonFace", "Plate");

			// The fill is inset by an outline's width. Painted the same role as the face here, so it is
			// invisible — a variant that paints the two differently gets an outlined button for free.
			var fill = Child(face.transform, "Fill");
			Stretch(fill, new Vector2(outline, outline), new Vector2(-outline, -outline));
			Paint(fill, "ButtonFace", "Plate");

			var label = Child(fill.transform, "Label");
			Stretch(label, new Vector2(12f, 0f), new Vector2(-12f, 0f));
			Write(label, "ButtonLabel", "PLAY TODAY'S", TextAlignmentOptions.Center);

			var hit = Child(prefab.Root.transform, "HitTarget");
			Stretch(hit);
			Ensure<HitTarget>(hit).raycastTarget = true;

			plate.transform.SetSiblingIndex(0);
			face.transform.SetSiblingIndex(1);
			hit.transform.SetSiblingIndex(2);

			var button = Ensure<LetterpressButton>(prefab.Root);
			button.transition = Selectable.Transition.None;
			button.navigation = new Navigation { mode = Navigation.Mode.None };
			button.targetGraphic = null;
			EditorUtility.SetDirty(button);

			SetField(button, "plate", plate.GetComponent<RectTransform>());
			SetField(button, "face", face.GetComponent<RectTransform>());
			SetField(button, "fill", fill.GetComponent<RectTransform>());
			SetField(button, "label", label.GetComponent<TMP_Text>());
			SetField(button, "hitTarget", hit.GetComponent<HitTarget>());
		}

		// Variants rather than copies: the structure, the press and the wiring are the base's, and each of these
		// changes only which roles its parts are painted from — except the icon, which drops the plate and so
		// presses by sinking instead of travelling.
		private static void BuildButtonVariants(Scene workshop)
		{
			using (var accent = PrefabScope.OpenVariant(ButtonAccentPath, ButtonPath, "LetterpressButtonAccent", workshop))
			{
				Repaint(accent.Root, "Plate", "Accent");
			}

			using (var quiet = PrefabScope.OpenVariant(ButtonQuietPath, ButtonPath, "LetterpressButtonQuiet", workshop))
			{
				Repaint(quiet.Root, "Plate", "Rule");
				Repaint(quiet.Root, "Face", "RuleHard");
				Repaint(quiet.Root, "Face/Fill", "Paper");
				Restyle(quiet.Root, "Face/Fill/Label", "QuietButtonLabel");

				// PlateLine is the keyline drawn as a sprite — a ring of exactly the outline width, hollow in the
				// middle. Painting a solid plate the rule colour and covering all but its edge with the fill would
				// read the same along the sides and thicken by half again at the corners, where an inset square
				// corner cuts back further than a stroke does.
				Reskin(quiet.Root, "Face", "PlateLine");
			}

			using (var icon = PrefabScope.OpenVariant(ButtonIconPath, ButtonPath, "LetterpressButtonIcon", workshop))
			{
				float minimum = Theme.Current.Layout.MinHitTarget;
				Pin(icon.Root, Centre, Centre, Centre, Vector2.zero, new Vector2(minimum, minimum));

				var plate = icon.Root.transform.Find("Plate")!.gameObject;
				plate.SetActive(false);

				// No plate means no ledge to consume, so the face fills the whole button and the press sinks.
				var face = icon.Root.transform.Find("Face")!.gameObject;
				Stretch(face);
				Hide(face);
				Hide(icon.Root.transform.Find("Face/Fill")!.gameObject);

				// The glyph is a sprite, not a character. The font atlas is baked from a newspaper's character
				// set — Latin-1 and its punctuation — so a glyph drawn as text would mean either extending that
				// set per icon or living with a stand-in; the UI atlas draws the same marks as art, at the same
				// four-times scale as the rest of the sheet.
				var label = icon.Root.transform.Find("Face/Fill/Label")!.gameObject;
				label.SetActive(false);

				var glyph = Child(face.transform, "Icon");
				Pin(glyph, Centre, Centre, Centre, Vector2.zero, new Vector2(IconSize, IconSize));
				Paint(glyph, "Ink", "IconSearch");

				var button = icon.Root.GetComponent<LetterpressButton>();
				ClearField(button, "plate");
				ClearField(button, "label");
				SetField(button, "icon", glyph.GetComponent<Image>());
			}
		}

		private static void BuildSectionHeader(Scene workshop)
		{
			using var prefab = PrefabScope.Open(SectionHeaderPath, "SectionHeader", workshop);

			float ruleHeight = SectionHeaderTopPadding + SectionHeaderTitleHeight + SectionHeaderBottomPadding;
			float height = ruleHeight + (Theme.Current.Layout.Hairline * 3.5f);

			Pin(prefab.Root, TopLeft, TopRight, TopCentre, Vector2.zero, new Vector2(0f, height));

			var element = Ensure<LayoutElement>(prefab.Root);
			element.minHeight = height;
			element.preferredHeight = height;
			EditorUtility.SetDirty(element);

			var title = Child(prefab.Root.transform, "Title");
			Pin(
				title,
				TopLeft,
				TopLeft,
				TopLeft,
				new Vector2(0f, -SectionHeaderTopPadding),
				new Vector2(220f, SectionHeaderTitleHeight));
			Write(title, "SectionHeader", "MORE EDITIONS", TextAlignmentOptions.TopLeft);

			var caption = Child(prefab.Root.transform, "Caption");
			Pin(
				caption,
				TopRight,
				TopRight,
				TopRight,
				new Vector2(0f, -SectionHeaderTopPadding),
				new Vector2(140f, SectionHeaderTitleHeight));
			Write(caption, "CardMeta", string.Empty, TextAlignmentOptions.TopRight);
			caption.SetActive(false);

			var rule = NestRule(prefab.Root.transform, "Rule", RuleWeight.Double, "RuleHard");
			Pin(rule, BottomLeft, BottomRight, BottomCentre, Vector2.zero, new Vector2(0f, height - ruleHeight));

			var header = Ensure<SectionHeader>(prefab.Root);
			SetField(header, "title", title.GetComponent<TMP_Text>());
			SetField(header, "caption", caption.GetComponent<TMP_Text>());
		}

		private static void BuildSheetFrame(Scene workshop)
		{
			using var prefab = PrefabScope.Open(SheetFramePath, "SheetFrame", workshop);

			Stretch(prefab.Root);

			var scrim = Child(prefab.Root.transform, "Scrim");
			Stretch(scrim);
			Paint(scrim, "Scrim", "Fill");
			var scrimGroup = Ensure<CanvasGroup>(scrim);

			var scrimHit = Child(scrim.transform, "ScrimHitTarget");
			Stretch(scrimHit);
			Ensure<HitTarget>(scrimHit).raycastTarget = true;

			var sheet = Child(prefab.Root.transform, "Sheet");
			Pin(sheet, BottomLeft, BottomRight, BottomCentre, Vector2.zero, new Vector2(0f, SheetHeight));

			// SheetTop rounds its top two corners and leaves the bottom square, so the sheet sits flush on the
			// safe-area edge however far it is dragged.
			Paint(sheet, "Surface", "SheetTop");

			// First child, so anything an overlay parents into Content draws — and is hit — above it. Its job is
			// only to stop a tap on empty sheet falling through to the scrim and reading as "dismiss".
			var sheetHit = Child(sheet.transform, "SheetHitTarget");
			Stretch(sheetHit);
			Ensure<HitTarget>(sheetHit).raycastTarget = true;
			sheetHit.transform.SetSiblingIndex(0);

			var topRule = NestRule(sheet.transform, "TopRule", RuleWeight.Hairline, "Rule");
			Pin(topRule, TopLeft, TopRight, TopCentre, Vector2.zero, new Vector2(0f, Theme.Current.Layout.Hairline));

			var grab = Child(sheet.transform, "Grab");
			Pin(
				grab,
				TopCentre,
				TopCentre,
				TopCentre,
				new Vector2(0f, -10f),
				new Vector2(SheetGrabWidth, SheetGrabHeight));
			Paint(grab, "Rule", "PillSmall");

			var content = Child(sheet.transform, "Content");
			Stretch(content, new Vector2(16f, 26f), new Vector2(-16f, -30f));

			var frame = Ensure<SheetFrame>(prefab.Root);
			SetField(frame, "scrim", scrimGroup);
			SetField(frame, "scrimHit", scrimHit.GetComponent<HitTarget>());
			SetField(frame, "sheet", sheet.GetComponent<RectTransform>());
			SetField(frame, "content", content.GetComponent<RectTransform>());
		}

		private static void BuildLeadParagraph(Scene workshop)
		{
			using var prefab = PrefabScope.Open(LeadParagraphPath, "LeadParagraph", workshop);

			Pin(prefab.Root, Centre, Centre, Centre, Vector2.zero, new Vector2(ButtonWidth, 140f));
			var body = Write(prefab.Root, "Body", LeadParagraphPlaceholder, TextAlignmentOptions.TopLeft);
			body.textWrappingMode = TextWrappingModes.Normal;

			var cap = Child(prefab.Root.transform, "Cap");
			Pin(cap, TopLeft, TopLeft, TopLeft, Vector2.zero, new Vector2(40f, 40f));

			var capText = Ensure<TextMeshProUGUI>(cap);
			capText.raycastTarget = false;
			capText.textWrappingMode = TextWrappingModes.NoWrap;
			capText.overflowMode = TextOverflowModes.Overflow;
			capText.alignment = TextAlignmentOptions.TopLeft;

			var dropCap = Ensure<DropCap>(prefab.Root);
			SetField(dropCap, "cap", capText);
			SetField(dropCap, "capStyle", Style("DropCap"));
			dropCap.Rebuild();
		}

		private static GameObject NestRule(Transform parent, string name, RuleWeight weight, string role)
		{
			var existing = parent.Find(name);
			GameObject instance;

			if (existing != null)
			{
				instance = existing.gameObject;
			}
			else
			{
				var source = AssetDatabase.LoadAssetAtPath<GameObject>(RulePath);

				if (source == null)
				{
					throw new InvalidOperationException(
						$"{nameof(ShellPrefabBuilder)}: {RulePath} has not been built yet.");
				}

				instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
				instance.name = name;
			}

			var rule = instance.GetComponent<Rule>();
			SetInt(rule, "weight", (int)weight);
			Repaint(instance, "Line", role);
			Repaint(instance, "LineLower", role);
			rule.Apply();

			return instance;
		}

		// A theme-bound graphic: an Image that draws an atlas sprite, takes its colour from a role and, per
		// UIPLAN 7.4, does not raycast.
		private static Image Paint(GameObject target, string role, string sprite, float alpha = 1f)
		{
			var image = Ensure<Image>(target);
			image.raycastTarget = false;
			Skin(image, sprite);

			var binder = Ensure<ThemeColor>(target);
			SetField(binder, "role", Role(role));
			SetFloat(binder, "alpha", alpha);
			binder.Apply();

			return image;
		}

		private static TextMeshProUGUI Write(
			GameObject target,
			string style,
			string text,
			TextAlignmentOptions alignment)
		{
			var label = Ensure<TextMeshProUGUI>(target);
			label.raycastTarget = false;
			label.alignment = alignment;
			label.SetText(text);

			var binder = Ensure<TextStyleBinder>(target);
			SetField(binder, "style", Style(style));
			binder.Apply();

			return label;
		}

		// Sliced or simple is read off the sprite's own border rather than repeated here: the slice table decides
		// which sprites are nine-slices, and a second copy of that decision is a second thing to keep in step.
		//
		// The multiplier stays at 1. The sheet is authored at four times its unit size and imports at pixels per
		// unit 4, and the shell canvas answers with a reference of 1 — so a border of nine sheet pixels is two
		// and a quarter units, and Set Native Size lands on the unit size the art was drawn for.
		private static void Skin(Image image, string sprite)
		{
			var art = Sprite(sprite);

			image.sprite = art;
			image.type = art.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
			image.pixelsPerUnitMultiplier = 1f;
			EditorUtility.SetDirty(image);
		}

		private static void Reskin(GameObject root, string path, string sprite)
		{
			var target = Find(root, path);
			var image = target.GetComponent<Image>();

			if (image == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellPrefabBuilder)}: '{path}' carries no {nameof(Image)} to reskin.");
			}

			Skin(image, sprite);
		}

		private static void Repaint(GameObject root, string path, string role)
		{
			var target = Find(root, path);
			var binder = target.GetComponent<ThemeColor>();

			if (binder == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellPrefabBuilder)}: '{path}' carries no {nameof(ThemeColor)} to repaint.");
			}

			SetField(binder, "role", Role(role));
			binder.Apply();
		}

		private static void Restyle(GameObject root, string path, string style)
		{
			var target = Find(root, path);
			var binder = target.GetComponent<TextStyleBinder>();

			if (binder == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellPrefabBuilder)}: '{path}' carries no {nameof(TextStyleBinder)} to restyle.");
			}

			SetField(binder, "style", Style(style));
			binder.Apply();
		}

		// Turning the graphic off rather than painting it transparent: an icon button has no field, and a
		// disabled Image costs nothing where a clear one still batches.
		private static void Hide(GameObject target)
		{
			var image = target.GetComponent<Image>();

			if (image != null)
			{
				image.enabled = false;
				EditorUtility.SetDirty(image);
			}

			var binder = target.GetComponent<ThemeColor>();

			if (binder != null)
			{
				binder.enabled = false;
				EditorUtility.SetDirty(binder);
			}
		}

		private static GameObject Find(GameObject root, string path)
		{
			var found = root.transform.Find(path);

			if (found == null)
			{
				throw new InvalidOperationException($"{nameof(ShellPrefabBuilder)}: no '{path}' under {root.name}.");
			}

			return found.gameObject;
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
			{
				return;
			}

			int separator = path.LastIndexOf('/');
			AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
		}

		// Vector2 is forced here by the RectTransform anchor API.
		private static Vector2 Centre => new(0.5f, 0.5f);

		private static Vector2 TopLeft => new(0f, 1f);

		private static Vector2 TopRight => new(1f, 1f);

		private static Vector2 TopCentre => new(0.5f, 1f);

		private static Vector2 BottomLeft => new(0f, 0f);

		private static Vector2 BottomRight => new(1f, 0f);

		private static Vector2 BottomCentre => new(0.5f, 0f);

		/// <summary>
		/// An open prefab, saved back to the same path when it closes. Opening an existing one keeps its GUID —
		/// which is what makes re-running this safe for the scenes and variants already pointing at it.
		/// </summary>
		private sealed class PrefabScope : IDisposable
		{
			private readonly string _path;
			private readonly bool _fromDisk;

			private PrefabScope(GameObject root, string path, bool fromDisk)
			{
				Root = root;
				_path = path;
				_fromDisk = fromDisk;
			}

			public GameObject Root { get; }

			public static PrefabScope Open(string path, string rootName, Scene workshop)
			{
				if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
				{
					// LoadPrefabContents opens the prefab in a preview scene of its own, so this path never
					// touches the workshop or anything the editor has open.
					return new PrefabScope(PrefabUtility.LoadPrefabContents(path), path, fromDisk: true);
				}

				var root = new GameObject(rootName, typeof(RectTransform));
				SceneManager.MoveGameObjectToScene(root, workshop);

				return new PrefabScope(root, path, fromDisk: false);
			}

			/// <summary>Opens a variant of <paramref name="basePath"/>, creating it from an instance if absent.</summary>
			public static PrefabScope OpenVariant(string path, string basePath, string rootName, Scene workshop)
			{
				if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
				{
					return new PrefabScope(PrefabUtility.LoadPrefabContents(path), path, fromDisk: true);
				}

				var source = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);

				if (source == null)
				{
					throw new InvalidOperationException(
						$"{nameof(ShellPrefabBuilder)}: {basePath} has not been built yet.");
				}

				// Saving an instance of a prefab as a prefab is what makes the result a variant rather than a
				// copy — so the base keeps driving everything this one does not override.
				var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, workshop);
				instance.name = rootName;

				return new PrefabScope(instance, path, fromDisk: false);
			}

			public void Dispose()
			{
				PrefabUtility.SaveAsPrefabAsset(Root, _path);

				if (_fromDisk)
				{
					PrefabUtility.UnloadPrefabContents(Root);
					return;
				}

				Object.DestroyImmediate(Root);
			}
		}
	}
}

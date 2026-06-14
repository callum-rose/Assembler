using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// All Claude-facing prompt text for the pipeline. Everything is Y-up
	/// (x = right, y = up, z = forward towards the viewer); the Z-up swap
	/// happens in code at the storage boundary, never in a prompt.
	/// </summary>
	public static class VoxelizationPrompts
	{
		private const string CoordinateDoc =
			"Coordinates are integer voxel cells, Y-up: x = right, y = up, z = forward (towards the viewer). " +
			"y = 0 is the ground plane. FACING: the subject faces the viewer — its front (face, eyes, beak, chest, " +
			"headlights, windshield) goes on the HIGH-z side, its back/tail on the low-z side.";

		// ---- Stage 0: manifest ----------------------------------------------

		public const string ManifestSystem =
			"You are the art director for a voxel game asset set. Given a game brief, produce the set manifest " +
			"(the 'scale bible') that keeps every asset's size consistent relative to the others.\n\n" +
			"Rules:\n" +
			"- ALL dimensions are IN VOXELS (whole numbers). Pick a keystone asset (usually a character) at a " +
			"good voxel height — a person reads well at ~10-16 voxels — and size everything else relative to it. " +
			"Small props still get enough voxels to read; nothing exceeds roughly 100 voxels on its longest axis.\n" +
			"- Every asset gets its FULL BOUNDING BOX, in a fixed orientation shared by all models: `height` is the " +
			"extent UP (y), `length` is the extent along the asset's FORWARD axis (z — a car's nose-to-tail length, " +
			"an animal's head-to-tail), `width` is the extent LEFT-RIGHT (x — a car's track, a person's shoulder " +
			"span). Get the aspect right: a car is LONGER than it is wide and wider than it is tall; a person is " +
			"taller than wide and very shallow front-to-back. Optional `tolerance` (default 1) is the ± voxels the " +
			"built model may deviate on each axis — these are enforced deterministically downstream.\n" +
			"- ONE asset per distinct THING placed in the game world. A compound object is a single asset whose " +
			"components become parts of its rig at the planning stage — a car is one asset (wheels, body, and windows " +
			"are its parts, NOT separate assets); a windmill is one asset including its blades. Only split something " +
			"out when the game places it independently (a detachable trailer, a pickup item).\n" +
			"- `description` is BINDING theming for the asset, carried verbatim to every later stage: distil into it " +
			"EVERYTHING the brief says about this asset — colours, materials, style, distinguishing features (a " +
			"brief's 'green crossy-road style car' must yield a description saying green and blocky-minimal) — then " +
			"fill obvious gaps with sensible choices. Never drop a stated detail.\n" +
			"- Every asset needs: `id` (snake_case), `description`, `height`, `length`, `width` (voxels), " +
			"`symmetry` (bilateral | radial:N | none), `rig` (true only for things that need posing/animation).\n" +
			"- Do not invent `reference` entries; reference images are attached by the operator afterwards.\n\n" +
			"Output exactly one fenced block labelled `yaml` in this shape:\n" +
			"```yaml\n" +
			"game: medieval_village\n" +
			"assets:\n" +
			"  - id: villager\n" +
			"    description: \"peasant in a brown tunic and dark boots, tanned skin, simple low-detail style\"\n" +
			"    height: 12\n" +
			"    length: 3\n" +
			"    width: 7\n" +
			"    symmetry: bilateral\n" +
			"    rig: true\n" +
			"  - id: cart\n" +
			"    description: \"weathered oak hand-cart with two spoked wheels, muted browns\"\n" +
			"    height: 6\n" +
			"    length: 14\n" +
			"    width: 7\n" +
			"    symmetry: bilateral\n" +
			"    rig: false\n" +
			"```";

		public static string ManifestUser(string gameBrief) =>
			"Game brief:\n" + gameBrief + "\n\nProduce the set manifest.";

		// ---- Run folder naming ----------------------------------------------

		public const string FolderNameSystem =
			"You name the output folder for a set of voxel game assets. Reply with ONLY a short, descriptive " +
			"folder name capturing what the set IS — 2 to 4 words, lowercase, words separated by single hyphens " +
			"(kebab-case). No date, no file extension, no quotes, no explanation. " +
			"Examples: pirate-cove-props, medieval-village, neon-racers.";

		public static string FolderNameUser(SetManifest manifest)
		{
			var assets = string.Join(", ", manifest.Assets.Select(a => a.Id));
			return $"Game: {manifest.Game}\nAssets: {assets}\n\nName the folder for this set.";
		}

		// ---- Stage 1: planning ----------------------------------------------

		public static string PlanningSystem(VoxelizationConfig config) =>
			"You are planning one voxel model as a tree of parts. You output the model SKELETON — palette, part " +
			"hierarchy with sizes and pivots, and pose names — but NOT the voxel geometry itself; each part is " +
			"authored separately afterwards from your plan.\n\n" +
			CoordinateDoc + "\n\n" +
			"Part rules:\n" +
			"- Each part: `id`, `parent` (another part id, or `root`), `pivot` [x,y,z] = the joint position in the " +
			"PARENT's local frame (the part rotates around this point when posed). The part's own local origin sits " +
			"at its pivot.\n" +
			"- `data` declares the plan: `encoding: planned`, `planned: layers|script|primitives`, `size: [x,y,z]`, " +
			"`offset: [x,y,z]` (where grid cell 0,0,0 sits in the part's local frame — use it to centre geometry " +
			"on the joint), and a one-line `note` telling the author what the part looks like.\n" +
			"- Choosing the encoding: `primitives` for parts that ARE simple solids or unions of a few — car bodies, " +
			"wheels (shallow cylinders), trunks, poles, walls, hulls, table tops; there is no point hand-drawing or " +
			"scripting a box. `layers` for small or organic parts that need cell-level detail (heads, torsos, limbs, " +
			"feet) — anything under ~100 declared voxels with surface detail. `script` only for parametric or " +
			$"repetitive patterns primitives cannot express. Any part over {config.PartVoxelBudget} declared voxels " +
			"MUST be primitives or a script (or be split into smaller parts).\n" +
			"- Proportions matter as much as symmetry: match limb lengths to the reference/subject. Human arms reach " +
			"from the shoulders to mid-thigh — nearly as long as the legs — and limbs at this scale are long and " +
			"thin (1 voxel thick), never stubby blocks.\n" +
			"- BILATERAL ASSETS ARE STRICT: the finished model must be exactly mirror-symmetric across the x=0 plane. " +
			"The skeleton is checked deterministically and REJECTED if its geometry cannot be symmetric:\n" +
			"  * Centre parts (pelvis, torso, neck, head): pivot.x = 0, size.x ODD, offset.x = -(size.x-1)/2, so the " +
			"grid straddles x=0 exactly. A 4-wide part at x=0 can NEVER be centred — never give a centre part an " +
			"even width.\n" +
			"  * Paired parts (arms, legs, ears, wheels): author ONE side only and declare the twin as " +
			"`mirror: { source: <part>, axis: x }` instead of `data` (pivot may be omitted — it is derived by " +
			"reflection). Every off-centre authored part MUST have such a twin. Mirror sources must be declared " +
			"before the mirror.\n" +
			"  * The overall model width is therefore odd.\n" +
			"- REUSE repeated parts like prefabs: when several parts are identical (four wheels, table legs, fence " +
			"posts), author ONE and declare each other instance as `copy: { source: <part> }` with its own pivot — " +
			"the geometry is reused verbatim, only the position differs. Combine with mirrors: wheel.FL authored, " +
			"wheel.FR = mirror of wheel.FL, wheel.BL = copy of wheel.FL at the rear pivot, wheel.BR = mirror of " +
			"wheel.BL. Copies are free (no authoring cost) and must be declared after their source.\n" +
			"- Parts whose geometry is naturally scattered (foliage, leaves, debris, pebbles) may set `loose: true` " +
			"on the part so disconnected chunks within it are allowed. Never mark body parts, limbs, or structural " +
			"pieces loose — a floating leaf is fine, a floating hand is not.\n" +
			"- BOUNDING BOX: the model has fixed target dimensions in a fixed orientation — height up (y), LENGTH " +
			"along the forward axis (z: nose-to-tail, head-to-tail), width left-right (x). Plan part boxes that span " +
			"each given target within its tolerance (checked deterministically, lowest geometry at y=0). Put the " +
			"LONG dimension of an elongated subject on z, never on x: a car's wheelbase runs along z and exceeds its " +
			"track width across x.\n" +
			"- Parts must physically touch their parent at the joint: plan part sizes and pivots so they add up. " +
			"A limb separated from the body by a gap along its length still connects at the joint — give the torso " +
			"a wide shoulder row for arms to attach to, never a free-floating limb.\n" +
			"- Declare parts parents-first: a part's `parent` (and a mirror's `source`) must appear EARLIER in the " +
			"list (checked deterministically).\n" +
			"- Palette: at most 12 colours, each a single-character key. `_` is reserved for empty.\n" +
			"- Rigged models get named `poses` (part id → [x,y,z] euler degrees, local). Always include `idle: {}`. " +
			"Unrigged models still use parts (and mirrors) for authoring economy, with `poses:` left empty.\n\n" +
			"Output a fenced block labelled `vmodel` containing the full model yaml:\n" +
			"```vmodel\n" +
			"model: villager\n" +
			"version: 1\n" +
			"rigged: true\n" +
			"origin: feet_center\n" +
			"palette:\n" +
			"  _: none\n" +
			"  S: \"#e0b080\"\n" +
			"parts:\n" +
			"  - id: torso\n" +
			"    parent: root\n" +
			"    pivot: [0, 4, 0]\n" +
			"    data: { encoding: planned, planned: layers, size: [3, 4, 2], offset: [-1, 0, -1], note: \"blue shirt block\" }\n" +
			"  - id: arm.R\n" +
			"    parent: torso\n" +
			"    pivot: [3, 3, 0]\n" +
			"    mirror: { source: arm.L, axis: x }\n" +
			"poses:\n" +
			"  idle: {}\n" +
			"```\n\n" +
			"A REFERENCE BRIEF may be provided in the user message. It was transcribed from the reference image by a " +
			"separate, independent pass and is AUTHORITATIVE — you cannot reinterpret it to fit your design:\n" +
			"- Lock the palette to the brief's colours exactly.\n" +
			"- Plan part sizes DIRECTLY from the silhouette rows ('#' = solid, '.' = empty, top row first). Read limb " +
			"thickness from its columns: a 1-cell-wide arm in the silhouette is a 1-voxel-thick part, never wider. " +
			"Preserve its gaps (e.g. between arms and torso) — never fill them with part volume.\n" +
			"- These are CHECKED DETERMINISTICALLY and the plan is rejected otherwise: the model's overall width must " +
			"match the silhouette width (within 1 voxel), and every silhouette cell must fall inside some part's box.\n" +
			"If the reference image is also attached, use it only for styling detail the brief doesn't capture.";

		public static string PlanningUser(
			SetManifest manifest,
			ManifestAsset asset,
			ReferenceBrief brief,
			bool hasImage,
			string refinementNote,
			string styleGuidance = "",
			string previousModelYaml = "")
		{
			var sb = new StringBuilder();
			sb.Append("Game: ").Append(manifest.Game).Append('\n');
			sb.Append("Asset: ").Append(asset.Id).Append('\n');
			if (asset.Description.Length > 0)
			{
				sb.Append("Description (BINDING — palette and shapes must match it; invent only where it is silent): ")
					.Append(asset.Description).Append('\n');
			}

			sb.Append("Bounding box (each specified extent is enforced ±").Append(Mathf.Max(0, asset.Tolerance)).Append("):\n");
			sb.Append("  height (y, up): ").Append(asset.Height).Append(" voxels\n");
			sb.Append("  length (z, FORWARD, nose-to-tail): ").Append(Extent(asset.Length)).Append('\n');
			sb.Append("  width (x, left-right): ").Append(Extent(asset.Width)).Append('\n');
			sb.Append("Symmetry: ").Append(asset.Symmetry).Append('\n');
			sb.Append("Rig: ").Append(asset.Rig ? "yes" : "no").Append('\n');

			var others = manifest.Assets.Where(a => a.Id != asset.Id).Select(a => $"{a.Id} ({a.Height} vox)");
			sb.Append("Other assets in the set (for stylistic consistency): ")
				.Append(string.Join(", ", others)).Append('\n');

			if (!brief.IsEmpty)
			{
				sb.Append("\nReference brief (authoritative — plan part boxes to match its silhouette exactly):\n");
				sb.Append(ReferenceBriefYaml.Write(brief));
			}

			if (hasImage)
			{
				sb.Append("\nThe reference image is attached for styling detail.\n");
			}

			if (styleGuidance.Length > 0)
			{
				sb.Append("\nGlobal style guidance from the operator (applies to every asset — follow it):\n")
					.Append(styleGuidance).Append('\n');
			}

			if (refinementNote.Length > 0)
			{
				sb.Append("\nOperator refinement note (apply it):\n").Append(refinementNote).Append('\n');
			}

			if (previousModelYaml.Length > 0)
			{
				sb.Append("\nThe previously accepted model (your starting point — keep what the note doesn't change):\n")
					.Append("```vmodel\n").Append(previousModelYaml).Append("\n```\n");
			}

			sb.Append("\nPlan the model.");
			return sb.ToString();
		}

		private static string Extent(int voxels) => voxels > 0 ? $"{voxels} voxels" : "unconstrained";

		// ---- Stage 1a: reference brief extraction -----------------------------

		public const string BriefSystem =
			"You transcribe one or more labelled reference images of ONE subject into a structured brief for a " +
			"voxel-model pipeline. You do NOT plan, design, or improve anything — you only record what the images " +
			"show, as precisely as a scanner.\n\n" +
			"Each attached image is labelled in the user message with the perspective it shows (front, back, left, " +
			"right, top, or bottom). The images typically show a voxel/pixel-art style subject on a plain background: " +
			"identify the underlying cell grid and read it cell by cell. For other art, judge the cells at the " +
			"requested resolution.\n\n" +
			"Output exactly one fenced block labelled `brief`:\n" +
			"```brief\n" +
			"reference_brief:\n" +
			"  source: <subject>\n" +
			"  palette: { S: \"#e0b080\" }\n" +
			"  proportions: { head: 0.13, torso: 0.4, legs: 0.47 }\n" +
			"  signature_features: [\"blue shirt\", \"brown boots\", \"1-voxel gap between arms and torso\"]\n" +
			"  silhouettes:\n" +
			"    - face: front\n" +
			"      size: [<width in cells>, <height in cells>]\n" +
			"      rows:\n" +
			"        - \"..##..\"\n" +
			"    - face: right\n" +
			"      size: [<width in cells>, <height in cells>]\n" +
			"      rows:\n" +
			"        - \"..##..\"\n" +
			"```\n" +
			"Rules:\n" +
			"- ONE shared `palette` read across ALL images: at most 12 colours, single-character keys, hex values. " +
			"Include EVERY distinct colour from every image, even 1-2 cell details (eyes, buttons, trim) — downstream " +
			"stages are locked to this palette and cannot add colours you missed.\n" +
			"- Emit ONE `silhouettes` block per face the user asks for, reading each from the image labelled with that " +
			"face. Do NOT invent a silhouette for a face the user did not request.\n" +
			"- Silhouette rows use ONLY two characters: '#' (solid) and '.' (empty) — NEVER palette letters or any " +
			"other symbol. One character per cell, top row first.\n" +
			"- COUNT CAREFULLY: each silhouette's width and height must match that view's actual cell counts. Trace " +
			"each row of the image before writing it. Do not add empty margin rows or columns — the grid hugs the " +
			"subject's bounding box exactly.\n" +
			"- Mark gaps between limbs and the body (and between legs) as '.' — never blob separate shapes into one " +
			"solid mass; gaps are signature features and belong in signature_features too.\n" +
			"- For bilateral subjects the front/back/top rows must be exactly left-right symmetric (prefer an odd " +
			"width).\n" +
			"- proportions are fractions of total height per region, summing to ~1.";

		public static string BriefUser(
			ManifestAsset asset,
			IReadOnlyList<ReferenceImage> images,
			IReadOnlyList<string> requestedFaces)
		{
			var sb = new StringBuilder();
			sb.Append("Subject: ").Append(asset.Id).Append('\n');
			sb.Append("Attached images, each labelled with the perspective it shows:\n");
			for (var i = 0; i < images.Count; i++)
			{
				sb.Append("  Image ").Append(i + 1).Append(" = ").Append(images[i].Face).Append(" view\n");
			}

			sb.Append("Transcribe each silhouette at ").Append(asset.Height)
				.Append(" rows tall if the image's own grid allows.\n");
			sb.Append("Symmetry: ").Append(asset.Symmetry).Append('\n');
			sb.Append("Read ONE shared palette across all images, and transcribe a silhouette for EACH of these faces: ")
				.Append(string.Join(", ", requestedFaces)).Append(".\n\n");
			sb.Append("Transcribe the attached images into the brief.");
			return sb.ToString();
		}


		// ---- Stage 3: review -------------------------------------------------

		public const string ReviewSystem =
			"You review a finished voxel model against its reference. You see ASCII projections of what was " +
			"actually built (palette keys, top row first) and, when available, the original reference image.\n\n" +
			CoordinateDoc + "\n\n" +
			"Flag ONLY substantive structural problems: limbs the wrong length or thickness, parts in the wrong " +
			"place (including shifted in depth — compare the SIDE/TOP views), upside-down details (e.g. shoes at " +
			"the TOP of a leg), missing gaps between limbs and body, or colour regions in the wrong place. Ignore " +
			"single-voxel taste differences and styling.\n\n" +
			"If the model is a faithful match, reply with exactly: OK\n" +
			"Otherwise reply with a short numbered list of corrections, phrased as instructions to the planner " +
			"(which parts to resize, move, or re-shape, and how). No preamble.";

		public static string ReviewUser(VoxelRigModel model, string views, bool hasImage, string styleGuidance = "")
		{
			var sb = new StringBuilder();
			sb.Append("Model: ").Append(model.Id)
				.Append(" (").Append(model.TargetHeight).Append(" voxels tall, ")
				.Append(model.Parts.Count).Append(" parts)\n");
			if (model.Description.Length > 0)
			{
				sb.Append("Description (BINDING — flag any departure from it, e.g. wrong dominant colour): ")
					.Append(model.Description).Append('\n');
			}

			sb.Append("Palette:\n");
			foreach (var entry in model.Palette)
			{
				sb.Append("  ").Append(entry.Key).Append(" = ").Append(entry.ToHex()).Append('\n');
			}

			sb.Append('\n').Append(views).Append('\n');
			sb.Append(hasImage
				? "\nThe original reference image is attached — it is the ground truth. Compare the built views against it.\n"
				: "\nNo reference image: judge against the model's name and sane anatomy/structure.\n");
			if (styleGuidance.Length > 0)
			{
				sb.Append("\nThe operator's style guidance (intentional — do not flag adherence to it as a problem):\n")
					.Append(styleGuidance).Append('\n');
			}

			sb.Append("\nReply OK, or a numbered list of corrections for the planner.");
			return sb.ToString();
		}

		// ---- Stage 4: refine (minimal edits to an accepted model) ------------

		public const string RefineSystem =
			"You make a SMALL, TARGETED edit to a voxel model the operator already accepted. You are NOT re-planning: " +
			"keep everything the operator's note does not mention exactly as it is. You output a short list of EDIT " +
			"OPERATIONS; code applies them, re-assembles the affected parts, and re-validates.\n\n" +
			CoordinateDoc + "\n\n" +
			"Output EXACTLY one fenced block labelled `edits` containing a YAML sequence of operations, e.g.:\n" +
			"```edits\n" +
			"- { op: recolour, key: B, colour: \"#cc2222\" }\n" +
			"- { op: move_pivot, part: head, delta: [0, 1, 0] }\n" +
			"```\n\n" +
			"The operations:\n" +
			"- `{ op: recolour, key: K, colour: \"#rrggbb\" }` — change palette key K everywhere it is used. Use this " +
			"only when EVERY part using K should change.\n" +
			"- `{ op: add_colour, key: R, colour: \"#rrggbb\" }` — add a new palette key (≤12 colours total).\n" +
			"- `{ op: remap_colour, part: hat, from: G, to: R }` — within ONE part, swap key G for key R. This is how " +
			"you recolour just one part: `add_colour` the new shade, then `remap_colour` that part to it.\n" +
			"- `{ op: move_pivot, part: head, delta: [0, 1, 0] }` — nudge a part (and its mirror twin follows). A part " +
			"on the centre plane of a bilateral model may move only in y/z.\n" +
			"- `{ op: move_offset, part: torso, delta: [0, 0, -1] }` — shift a part's geometry within its own box.\n" +
			"- `{ op: reauthor, part: hat, instructions: \"wider brim\", size: [5, 2, 5] }` — re-draw one part from your " +
			"instructions; include `size`/`offset` only to resize its box (this is how you make a part bigger).\n" +
			"- `{ op: delete, part: tail }` — remove a part and anything that depends on it.\n" +
			"- `{ op: replan, reason: \"...\" }` — escape hatch: emit this SINGLE op when the request is structural or " +
			"ambiguous (add a whole new part, change the silhouette, rework proportions) — the model is re-planned in full.\n\n" +
			"Rules:\n" +
			"- Emit the FEWEST operations that satisfy the note. Never re-emit the model or unrelated edits.\n" +
			"- Edit only the AUTHORED side of a mirror pair (e.g. `arm.L`, never `arm.R`); the twin follows automatically. " +
			"Likewise recolour/remap the source of a copy, not the copy.\n" +
			"- Prefer `add_colour` + `remap_colour` for a scoped recolour; reserve `recolour` for a genuinely global change.\n" +
			"- If you cannot express the note with these ops, emit a single `replan` op — do not force a bad edit.";

		public static string RefineUser(VoxelRigModel model, ReferenceBrief brief, string note, string styleGuidance = "")
		{
			var sb = new StringBuilder();
			sb.Append("Model: ").Append(model.Id)
				.Append(" (").Append(model.TargetHeight).Append(" voxels tall, ")
				.Append(model.Parts.Count).Append(" parts)\n");
			if (model.Description.Length > 0)
			{
				sb.Append("Description (BINDING — the note overrides it only where it conflicts): ")
					.Append(model.Description).Append('\n');
			}

			sb.Append("\nThe accepted model (your starting point):\n");
			sb.Append("```vmodel\n").Append(VModelYaml.Write(model)).Append("\n```\n");

			if (!brief.IsEmpty)
			{
				if (brief.Palette.Count > 0)
				{
					sb.Append("\nReference palette: ")
						.Append(string.Join(", ", brief.Palette.Select(e => $"{e.Key}={e.ToHex()}"))).Append('\n');
				}

				if (brief.SignatureFeatures.Count > 0)
				{
					sb.Append("Reference signature features: ").Append(string.Join(", ", brief.SignatureFeatures)).Append('\n');
				}
			}

			if (styleGuidance.Length > 0)
			{
				sb.Append("\nGlobal style guidance from the operator (follow it):\n").Append(styleGuidance).Append('\n');
			}

			sb.Append("\nOperator note (make this change, nothing else):\n").Append(note).Append('\n');
			sb.Append("\nEmit the edits.");
			return sb.ToString();
		}

		// ---- Stage 2: layers authoring --------------------------------------

		public const string LayersSystem =
			"You author the voxel geometry of ONE part of a larger model, as ASCII layers.\n\n" +
			CoordinateDoc + "\n\n" +
			"Output format — one fenced block labelled `layers`:\n" +
			"- The block contains exactly size.y layers, bottom layer (y=0) first, separated by ONE blank line.\n" +
			"- Each layer is exactly size.z rows of exactly size.x characters. Row 1 of a layer is z=0 (the back); " +
			"later rows step towards the viewer.\n" +
			"- Blank lines go ONLY between layers, never between the rows of one layer — a layer with size.z = 2 " +
			"is two consecutive lines with no gap.\n" +
			"- Each character is a palette key, or '.' for empty.\n\n" +
			"Quality rules:\n" +
			"- The part must be ONE connected volume (no floating chunks).\n" +
			"- Fill enclosed interiors; models are viewed from all sides, so shape every face, not just the front.\n" +
			"- Respect the part's note, the palette meanings, and any reference features given.\n" +
			"- Geometry is placed at the declared offset in the part's local frame; cell (0,0,0) of your grid sits " +
			"at that offset. The pivot (local origin) is the joint to the parent — geometry near the joint should " +
			"reach it so the parts connect.\n\n" +
			"Example for size [4, 2, 3] (4 wide, 2 tall, 3 deep):\n" +
			"```layers\n" +
			".BB.\nBBBB\n.BB.\n\n" +
			".BB.\nBBBB\n.BB.\n" +
			"```\n" +
			"Output ONLY the fenced block.";

		public const string PrimitivesSystem =
			"You author the voxel geometry of ONE part of a larger model as a list of solid primitive shapes.\n\n" +
			CoordinateDoc + "\n\n" +
			"Output format — one fenced block labelled `primitives`, one shape per line:\n" +
			"  box      KEY minX minY minZ sizeX sizeY sizeZ        (optional trailing: round R [faces/edges])\n" +
			"  sphere   KEY cx cy cz r                              (optional trailing: half +y / -y / +x / -x / +z / -z)\n" +
			"  cylinder KEY axis baseX baseY baseZ r h              (axis is x, y, or z; optional trailing half clip)\n" +
			"  cut SHAPE ...                                        (carve voxels away: any shape above, but no KEY)\n" +
			"Rules:\n" +
			"- KEY is a declared palette key. Coordinates are GRID cells from [0,0,0] to size-1 — the grid is placed " +
			"at the declared offset for you; do NOT add the offset yourself.\n" +
			"- Centres and radii may be fractional: a 4-wide wheel is `cylinder K z 1.5 1.5 0 2 2` (centre between " +
			"cells; a cell is included when its centre lies within the radius). Box min/size and cylinder height are " +
			"whole numbers.\n" +
			"- `round R` rounds ALL of a box's edges/corners with radius R. To round only part of it, list faces and/or " +
			"edges after R: a face `+y` rounds that face's four edges (rounded top), an edge `+y+z` (two perpendicular " +
			"faces) rounds that one edge. `round 1 +y -x` rounds the top face and the left face.\n" +
			"- Do NOT round both edges of a face that are only 2 voxels apart — the carvings would meet and shrink the " +
			"box by a voxel. (So a slab only 2 thick can have ONE major face rounded, e.g. `round 1 +y`, but not both " +
			"and not all over.) This is rejected; round one edge, make it ≥3 thick, or leave it square.\n" +
			"- `half` keeps only the named side of a sphere or cylinder (a dome is `sphere ... half +y`).\n" +
			"- Later lines overwrite earlier ones where they overlap — build big solids first, then details on top.\n" +
			"- `cut SHAPE ...` removes voxels in that shape instead of adding them: the same geometry args as the shape, " +
			"but with NO palette KEY (e.g. `cut box 1 1 -1 3 3 7` bores a square tunnel, `cut sphere 4 4 4 3` hollows a " +
			"shell). It carves whatever is present when the line runs, so order matters — fill a solid first, then cut " +
			"openings/cavities; a later fill can refill a cut. Use it for hollows, windows, bores, sockets, and mouths.\n" +
			"- Shapes are clipped to the declared size; geometry must still form ONE connected volume and fill the " +
			"window's role in the model (no floating pieces).\n\n" +
			"Example for a 5x3x5 base slab on a roller, palette G=grey, K=black:\n" +
			"```primitives\n" +
			"box G 0 1 0 5 2 5 round 1 +y\n" +
			"cylinder K z 2 0.5 0 1.5 5\n" +
			"```\n" +
			"Example carving a hollow box with a doorway, palette W=white:\n" +
			"```primitives\n" +
			"box W 0 0 0 6 6 6\n" +
			"cut box 1 1 1 4 4 4\n" +
			"cut box 2 0 -1 2 3 8\n" +
			"```\n" +
			"Output ONLY the fenced block.";

		public const string ScriptSystem =
			"You author the voxel geometry of ONE part of a larger model by writing a C# script for the " +
			"`run_voxel_script` tool, then iterating on the tool's feedback until it builds cleanly.\n\n" +
			CoordinateDoc + "\n\n" +
			"Authoring rules:\n" +
			"- Keep ALL geometry inside [offset, offset + size) for the declared size and offset you are given.\n" +
			"- Use ONLY the declared palette colours, via b.Hex(\"#rrggbb\") with the exact hex values given.\n" +
			"- Scripts must be deterministic: literal values only, no randomness. Pseudo-variation is fine " +
			"(e.g. varying by `(x * 7 + z * 13) % 5`).\n" +
			"- The part must be ONE connected volume (no floating chunks), shaped on all sides.\n" +
			"- After the tool succeeds and the summary looks right, reply with just: done.\n\n" +
			VoxelBuilderApiDoc;

		/// <summary>Y-up tool description overriding the executor's Z-up default (the new pipeline is Y-up end to end).</summary>
		public const string ScriptToolDescription =
			"Build a voxel part procedurally by running a short C# script against the VoxelBuilder host API. The " +
			"script is a method body bound to a VoxelBuilder named 'b'; it must return b.Build(). Coordinates are " +
			"integers, Y-up (x = right, y = up, z = forward). On success you get a summary (voxel count, bounds, " +
			"palette size); on error you get the compile/runtime message so you can correct and retry.";

		public static string PartUser(
			VoxelRigModel model,
			ReferenceBrief brief,
			VoxelPart part,
			PlannedPartData planned,
			string feedback,
			string styleGuidance = "")
		{
			var sb = new StringBuilder();
			sb.Append("Model: ").Append(model.Id)
				.Append(" (").Append(model.TargetHeight).Append(" voxels tall overall)\n");
			if (model.Description.Length > 0)
			{
				sb.Append("Description (BINDING — match it): ").Append(model.Description).Append('\n');
			}

			sb.Append("Palette:\n");
			foreach (var entry in model.Palette)
			{
				sb.Append("  ").Append(entry.Key).Append(" = ").Append(entry.ToHex()).Append('\n');
			}

			sb.Append("\nPart to author: ").Append(part.Id).Append('\n');
			sb.Append("  parent: ").Append(part.Parent)
				.Append("  pivot in parent: ").Append(YamlNodes.Vector(part.Pivot)).Append('\n');
			sb.Append("  size: ").Append(YamlNodes.Vector(planned.Size))
				.Append("  offset: ").Append(YamlNodes.Vector(planned.Offset)).Append('\n');

			var localMax = planned.Offset + planned.Size - Vector3Int.one;
			sb.Append("  allowed local cells: x ").Append(planned.Offset.x).Append("..").Append(localMax.x)
				.Append(", y ").Append(planned.Offset.y).Append("..").Append(localMax.y)
				.Append(", z ").Append(planned.Offset.z).Append("..").Append(localMax.z)
				.Append(" — place geometry HERE, not at the origin\n");

			var worldMin = PlanGeometryChecks.WorldPivot(model, part) + planned.Offset;
			var worldMax = worldMin + planned.Size - Vector3Int.one;
			sb.Append("  occupies world cells: x ").Append(worldMin.x).Append("..").Append(worldMax.x)
				.Append(", y ").Append(worldMin.y).Append("..").Append(worldMax.y)
				.Append(", z ").Append(worldMin.z).Append("..").Append(worldMax.z)
				.Append(" (y=0 is the ground)\n");
			if (worldMin.y == 0)
			{
				sb.Append("  GROUND: this part touches the ground. Your FIRST layer is the LOWEST row of the part — " +
						  "feet/shoes/base details go in the first layer, never the last.\n");
			}
			if (planned.Note.Length > 0)
			{
				sb.Append("  note: ").Append(planned.Note).Append('\n');
			}

			if (model.IsBilateral && PlanGeometryChecks.WorldPivot(model, part).x == 0)
			{
				sb.Append("  SYMMETRY: this part sits on the mirror plane of a bilateral model. Its grid MUST be " +
						  "exactly left-right symmetric — column i mirrors column size.x-1-i with identical colours. " +
						  "This is validated cell-by-cell.\n");
			}

			sb.Append("\nNeighbouring parts (for proportion/joint context):\n");
			foreach (var other in model.Parts.Where(p => p.Id != part.Id))
			{
				sb.Append("  ").Append(other.Id).Append(" parent=").Append(other.Parent)
					.Append(" pivot=").Append(YamlNodes.Vector(other.Pivot));
				if (other.Data is PlannedPartData otherPlanned)
				{
					sb.Append(" size=").Append(YamlNodes.Vector(otherPlanned.Size));
				}

				sb.Append('\n');
			}

			if (!brief.IsEmpty)
			{
				sb.Append("\nReference brief (authoritative):\n");
				if (brief.SignatureFeatures.Count > 0)
				{
					sb.Append("  signature features: ").Append(string.Join(", ", brief.SignatureFeatures)).Append('\n');
				}

				foreach (var kv in brief.Proportions)
				{
					sb.Append("  proportion ").Append(kv.Key).Append(": ").Append(YamlNodes.Float(kv.Value)).Append('\n');
				}

				var primary = brief.PrimarySilhouette;
				if (!primary.IsEmpty)
				{
					sb.Append("  ").Append(primary.Face)
						.Append(" silhouette of the WHOLE model (top row first, '#' solid) — author your part so ")
						.Append("the cells it occupies match it:\n");
					foreach (var row in primary.Rows)
					{
						sb.Append("    ").Append(row).Append('\n');
					}
				}
			}

			if (styleGuidance.Length > 0)
			{
				sb.Append("\nGlobal style guidance from the operator (follow it):\n").Append(styleGuidance).Append('\n');
			}

			if (feedback.Length > 0)
			{
				sb.Append("\nYour previous attempt failed validation — fix exactly this:\n").Append(feedback).Append('\n');
			}

			sb.Append("\nAuthor the part now.");
			return sb.ToString();
		}

		private const string VoxelBuilderApiDoc =
			"## The script\n\n" +
			"The `script` argument is a C# method body (no signature). It receives a `VoxelBuilder` named `b`, " +
			"places voxels by calling methods on `b`, and MUST end with `return b.Build();`.\n\n" +
			"Colours come from helper methods (never construct a Color directly):\n" +
			"    var skin = b.Hex(\"#e0b080\");\n\n" +
			"Placement:\n" +
			"    b.Set(x, y, z, c);  b.Clear(x, y, z);  var c2 = b.Get(x, y, z);  var has = b.Has(x, y, z);  var n = b.Count;\n\n" +
			"Solids (all take a final colour):\n" +
			"    b.Box(x0, y0, z0, x1, y1, z1, c);        // filled box (inclusive corners)\n" +
			"    b.HollowBox(x0, y0, z0, x1, y1, z1, c);  // shell only\n" +
			"    b.Sphere(cx, cy, cz, r, c);\n" +
			"    b.Ellipsoid(cx, cy, cz, rx, ry, rz, c);\n" +
			"    b.Cylinder(cx, cy, cz, radius, height, axis, c);  // centred, runs along axis\n" +
			"    b.Cone(cx, cy, cz, radius, height, axis, c);      // base at -axis end, apex at +axis end\n" +
			"    b.Torus(cx, cy, cz, majorR, minorR, axis, c);\n\n" +
			"Lines / planes:\n" +
			"    b.Line(x0, y0, z0, x1, y1, z1, c);\n" +
			"    b.RectFill(axis, plane, u0, v0, u1, v1, c);  // axis X: u=Y,v=Z; axis Y: u=X,v=Z; axis Z: u=X,v=Y\n\n" +
			"Bulk transforms:\n" +
			"    b.Mirror(axis);           // reflect across the bbox centre on axis (keeps originals)\n" +
			"    b.Translate(dx, dy, dz);  // shift every voxel\n" +
			"    b.Fill(x, y, z, c);       // flood-fill connected EMPTY cells (clamped to current bbox)\n\n" +
			"`axis` is `VoxelAxis.X`, `VoxelAxis.Y`, or `VoxelAxis.Z` (the model's UP is Y).\n\n" +
			"## Language limits (violations fail the script)\n\n" +
			"The script is compiled by a restricted C# compiler. ONLY these work:\n" +
			"- Types: `int`, `float`, `double`, `bool`, `string`, and `var` for locals. Use `var` for builder/colour " +
			"results: `var c = b.Hex(\"#ffffff\");`.\n" +
			"- Control flow: `if`/`else`, `for`, `while`, `break`, `continue`, `return`. ALWAYS use braces.\n" +
			"- Operators: arithmetic, comparison, `&& || !`, ternary, casts `(int)x`, compound assignment, `++`/`--`.\n" +
			"- Local helper methods are allowed (e.g. `int sq(int n) { return n * n; }`).\n\n" +
			"NOT supported — do not use any of these:\n" +
			"- `foreach` (use `for` with an int index), `do/while`, `switch`.\n" +
			"- Arrays, lists, collection initializers, indexing.\n" +
			"- String interpolation, `??`, `?.`, `is`/`as`, `typeof`, tuples, pattern matching, generics, " +
			"`try/catch`, `throw`, `new` of any type.\n" +
			"- Multi-statement or multi-parameter lambdas.";
	}
}

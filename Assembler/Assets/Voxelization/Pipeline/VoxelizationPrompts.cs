using System.Linq;
using System.Text;

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
			"y = 0 is the ground plane.";

		// ---- Stage 0: manifest ----------------------------------------------

		public const string ManifestSystem =
			"You are the art director for a voxel game asset set. Given a game brief, produce the set manifest " +
			"(the 'scale bible') that anchors every asset to real-world dimensions through one global unit.\n\n" +
			"Rules:\n" +
			"- Pick a keystone asset (usually a character) and choose `unit` (metres per voxel) so that the keystone " +
			"comes out at a good voxel height (a person of 1.8m at 10 voxels tall means unit 0.18). Small props get " +
			"enough voxels to read; large assets stay under roughly 100 voxels on their longest axis.\n" +
			"- Every asset needs: `id` (snake_case), `real_world_height` (metres — the real object's height), " +
			"`symmetry` (bilateral | radial:N | none), `rig` (true only for things that need posing/animation).\n" +
			"- Do not invent `reference` entries; reference images are attached by the operator afterwards.\n\n" +
			"Output exactly one fenced block labelled `yaml` in this shape:\n" +
			"```yaml\n" +
			"game: medieval_village\n" +
			"unit: 0.18\n" +
			"assets:\n" +
			"  - id: villager\n" +
			"    real_world_height: 1.8\n" +
			"    symmetry: bilateral\n" +
			"    rig: true\n" +
			"```";

		public static string ManifestUser(string gameBrief) =>
			"Game brief:\n" + gameBrief + "\n\nProduce the set manifest.";

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
			"- `data` declares the plan: `encoding: planned`, `planned: layers|script`, `size: [x,y,z]`, " +
			"`offset: [x,y,z]` (where grid cell 0,0,0 sits in the part's local frame — use it to centre geometry " +
			"on the joint), and a one-line `note` telling the author what the part looks like.\n" +
			"- Choose `layers` for small organic/detailed parts (head, torso); `script` for geometric, parametric, " +
			$"repetitive, or large parts (trunks, wheels, foliage). Any part over {config.PartVoxelBudget} declared " +
			"voxels MUST be a script (or be split into smaller parts).\n" +
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
			"- Parts whose geometry is naturally scattered (foliage, leaves, debris, pebbles) may set `loose: true` " +
			"on the part so disconnected chunks within it are allowed. Never mark body parts, limbs, or structural " +
			"pieces loose — a floating leaf is fine, a floating hand is not.\n" +
			"- Parts must physically touch their parent at the joint, and the whole model must hit the required " +
			"voxel height exactly: plan part sizes and pivots so they add up.\n" +
			"- Palette: at most 12 colours, each a single-character key. `_` is reserved for empty.\n" +
			"- Rigged models get named `poses` (part id → [x,y,z] euler degrees, local). Always include `idle: {}`. " +
			"Unrigged models still use parts (and mirrors) for authoring economy, with `poses:` left empty.\n\n" +
			"Output a fenced block labelled `vmodel` containing the full model yaml:\n" +
			"```vmodel\n" +
			"model: villager\n" +
			"version: 1\n" +
			"rigged: true\n" +
			"unit: 0.18\n" +
			"real_world_height: 1.8\n" +
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
			"If a reference image is attached, it is AUTHORITATIVE: extract its palette (locked — the model palette " +
			"must use exactly these colours), proportions, signature features, and a front silhouette, and ALSO " +
			"output a fenced block labelled `brief`:\n" +
			"```brief\n" +
			"reference_brief:\n" +
			"  source: <reference name>\n" +
			"  real_world_dims: { height: 1.8, width: 0.5, depth: 0.3 }\n" +
			"  palette: { S: \"#e0b080\" }\n" +
			"  proportions: { head: 0.13, torso: 0.4, legs: 0.47 }\n" +
			"  signature_features: [\"blue shirt\", \"brown boots\"]\n" +
			"  silhouette:\n" +
			"    face: front\n" +
			"    size: [<model width in voxels>, <model height in voxels>]\n" +
			"    rows:\n" +
			"      - \"..##..\"\n" +
			"```\n" +
			"Silhouette rows are listed top row first; '#' = solid, '.' = empty; the grid must match the planned " +
			"model's front bounding box. For bilateral assets every row must be exactly left-right symmetric and the " +
			"width odd. Without an image, output no `brief` block.";

		public static string PlanningUser(SetManifest manifest, ManifestAsset asset, bool hasImage, string refinementNote)
		{
			var sb = new StringBuilder();
			sb.Append("Game: ").Append(manifest.Game).Append('\n');
			sb.Append("Global unit: ").Append(YamlNodes.Float(manifest.Unit)).Append(" metres per voxel\n");
			sb.Append("Asset: ").Append(asset.Id).Append('\n');
			sb.Append("Real-world height: ").Append(YamlNodes.Float(asset.RealWorldHeight))
				.Append("m -> the model must be ").Append(manifest.HeightInVoxels(asset)).Append(" voxels tall\n");
			sb.Append("Symmetry: ").Append(asset.Symmetry).Append('\n');
			sb.Append("Rig: ").Append(asset.Rig ? "yes" : "no").Append('\n');

			var others = manifest.Assets.Where(a => a.Id != asset.Id).Select(a => $"{a.Id} ({manifest.HeightInVoxels(a)} vox)");
			sb.Append("Other assets in the set (for stylistic consistency): ")
				.Append(string.Join(", ", others)).Append('\n');

			if (hasImage)
			{
				sb.Append("\nA reference image of ").Append(asset.Reference)
					.Append(" is attached. It is authoritative for palette, proportions, and silhouette.\n");
			}

			if (refinementNote.Length > 0)
			{
				sb.Append("\nOperator refinement note (apply it):\n").Append(refinementNote).Append('\n');
			}

			sb.Append("\nPlan the model.");
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
			string feedback)
		{
			var sb = new StringBuilder();
			sb.Append("Model: ").Append(model.Id)
				.Append(" (").Append(model.HeightInVoxels).Append(" voxels tall overall)\n");
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

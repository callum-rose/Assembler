# Libraries

Generated from the `Assembler.Libraries` XML doc comments. Every public static method of these classes is registered globally with the expression compiler, so descriptor expressions can call it by bare name.

## `ColorMath`

### `Color Brighten(Color c, float factor)`
_No XML docs — add `<summary>` on `ColorMath.Brighten`._

### `Color Darken(Color c, float factor)`
_No XML docs — add `<summary>` on `ColorMath.Darken`._

### `Color Grayscale(Color c)`
_No XML docs — add `<summary>` on `ColorMath.Grayscale`._

### `Color HsvToRgb(float h, float s, float v)`
_No XML docs — add `<summary>` on `ColorMath.HsvToRgb`._

### `Color LerpColor(Color a, Color b, float t)`
_No XML docs — add `<summary>` on `ColorMath.LerpColor`._

### `Vector3 RgbToHsv(Color c)`
_No XML docs — add `<summary>` on `ColorMath.RgbToHsv`._

### `Color WithAlpha(Color c, float alpha)`
_No XML docs — add `<summary>` on `ColorMath.WithAlpha`._

## `GridMath`

### `Vector3 CellToWorld(Vector3 cell, float originX, float originY)`
_No XML docs — add `<summary>` on `GridMath.CellToWorld`._

### `Vector3 CellToWorld(Vector3 cell, float originX, float originY, float cellSize)`
_No XML docs — add `<summary>` on `GridMath.CellToWorld`._

### `int CellsInRow(List<Vector3> occupied, float row)`
_No XML docs — add `<summary>` on `GridMath.CellsInRow`._

### `int FullRowCount(List<Vector3> occupied, float width, float height)`
_No XML docs — add `<summary>` on `GridMath.FullRowCount`._

### `int FullRowsBelow(List<Vector3> occupied, float row, float width, float height)`
_No XML docs — add `<summary>` on `GridMath.FullRowsBelow`._

### `bool InBounds(float col, float row, float width, float height)`
_No XML docs — add `<summary>` on `GridMath.InBounds`._

### `bool InBoundsOpenTop(float col, float row, float width)`
_No XML docs — add `<summary>` on `GridMath.InBoundsOpenTop`._

### `bool IsOccupied(List<Vector3> occupied, float col, float row)`
_No XML docs — add `<summary>` on `GridMath.IsOccupied`._

### `Vector3 NeighbourCell(Vector3 cell, float dCol, float dRow)`
_No XML docs — add `<summary>` on `GridMath.NeighbourCell`._

### `Vector3 RotateCellCW(Vector3 cell, int times)`
_No XML docs — add `<summary>` on `GridMath.RotateCellCW`._

### `bool RowFull(List<Vector3> occupied, float row, float width)`
_No XML docs — add `<summary>` on `GridMath.RowFull`._

### `Vector3 WorldToCell(Vector3 world, float originX, float originY)`
_No XML docs — add `<summary>` on `GridMath.WorldToCell`._

## `HexMath`

### `float HexDistance(Vector3 a, Vector3 b)`
_No XML docs — add `<summary>` on `HexMath.HexDistance`._

### `Vector3 HexNeighbour(Vector3 hex, int directionIndex)`
_No XML docs — add `<summary>` on `HexMath.HexNeighbour`._

### `List<Vector3> HexNeighbours(Vector3 hex)`
_No XML docs — add `<summary>` on `HexMath.HexNeighbours`._

### `Vector3 HexRound(Vector3 hex)`
_No XML docs — add `<summary>` on `HexMath.HexRound`._

### `Vector3 HexToWorldFlat(Vector3 hex, float size)`
_No XML docs — add `<summary>` on `HexMath.HexToWorldFlat`._

### `Vector3 HexToWorldPointy(Vector3 hex, float size)`
_No XML docs — add `<summary>` on `HexMath.HexToWorldPointy`._

## `NumberMath`

### `float Abs(float x)`
_No XML docs — add `<summary>` on `NumberMath.Abs`._

### `bool Approx(float a, float b)`
_No XML docs — add `<summary>` on `NumberMath.Approx`._

### `float Ceil(float x)`
_No XML docs — add `<summary>` on `NumberMath.Ceil`._

### `float Clamp(float x, float min, float max)`
_No XML docs — add `<summary>` on `NumberMath.Clamp`._

### `float Clamp01(float x)`
_No XML docs — add `<summary>` on `NumberMath.Clamp01`._

### `float DegToRad(float degrees)`
_No XML docs — add `<summary>` on `NumberMath.DegToRad`._

### `float Floor(float x)`
_No XML docs — add `<summary>` on `NumberMath.Floor`._

### `float Lerp(float a, float b, float t)`
_No XML docs — add `<summary>` on `NumberMath.Lerp`._

### `float Max(float a, float b)`
_No XML docs — add `<summary>` on `NumberMath.Max`._

### `float Min(float a, float b)`
_No XML docs — add `<summary>` on `NumberMath.Min`._

### `float RadToDeg(float radians)`
_No XML docs — add `<summary>` on `NumberMath.RadToDeg`._

### `float Remap(float x, float inMin, float inMax, float outMin, float outMax)`
_No XML docs — add `<summary>` on `NumberMath.Remap`._

### `float Round(float x)`
_No XML docs — add `<summary>` on `NumberMath.Round`._

### `float Sign(float x)`
_No XML docs — add `<summary>` on `NumberMath.Sign`._

## `RandomMath`

### `bool Chance(float probability)`
_No XML docs — add `<summary>` on `RandomMath.Chance`._

### `Vector3 Pick(List<Vector3> items)`
_No XML docs — add `<summary>` on `RandomMath.Pick`._

### `int PickInt(List<int> items)`
_No XML docs — add `<summary>` on `RandomMath.PickInt`._

### `Color RandomColor()`
_No XML docs — add `<summary>` on `RandomMath.RandomColor`._

### `Color RandomColorBetween(Color a, Color b)`
_No XML docs — add `<summary>` on `RandomMath.RandomColorBetween`._

### `float RandomFloat(float min, float max)`
_No XML docs — add `<summary>` on `RandomMath.RandomFloat`._

### `Vector3 RandomInsideCircle(float radius)`
_No XML docs — add `<summary>` on `RandomMath.RandomInsideCircle`._

### `int RandomInt(float minInclusive, float maxInclusive)`
_No XML docs — add `<summary>` on `RandomMath.RandomInt`._

### `Vector3 RandomOnCircle(float radius)`
_No XML docs — add `<summary>` on `RandomMath.RandomOnCircle`._

## `VectorMath`

### `Vector3 AddVector(Vector3 a, Vector3 b)`
_No XML docs — add `<summary>` on `VectorMath.AddVector`._

### `float Angle2D(Vector3 v)`
_No XML docs — add `<summary>` on `VectorMath.Angle2D`._

### `Vector3 Direction(Vector3 from, Vector3 to)`
_No XML docs — add `<summary>` on `VectorMath.Direction`._

### `float Distance(Vector3 a, Vector3 b)`
_No XML docs — add `<summary>` on `VectorMath.Distance`._

### `float Dot(Vector3 a, Vector3 b)`
_No XML docs — add `<summary>` on `VectorMath.Dot`._

### `Vector3 IntegratePosition(Vector3 pos, Vector3 vel)`
_No XML docs — add `<summary>` on `VectorMath.IntegratePosition`._

### `Vector3 IntegratePosition(Vector3 pos, Vector3 vel, float dt)`
_No XML docs — add `<summary>` on `VectorMath.IntegratePosition`._

### `Vector3 LerpVector(Vector3 a, Vector3 b, float t)`
_No XML docs — add `<summary>` on `VectorMath.LerpVector`._

### `float Magnitude(Vector3 v)`
_No XML docs — add `<summary>` on `VectorMath.Magnitude`._

### `Vector3 Normalize(Vector3 v)`
_No XML docs — add `<summary>` on `VectorMath.Normalize`._

### `Vector3 Rotate2D(Vector3 v, float degrees)`
_No XML docs — add `<summary>` on `VectorMath.Rotate2D`._

### `Vector3 ScaleVector(Vector3 v, float k)`
_No XML docs — add `<summary>` on `VectorMath.ScaleVector`._

### `Vector3 SubtractVector(Vector3 a, Vector3 b)`
_No XML docs — add `<summary>` on `VectorMath.SubtractVector`._

---

## Doc-gen warnings

- `ColorMath.Brighten`: no XML doc member found for id `M:Assembler.Libraries.ColorMath.Brighten(UnityEngine.Color,System.Single)`.
- `ColorMath.Darken`: no XML doc member found for id `M:Assembler.Libraries.ColorMath.Darken(UnityEngine.Color,System.Single)`.
- `ColorMath.Grayscale`: no XML doc member found for id `M:Assembler.Libraries.ColorMath.Grayscale(UnityEngine.Color)`.
- `ColorMath.HsvToRgb`: no XML doc member found for id `M:Assembler.Libraries.ColorMath.HsvToRgb(System.Single,System.Single,System.Single)`.
- `ColorMath.LerpColor`: no XML doc member found for id `M:Assembler.Libraries.ColorMath.LerpColor(UnityEngine.Color,UnityEngine.Color,System.Single)`.
- `ColorMath.RgbToHsv`: no XML doc member found for id `M:Assembler.Libraries.ColorMath.RgbToHsv(UnityEngine.Color)`.
- `ColorMath.WithAlpha`: no XML doc member found for id `M:Assembler.Libraries.ColorMath.WithAlpha(UnityEngine.Color,System.Single)`.
- `GridMath.CellToWorld`: no XML doc member found for id `M:Assembler.Libraries.GridMath.CellToWorld(UnityEngine.Vector3,System.Single,System.Single)`.
- `GridMath.CellToWorld`: no XML doc member found for id `M:Assembler.Libraries.GridMath.CellToWorld(UnityEngine.Vector3,System.Single,System.Single,System.Single)`.
- `GridMath.CellsInRow`: no XML doc member found for id `M:Assembler.Libraries.GridMath.CellsInRow(System.Collections.Generic.List{UnityEngine.Vector3},System.Single)`.
- `GridMath.FullRowCount`: no XML doc member found for id `M:Assembler.Libraries.GridMath.FullRowCount(System.Collections.Generic.List{UnityEngine.Vector3},System.Single,System.Single)`.
- `GridMath.FullRowsBelow`: no XML doc member found for id `M:Assembler.Libraries.GridMath.FullRowsBelow(System.Collections.Generic.List{UnityEngine.Vector3},System.Single,System.Single,System.Single)`.
- `GridMath.InBounds`: no XML doc member found for id `M:Assembler.Libraries.GridMath.InBounds(System.Single,System.Single,System.Single,System.Single)`.
- `GridMath.InBoundsOpenTop`: no XML doc member found for id `M:Assembler.Libraries.GridMath.InBoundsOpenTop(System.Single,System.Single,System.Single)`.
- `GridMath.IsOccupied`: no XML doc member found for id `M:Assembler.Libraries.GridMath.IsOccupied(System.Collections.Generic.List{UnityEngine.Vector3},System.Single,System.Single)`.
- `GridMath.NeighbourCell`: no XML doc member found for id `M:Assembler.Libraries.GridMath.NeighbourCell(UnityEngine.Vector3,System.Single,System.Single)`.
- `GridMath.RotateCellCW`: no XML doc member found for id `M:Assembler.Libraries.GridMath.RotateCellCW(UnityEngine.Vector3,System.Int32)`.
- `GridMath.RowFull`: no XML doc member found for id `M:Assembler.Libraries.GridMath.RowFull(System.Collections.Generic.List{UnityEngine.Vector3},System.Single,System.Single)`.
- `GridMath.WorldToCell`: no XML doc member found for id `M:Assembler.Libraries.GridMath.WorldToCell(UnityEngine.Vector3,System.Single,System.Single)`.
- `HexMath.HexDistance`: no XML doc member found for id `M:Assembler.Libraries.HexMath.HexDistance(UnityEngine.Vector3,UnityEngine.Vector3)`.
- `HexMath.HexNeighbour`: no XML doc member found for id `M:Assembler.Libraries.HexMath.HexNeighbour(UnityEngine.Vector3,System.Int32)`.
- `HexMath.HexNeighbours`: no XML doc member found for id `M:Assembler.Libraries.HexMath.HexNeighbours(UnityEngine.Vector3)`.
- `HexMath.HexRound`: no XML doc member found for id `M:Assembler.Libraries.HexMath.HexRound(UnityEngine.Vector3)`.
- `HexMath.HexToWorldFlat`: no XML doc member found for id `M:Assembler.Libraries.HexMath.HexToWorldFlat(UnityEngine.Vector3,System.Single)`.
- `HexMath.HexToWorldPointy`: no XML doc member found for id `M:Assembler.Libraries.HexMath.HexToWorldPointy(UnityEngine.Vector3,System.Single)`.
- `NumberMath.Abs`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Abs(System.Single)`.
- `NumberMath.Approx`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Approx(System.Single,System.Single)`.
- `NumberMath.Ceil`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Ceil(System.Single)`.
- `NumberMath.Clamp`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Clamp(System.Single,System.Single,System.Single)`.
- `NumberMath.Clamp01`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Clamp01(System.Single)`.
- `NumberMath.DegToRad`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.DegToRad(System.Single)`.
- `NumberMath.Floor`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Floor(System.Single)`.
- `NumberMath.Lerp`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Lerp(System.Single,System.Single,System.Single)`.
- `NumberMath.Max`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Max(System.Single,System.Single)`.
- `NumberMath.Min`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Min(System.Single,System.Single)`.
- `NumberMath.RadToDeg`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.RadToDeg(System.Single)`.
- `NumberMath.Remap`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Remap(System.Single,System.Single,System.Single,System.Single,System.Single)`.
- `NumberMath.Round`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Round(System.Single)`.
- `NumberMath.Sign`: no XML doc member found for id `M:Assembler.Libraries.NumberMath.Sign(System.Single)`.
- `RandomMath.Chance`: no XML doc member found for id `M:Assembler.Libraries.RandomMath.Chance(System.Single)`.
- `RandomMath.Pick`: no XML doc member found for id `M:Assembler.Libraries.RandomMath.Pick(System.Collections.Generic.List{UnityEngine.Vector3})`.
- `RandomMath.PickInt`: no XML doc member found for id `M:Assembler.Libraries.RandomMath.PickInt(System.Collections.Generic.List{System.Int32})`.
- `RandomMath.RandomColor`: no XML doc member found for id `M:Assembler.Libraries.RandomMath.RandomColor`.
- `RandomMath.RandomColorBetween`: no XML doc member found for id `M:Assembler.Libraries.RandomMath.RandomColorBetween(UnityEngine.Color,UnityEngine.Color)`.
- `RandomMath.RandomFloat`: no XML doc member found for id `M:Assembler.Libraries.RandomMath.RandomFloat(System.Single,System.Single)`.
- `RandomMath.RandomInsideCircle`: no XML doc member found for id `M:Assembler.Libraries.RandomMath.RandomInsideCircle(System.Single)`.
- `RandomMath.RandomInt`: no XML doc member found for id `M:Assembler.Libraries.RandomMath.RandomInt(System.Single,System.Single)`.
- `RandomMath.RandomOnCircle`: no XML doc member found for id `M:Assembler.Libraries.RandomMath.RandomOnCircle(System.Single)`.
- `VectorMath.AddVector`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.AddVector(UnityEngine.Vector3,UnityEngine.Vector3)`.
- `VectorMath.Angle2D`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.Angle2D(UnityEngine.Vector3)`.
- `VectorMath.Direction`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.Direction(UnityEngine.Vector3,UnityEngine.Vector3)`.
- `VectorMath.Distance`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.Distance(UnityEngine.Vector3,UnityEngine.Vector3)`.
- `VectorMath.Dot`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.Dot(UnityEngine.Vector3,UnityEngine.Vector3)`.
- `VectorMath.IntegratePosition`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.IntegratePosition(UnityEngine.Vector3,UnityEngine.Vector3)`.
- `VectorMath.IntegratePosition`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.IntegratePosition(UnityEngine.Vector3,UnityEngine.Vector3,System.Single)`.
- `VectorMath.LerpVector`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.LerpVector(UnityEngine.Vector3,UnityEngine.Vector3,System.Single)`.
- `VectorMath.Magnitude`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.Magnitude(UnityEngine.Vector3)`.
- `VectorMath.Normalize`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.Normalize(UnityEngine.Vector3)`.
- `VectorMath.Rotate2D`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.Rotate2D(UnityEngine.Vector3,System.Single)`.
- `VectorMath.ScaleVector`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.ScaleVector(UnityEngine.Vector3,System.Single)`.
- `VectorMath.SubtractVector`: no XML doc member found for id `M:Assembler.Libraries.VectorMath.SubtractVector(UnityEngine.Vector3,UnityEngine.Vector3)`.

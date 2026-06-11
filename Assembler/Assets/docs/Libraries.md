# Libraries

Generated from the `Assembler.Libraries` XML doc comments. Every public static method of these classes is registered globally with the expression compiler, so descriptor expressions can call it by bare name.

## `ColorMath`
First-class colour helpers for descriptor expressions. Registered globally in
            CompiledExpressionsRegistry so every expression can call these by bare name
            (LerpColor, WithAlpha, Brighten, RgbToHsv, ...). Colours are UnityEngine.Color
            (already registered as a constructible type). HSV triples are carried as
            Vector3(h, s, v) with all components in [0, 1]. Numeric parameters are float so int
            arguments coerce automatically during overload resolution.

### `Color Brighten(Color c, float factor)`
Brighten a colour by scaling its RGB toward white (alpha preserved).

| Parameter | Type | Description |
|-----------|------|-------------|
| c | Color | The colour. |
| factor | float | Brighten amount in [0, 1]; 0 leaves the colour unchanged, 1 is white. |

**Returns** (Color): The brightened colour.

### `Color Darken(Color c, float factor)`
Darken a colour by scaling its RGB toward black (alpha preserved).

| Parameter | Type | Description |
|-----------|------|-------------|
| c | Color | The colour. |
| factor | float | Darken amount in [0, 1]; 0 leaves the colour unchanged, 1 is black. |

**Returns** (Color): The darkened colour.

### `Color Grayscale(Color c)`
A grayscale colour matching the perceived luminance of the input (alpha preserved).

| Parameter | Type | Description |
|-----------|------|-------------|
| c | Color | The colour. |

**Returns** (Color): The desaturated grayscale colour.

### `Color HsvToRgb(float h, float s, float v)`
Build an opaque colour from hue, saturation and value (each in [0, 1]).

| Parameter | Type | Description |
|-----------|------|-------------|
| h | float | Hue in [0, 1]. |
| s | float | Saturation in [0, 1]. |
| v | float | Value in [0, 1]. |

**Returns** (Color): The opaque RGB colour.

### `Color LerpColor(Color a, Color b, float t)`
Linear interpolation between two colours (t clamped to [0, 1]).

| Parameter | Type | Description |
|-----------|------|-------------|
| a | Color | Start colour (t = 0). |
| b | Color | End colour (t = 1). |
| t | float | Interpolation factor; clamped to [0, 1]. |

**Returns** (Color): The interpolated colour.

### `Vector3 RgbToHsv(Color c)`
Convert an RGB colour to HSV as Vector3(h, s, v), each in [0, 1].

| Parameter | Type | Description |
|-----------|------|-------------|
| c | Color | The colour to convert. |

**Returns** (Vector3): The hue, saturation and value packed as a Vector3.

### `Color WithAlpha(Color c, float alpha)`
The same colour with its alpha replaced.

| Parameter | Type | Description |
|-----------|------|-------------|
| c | Color | The colour. |
| alpha | float | New alpha in [0, 1]. |

**Returns** (Color): The colour with the given alpha.

## `GridMath`
First-class grid/tilemap helpers for grid-based games. Registered globally
            in CompiledExpressionsRegistry so every descriptor expression can call these
            by bare name (CellToWorld, RowFull, IsOccupied, ...). Cells are carried as
            Vector3(col, row, 0); grid dimensions/origin are passed explicitly so the
            helpers stay stateless. All numeric parameters are float so int arguments
            coerce automatically during overload resolution.

### `Vector3 CellToWorld(Vector3 cell, float originX, float originY)`
World position of a cell, given the world coordinates of cell (0,0).

| Parameter | Type | Description |
|-----------|------|-------------|
| cell | Vector3 | Cell as Vector3(col, row, 0). |
| originX | float | World x of cell (0,0). |
| originY | float | World y of cell (0,0). |

**Returns** (Vector3): The cell's world position.

### `Vector3 CellToWorld(Vector3 cell, float originX, float originY, float cellSize)`
World position of a cell with a non-unit, square cell size.

| Parameter | Type | Description |
|-----------|------|-------------|
| cell | Vector3 | Cell as Vector3(col, row, 0). |
| originX | float | World x of cell (0,0). |
| originY | float | World y of cell (0,0). |
| cellSize | float | Edge length of a single (square) cell in world units. |

**Returns** (Vector3): The cell's world position.

### `int CellsInRow(List<Vector3> occupied, float row)`
Number of occupied cells in a row.

| Parameter | Type | Description |
|-----------|------|-------------|
| occupied | List<Vector3> | The occupied cells, each as Vector3(col, row, 0). |
| row | float | Row index to count. |

**Returns** (int): The count of occupied cells in the row.

### `int FullRowCount(List<Vector3> occupied, float width, float height)`
Number of completely filled rows in [0, height-1].

| Parameter | Type | Description |
|-----------|------|-------------|
| occupied | List<Vector3> | The occupied cells, each as Vector3(col, row, 0). |
| width | float | Grid width in cells. |
| height | float | Grid height in cells. |

**Returns** (int): The count of fully filled rows.

### `int FullRowsBelow(List<Vector3> occupied, float row, float width, float height)`
Number of completely filled rows strictly below the given row.

| Parameter | Type | Description |
|-----------|------|-------------|
| occupied | List<Vector3> | The occupied cells, each as Vector3(col, row, 0). |
| row | float | Reference row; only rows with index < row are counted. |
| width | float | Grid width in cells. |
| height | float | Grid height in cells. |

**Returns** (int): The count of fully filled rows below the reference row.

### `bool InBounds(float col, float row, float width, float height)`
True when col is in [0, width-1] and row is in [0, height-1].

| Parameter | Type | Description |
|-----------|------|-------------|
| col | float | Column index. |
| row | float | Row index. |
| width | float | Grid width in cells. |
| height | float | Grid height in cells. |

**Returns** (bool): Whether the cell lies inside the grid.

### `bool InBoundsOpenTop(float col, float row, float width)`
True when col is in [0, width-1] and row >= 0, with no ceiling (pieces may spawn/extend above the top of a well).

| Parameter | Type | Description |
|-----------|------|-------------|
| col | float | Column index. |
| row | float | Row index. |
| width | float | Grid width in cells. |

**Returns** (bool): Whether the cell is in horizontal bounds and at or above row 0.

### `bool IsOccupied(List<Vector3> occupied, float col, float row)`
True if any occupied cell sits at (col, row).

| Parameter | Type | Description |
|-----------|------|-------------|
| occupied | List<Vector3> | The occupied cells, each as Vector3(col, row, 0). |
| col | float | Column index to test. |
| row | float | Row index to test. |

**Returns** (bool): Whether the cell is occupied.

### `Vector3 NeighbourCell(Vector3 cell, float dCol, float dRow)`
Cell offset from a cell by (dCol, dRow).

| Parameter | Type | Description |
|-----------|------|-------------|
| cell | Vector3 | Cell as Vector3(col, row, 0). |
| dCol | float | Column delta. |
| dRow | float | Row delta. |

**Returns** (Vector3): The neighbouring cell.

### `Vector3 RotateCellCW(Vector3 cell, int times)`
Rotate a cell offset clockwise about the origin, times quarter-turns ((x, y) -> (y, -x) per turn). Four turns is the identity.

| Parameter | Type | Description |
|-----------|------|-------------|
| cell | Vector3 | Cell offset as Vector3(col, row, 0). |
| times | int | Number of clockwise quarter-turns (any int; reduced mod 4). |

**Returns** (Vector3): The rotated cell offset.

### `bool RowFull(List<Vector3> occupied, float row, float width)`
True if a row is completely filled.

| Parameter | Type | Description |
|-----------|------|-------------|
| occupied | List<Vector3> | The occupied cells, each as Vector3(col, row, 0). |
| row | float | Row index to test. |
| width | float | Grid width in cells. |

**Returns** (bool): Whether every cell in the row is occupied.

### `Vector3 WorldToCell(Vector3 world, float originX, float originY)`
Inverse of the unit-cell CellToWorld.

| Parameter | Type | Description |
|-----------|------|-------------|
| world | Vector3 | A world position. |
| originX | float | World x of cell (0,0). |
| originY | float | World y of cell (0,0). |

**Returns** (Vector3): The cell as Vector3(col, row, 0).

## `HexMath`
First-class hex-grid helpers, the hexagonal companion to GridMath. Hex cells are
            carried as axial coordinates Vector3(q, r, 0); world conversions take a hex "size"
            (centre-to-corner radius). Registered globally in CompiledExpressionsRegistry so
            every descriptor expression can call these by bare name (HexToWorldPointy,
            HexDistance, HexNeighbour, ...). Direction indices run 0..5; for the standard axial
            layout they are +q, +q-r, -r, -q, -q+r, +r. All numeric parameters are float so int
            arguments coerce automatically during overload resolution.

### `float HexDistance(Vector3 a, Vector3 b)`
Number of steps between two hex cells (hex/cube distance).

| Parameter | Type | Description |
|-----------|------|-------------|
| a | Vector3 | First hex as axial Vector3(q, r, 0). |
| b | Vector3 | Second hex as axial Vector3(q, r, 0). |

**Returns** (float): The minimum number of single-step moves between the cells.

### `Vector3 HexNeighbour(Vector3 hex, int directionIndex)`
The neighbouring hex in one of the six directions (index reduced mod 6).

| Parameter | Type | Description |
|-----------|------|-------------|
| hex | Vector3 | Hex cell as axial Vector3(q, r, 0). |
| directionIndex | int | Direction 0..5 (any int; reduced mod 6). |

**Returns** (Vector3): The neighbouring hex cell.

### `List<Vector3> HexNeighbours(Vector3 hex)`
All six neighbours of a hex cell, in direction order 0..5.

| Parameter | Type | Description |
|-----------|------|-------------|
| hex | Vector3 | Hex cell as axial Vector3(q, r, 0). |

**Returns** (List<Vector3>): A list of the six neighbouring hex cells.

### `Vector3 HexRound(Vector3 hex)`
Round a fractional axial hex to the nearest integer hex cell (cube rounding).

| Parameter | Type | Description |
|-----------|------|-------------|
| hex | Vector3 | Fractional hex as axial Vector3(q, r, 0). |

**Returns** (Vector3): The nearest valid hex cell as axial Vector3(q, r, 0).

### `Vector3 HexToWorldFlat(Vector3 hex, float size)`
World position of a flat-top hex cell (flat sides top/bottom).

| Parameter | Type | Description |
|-----------|------|-------------|
| hex | Vector3 | Hex cell as axial Vector3(q, r, 0). |
| size | float | Centre-to-corner radius of a hex in world units. |

**Returns** (Vector3): The hex centre's world position (z = 0).

### `Vector3 HexToWorldPointy(Vector3 hex, float size)`
World position of a pointy-top hex cell (flat sides left/right).

| Parameter | Type | Description |
|-----------|------|-------------|
| hex | Vector3 | Hex cell as axial Vector3(q, r, 0). |
| size | float | Centre-to-corner radius of a hex in world units. |

**Returns** (Vector3): The hex centre's world position (z = 0).

## `LayoutMath`
Regular-layout helpers that each return a List<Vector3> of world positions, for use as a
            Placements At source (e.g. !expr { Do: 'GridPositions(19, 21, 0.5f, origin)', ReturnType:
            vector list }). Registered globally in CompiledExpressionsRegistry so every descriptor expression
            can call these by bare name (GridPositions, LinePositions, RingPositions). For irregular layouts
            (a grid with holes) build the list imperatively with PositionList instead. All numeric
            parameters are float so int arguments coerce automatically during overload resolution.

### `List<Vector3> GridPositions(float cols, float rows, float cellSize, Vector3 origin)`
A row-major grid of cell-centre world positions, starting at origin.

| Parameter | Type | Description |
|-----------|------|-------------|
| cols | float | Number of columns (cast to int; clamped at 0). |
| rows | float | Number of rows (cast to int; clamped at 0). |
| cellSize | float | Spacing between adjacent cells in world units. |
| origin | Vector3 | World position of cell (0, 0). |

**Returns** (List<Vector3>): cols × rows positions, ordered row by row (all of row 0, then row 1, …).

### `List<Vector3> LinePositions(Vector3 start, Vector3 end, int count)`
Evenly spaced positions along the segment from start to end.

| Parameter | Type | Description |
|-----------|------|-------------|
| start | Vector3 | First position (always included). |
| end | Vector3 | Last position (included when count > 1). |
| count | int | Number of positions (clamped at 0; a count of 1 yields just start). |

**Returns** (List<Vector3>): The count positions, endpoints inclusive.

### `List<Vector3> RingPositions(Vector3 center, float radius, int count)`
Positions evenly spaced around a circle, the first at angle 0 (along +x).

| Parameter | Type | Description |
|-----------|------|-------------|
| center | Vector3 | Centre of the ring. |
| radius | float | Ring radius in world units. |
| count | int | Number of positions, spaced 360/count degrees apart (clamped at 0). |

**Returns** (List<Vector3>): The count positions, counter-clockwise from +x, on the z = center.z plane.

## `NumberMath`
First-class scalar math helpers for descriptor expressions. Registered globally in
            CompiledExpressionsRegistry so every expression can call these by bare name (Clamp,
            Lerp, Remap, DegToRad, ...). All numeric parameters are float so int arguments coerce
            automatically during overload resolution. Names are scalar-specific (Lerp here,
            LerpVector in VectorMath, LerpColor in ColorMath) to keep the shared bare-name space
            unambiguous.

### `float Abs(float x)`
Absolute value.

| Parameter | Type | Description |
|-----------|------|-------------|
| x | float | The value. |

**Returns** (float): The magnitude of x with the sign removed.

### `bool Approx(float a, float b)`
True when two values are equal within floating-point tolerance.

| Parameter | Type | Description |
|-----------|------|-------------|
| a | float | First value. |
| b | float | Second value. |

**Returns** (bool): Whether a and b are approximately equal.

### `float Ceil(float x)`
Smallest whole number greater than or equal to a value.

| Parameter | Type | Description |
|-----------|------|-------------|
| x | float | The value. |

**Returns** (float): The ceiling of x.

### `float Clamp(float x, float min, float max)`
Constrain a value to the inclusive range [min, max].

| Parameter | Type | Description |
|-----------|------|-------------|
| x | float | The value to clamp. |
| min | float | Lower bound. |
| max | float | Upper bound. |

**Returns** (float): The clamped value.

### `float Clamp01(float x)`
Constrain a value to [0, 1].

| Parameter | Type | Description |
|-----------|------|-------------|
| x | float | The value to clamp. |

**Returns** (float): The value clamped to [0, 1].

### `float DegToRad(float degrees)`
Convert degrees to radians.

| Parameter | Type | Description |
|-----------|------|-------------|
| degrees | float | An angle in degrees. |

**Returns** (float): The angle in radians.

### `float Floor(float x)`
Largest whole number less than or equal to a value.

| Parameter | Type | Description |
|-----------|------|-------------|
| x | float | The value. |

**Returns** (float): The floor of x.

### `float Lerp(float a, float b, float t)`
Linear interpolation between two values (t clamped to [0, 1]).

| Parameter | Type | Description |
|-----------|------|-------------|
| a | float | Start value (t = 0). |
| b | float | End value (t = 1). |
| t | float | Interpolation factor; clamped to [0, 1]. |

**Returns** (float): The interpolated value.

### `float Max(float a, float b)`
The larger of two values.

| Parameter | Type | Description |
|-----------|------|-------------|
| a | float | First value. |
| b | float | Second value. |

**Returns** (float): The maximum of a and b.

### `float Min(float a, float b)`
The smaller of two values.

| Parameter | Type | Description |
|-----------|------|-------------|
| a | float | First value. |
| b | float | Second value. |

**Returns** (float): The minimum of a and b.

### `float RadToDeg(float radians)`
Convert radians to degrees.

| Parameter | Type | Description |
|-----------|------|-------------|
| radians | float | An angle in radians. |

**Returns** (float): The angle in degrees.

### `float Remap(float x, float inMin, float inMax, float outMin, float outMax)`
Re-map a value from one range to another (linear). A value at inMin maps to outMin and a value at inMax maps to outMax; values outside [inMin, inMax] are extrapolated.

| Parameter | Type | Description |
|-----------|------|-------------|
| x | float | The value to re-map. |
| inMin | float | Lower bound of the input range. |
| inMax | float | Upper bound of the input range. |
| outMin | float | Lower bound of the output range. |
| outMax | float | Upper bound of the output range. |

**Returns** (float): The re-mapped value.

### `float Round(float x)`
Round to the nearest whole number (banker's rounding at .5).

| Parameter | Type | Description |
|-----------|------|-------------|
| x | float | The value to round. |

**Returns** (float): The nearest integral value.

### `float Sign(float x)`
Sign of a value: -1, 0, or 1.

| Parameter | Type | Description |
|-----------|------|-------------|
| x | float | The value. |

**Returns** (float): -1 if negative, 1 if positive, 0 if zero.

## `PositionList`
A constructible builder for an irregular list of world positions — the imperative counterpart to
            LayoutMath's regular-layout helpers. Use it from a Placements At expression when the
            positions don't follow a closed-form pattern (e.g. a Pacman maze where pills skip wall cells):
            new PositionList(); … b.Add(p); return b.ToList();. The expression compiler supports
            new PositionList(...) and instance calls but not collection initializers, which is why a
            .Add-in-a-loop builder is needed. Registered globally in CompiledExpressionsRegistry so every
            descriptor expression can construct it by bare name.

_No public static methods._

## `RandomMath`
First-class randomness helpers for descriptor expressions, wrapping
            UnityEngine.Random. Registered globally in CompiledExpressionsRegistry so every
            expression can call these by bare name (RandomFloat, RandomOnCircle, RandomColor,
            ...). All numeric parameters are float so int arguments coerce automatically during
            overload resolution. Lists are carried as List<T>, matching GridMath.

### `bool Chance(float probability)`
True with the given probability.

| Parameter | Type | Description |
|-----------|------|-------------|
| probability | float | Chance of returning true, in [0, 1]. |

**Returns** (bool): A random boolean weighted by probability.

### `Vector3 Pick(List<Vector3> items)`
A random element from a list of vectors.

| Parameter | Type | Description |
|-----------|------|-------------|
| items | List<Vector3> | The list to pick from (must be non-empty). |

**Returns** (Vector3): A uniformly random element.

### `int PickInt(List<int> items)`
A random element from a list of integers.

| Parameter | Type | Description |
|-----------|------|-------------|
| items | List<int> | The list to pick from (must be non-empty). |

**Returns** (int): A uniformly random element.

### `Color RandomColor()`
A random fully-opaque RGB colour.

**Returns** (Color): A random opaque Color.

### `Color RandomColorBetween(Color a, Color b)`
A random opaque colour with each channel between the matching channels of two colours.

| Parameter | Type | Description |
|-----------|------|-------------|
| a | Color | One end of the per-channel range. |
| b | Color | The other end of the per-channel range. |

**Returns** (Color): A random opaque Color blended per channel between a and b.

### `float RandomFloat(float min, float max)`
A random float in the inclusive range [min, max].

| Parameter | Type | Description |
|-----------|------|-------------|
| min | float | Lower bound (inclusive). |
| max | float | Upper bound (inclusive). |

**Returns** (float): A uniformly random float in the range.

### `Vector3 RandomInsideCircle(float radius)`
A random point inside a disc of the given radius (z = 0).

| Parameter | Type | Description |
|-----------|------|-------------|
| radius | float | The disc radius. |

**Returns** (Vector3): A random Vector3 inside the disc, in the XY plane.

### `int RandomInt(float minInclusive, float maxInclusive)`
A random integer in the inclusive range [minInclusive, maxInclusive].

| Parameter | Type | Description |
|-----------|------|-------------|
| minInclusive | float | Lower bound (inclusive). |
| maxInclusive | float | Upper bound (inclusive). |

**Returns** (int): A uniformly random integer in the range.

### `Vector3 RandomOnCircle(float radius)`
A random point on the circumference of a circle of the given radius (z = 0).

| Parameter | Type | Description |
|-----------|------|-------------|
| radius | float | The circle radius. |

**Returns** (Vector3): A random Vector3 on the circle, in the XY plane.

### `int WeightedPickIndex(List<float> weights)`
An index in [0, weights.Count) chosen with probability proportional to each weight.

| Parameter | Type | Description |
|-----------|------|-------------|
| weights | List<float> | Per-item weights; negatives are clamped to 0. An all-zero list falls back to a uniform pick. |

**Returns** (int): A weighted-random index into the list (must be non-empty).

## `RecordHelper`
Cast-free helpers for reading and writing Record fields from descriptor expressions.
            Registered globally in CompiledExpressionsRegistry so every expression can call these by bare name
            (GetInt, SetInt, HasField, …). They are sugar over the Record indexer: the getters
            avoid the (int)item["count"] cast the indexer otherwise forces, and the setters mutate the
            record in place and return it so they chain and can carry a side effect through a value-setter's
            Value slot. Both styles interoperate — they read and write the same underlying field bag.

### `bool GetBool(Record record, string field)`
Reads a bool field.

| Parameter | Type | Description |
|-----------|------|-------------|
| record | Record | The record to read from. |
| field | string | The field name. |

**Returns** (bool): The field's value as a bool.

### `float GetFloat(Record record, string field)`
Reads a float field, widening a boxed int.

| Parameter | Type | Description |
|-----------|------|-------------|
| record | Record | The record to read from. |
| field | string | The field name. |

**Returns** (float): The field's value as a float.

### `int GetInt(Record record, string field)`
Reads an int field.

| Parameter | Type | Description |
|-----------|------|-------------|
| record | Record | The record to read from. |
| field | string | The field name. |

**Returns** (int): The field's value as an int.

### `string GetString(Record record, string field)`
Reads a string field.

| Parameter | Type | Description |
|-----------|------|-------------|
| record | Record | The record to read from. |
| field | string | The field name. |

**Returns** (string): The field's value as a string (never null).

### `bool HasField(Record record, string field)`
True when the named field is present on the record.

| Parameter | Type | Description |
|-----------|------|-------------|
| record | Record | The record to test. |
| field | string | The field name. |

**Returns** (bool): Whether the field exists.

### `Record SetBool(Record record, string field, bool value)`
Writes a bool field in place and returns the record so calls can chain.

| Parameter | Type | Description |
|-----------|------|-------------|
| record | Record | The record to mutate. |
| field | string | The field name. |
| value | bool | The value to store. |

**Returns** (Record): The same record instance.

### `Record SetFloat(Record record, string field, float value)`
Writes a float field in place and returns the record so calls can chain.

| Parameter | Type | Description |
|-----------|------|-------------|
| record | Record | The record to mutate. |
| field | string | The field name. |
| value | float | The value to store. |

**Returns** (Record): The same record instance.

### `Record SetInt(Record record, string field, int value)`
Writes an int field in place and returns the record so calls can chain.

| Parameter | Type | Description |
|-----------|------|-------------|
| record | Record | The record to mutate. |
| field | string | The field name. |
| value | int | The value to store. |

**Returns** (Record): The same record instance.

### `Record SetString(Record record, string field, string value)`
Writes a string field in place and returns the record so calls can chain.

| Parameter | Type | Description |
|-----------|------|-------------|
| record | Record | The record to mutate. |
| field | string | The field name. |
| value | string | The value to store. |

**Returns** (Record): The same record instance.

## `SteeringMath`
First-class steering helpers for descriptor expressions, registered globally in
            CompiledExpressionsRegistry so every expression can call them by bare name (Seek, Flee, Arrive, …). Each
            movement function returns a desired-velocity Vector3, so they compose inside a velocity: !expr
            (or feed the steering aggregator behaviour). Positions/velocities are carried as Vector3
            (z = 0 for 2D), matching VectorMath; all numeric parameters are float so int arguments coerce
            automatically. Wander draws on the global RNG and is therefore non-deterministic, like RandomMath.

### `Vector3 Alignment(Vector3 velocity, List<Vector3> neighbourVelocities, float maxSpeed)`
Steer to match the average heading of nearby neighbours, for flock alignment.

| Parameter | Type | Description |
|-----------|------|-------------|
| velocity | Vector3 | Current velocity (unused, kept for signature symmetry with the other rules). |
| neighbourVelocities | List<Vector3> | Velocities of nearby entities. |
| maxSpeed | float | Speed of the returned velocity (units per second). |

**Returns** (Vector3): A velocity matching the neighbours' average heading, or zero if there are none (or they cancel out).

### `Vector3 Arrive(Vector3 position, Vector3 target, float maxSpeed, float slowingRadius)`
Like Single) but eases to a stop, scaling speed down inside the slowing radius.

| Parameter | Type | Description |
|-----------|------|-------------|
| position | Vector3 | Current position. |
| target | Vector3 | Point to arrive at. |
| maxSpeed | float | Maximum speed (units per second). |
| slowingRadius | float | Distance from the target at which to begin slowing. |

**Returns** (Vector3): A velocity toward the target, ramped to zero as the target is reached.

### `Vector3 AvoidObstacles(Vector3 position, Vector3 velocity, List<Vector3> obstacles, float lookAhead, float avoidRadius, float maxSpeed)`
Steer away from obstacles that lie ahead within a look-ahead distance, for collision avoidance. Only obstacles in front of the current heading and inside the swept corridor threaten; the nearest such obstacle produces a lateral swerve (away from it) plus a braking component, ramping up as it closes. Complements Single) (which repels in every direction) by reacting only to what is actually in the agent's path.

| Parameter | Type | Description |
|-----------|------|-------------|
| position | Vector3 | Current position. |
| velocity | Vector3 | Current velocity; its direction is the heading scanned for obstacles. |
| obstacles | List<Vector3> | Positions of obstacles to dodge. |
| lookAhead | float | How far ahead along the heading to scan; obstacles beyond this are ignored. |
| avoidRadius | float | Corridor half-width: obstacles further than this off the heading line clear the agent. |
| maxSpeed | float | Speed of the returned velocity at full imminence (units per second). |

**Returns** (Vector3): A swerve-and-brake velocity away from the nearest threatening obstacle, or zero if none threaten.

### `Vector3 Cohesion(Vector3 position, List<Vector3> neighbours, float maxSpeed)`
Steer toward the average position (centre of mass) of nearby neighbours, for flock cohesion.

| Parameter | Type | Description |
|-----------|------|-------------|
| position | Vector3 | Current position. |
| neighbours | List<Vector3> | Positions of nearby entities. |
| maxSpeed | float | Speed of the returned velocity (units per second). |

**Returns** (Vector3): A velocity steering toward the neighbours' centroid, or zero if there are none.

### `Vector3 Evade(Vector3 position, Vector3 target, Vector3 targetVelocity, float maxSpeed)`
Flee the target's predicted future position, dodging a moving threat.

| Parameter | Type | Description |
|-----------|------|-------------|
| position | Vector3 | Current position. |
| target | Vector3 | Threat's current position. |
| targetVelocity | Vector3 | Threat's current velocity. |
| maxSpeed | float | Maximum speed (units per second). |

**Returns** (Vector3): A velocity away from where the threat is heading.

### `Vector3 Flee(Vector3 position, Vector3 target, float maxSpeed)`
Desired velocity that drives straight away from a target at full speed.

| Parameter | Type | Description |
|-----------|------|-------------|
| position | Vector3 | Current position. |
| target | Vector3 | Point to steer away from. |
| maxSpeed | float | Maximum speed (units per second). |

**Returns** (Vector3): A velocity of length maxSpeed pointing away from the target.

### `float Heading2D(Vector3 from, Vector3 to)`
Heading angle from one point toward another, in degrees CCW from +x, in [-180, 180].

| Parameter | Type | Description |
|-----------|------|-------------|
| from | Vector3 | The origin point. |
| to | Vector3 | The point to face. |

**Returns** (float): The 2D heading angle in degrees.

### `Vector3 LookRotation2D(Vector3 from, Vector3 to)`
Euler rotation that faces from one point toward another in the XY plane (rotation about z).

| Parameter | Type | Description |
|-----------|------|-------------|
| from | Vector3 | The origin point. |
| to | Vector3 | The point to aim at. |

**Returns** (Vector3): An euler-angles vector (0, 0, heading) suitable for an entity's Rotation.

### `Vector3 Pursue(Vector3 position, Vector3 target, Vector3 targetVelocity, float maxSpeed)`
Seek the target's predicted future position, leading a moving target.

| Parameter | Type | Description |
|-----------|------|-------------|
| position | Vector3 | Current position. |
| target | Vector3 | Target's current position. |
| targetVelocity | Vector3 | Target's current velocity. |
| maxSpeed | float | Maximum speed (units per second). |

**Returns** (Vector3): A velocity toward where the target is heading.

### `Vector3 Seek(Vector3 position, Vector3 target, float maxSpeed)`
Desired velocity that drives straight toward a target at full speed.

| Parameter | Type | Description |
|-----------|------|-------------|
| position | Vector3 | Current position. |
| target | Vector3 | Point to steer toward. |
| maxSpeed | float | Maximum speed (units per second). |

**Returns** (Vector3): A velocity of length maxSpeed pointing at the target (zero if already there).

### `Vector3 Separate(Vector3 position, List<Vector3> neighbours, float separationRadius, float maxSpeed)`
Repulsion velocity that pushes away from nearby neighbours, for flock/crowd separation.

| Parameter | Type | Description |
|-----------|------|-------------|
| position | Vector3 | Current position. |
| neighbours | List<Vector3> | Positions of nearby entities. |
| separationRadius | float | Only neighbours within this distance contribute. |
| maxSpeed | float | Speed of the returned velocity (units per second). |

**Returns** (Vector3): A velocity steering away from crowding neighbours, or zero if none are close.

### `Vector3 Wander(Vector3 velocity, float maxSpeed, float jitterDegrees)`
Nudge the current heading by a random jitter, for aimless roaming. Non-deterministic.

| Parameter | Type | Description |
|-----------|------|-------------|
| velocity | Vector3 | Current velocity (its direction is the base heading). |
| maxSpeed | float | Speed of the returned velocity (units per second). |
| jitterDegrees | float | Maximum turn this step, in degrees either way. |

**Returns** (Vector3): A velocity of length maxSpeed turned by a random amount.

## `VectorMath`
First-class 2D/3D vector helpers for descriptor expressions. Registered globally
            in CompiledExpressionsRegistry so every expression can call these by bare name
            (ScaleVector, Distance, Rotate2D, IntegratePosition, ...). Vectors are carried as
            Vector3 (z = 0 for 2D), matching GridMath. All numeric parameters are float so int
            arguments coerce automatically during overload resolution. Method names are chosen
            to avoid colliding with the scalar NumberMath helpers (e.g. LerpVector, not Lerp).

### `Vector3 AddVector(Vector3 a, Vector3 b)`
Add two vectors.

| Parameter | Type | Description |
|-----------|------|-------------|
| a | Vector3 | First vector. |
| b | Vector3 | Second vector. |

**Returns** (Vector3): The component-wise sum a + b.

### `float Angle2D(Vector3 v)`
Angle of a 2D vector in degrees, measured CCW from the +x axis, in [-180, 180].

| Parameter | Type | Description |
|-----------|------|-------------|
| v | Vector3 | The vector (x, y used). |

**Returns** (float): The heading angle in degrees.

### `Vector3 Direction(Vector3 from, Vector3 to)`
Unit vector pointing from one point toward another.

| Parameter | Type | Description |
|-----------|------|-------------|
| from | Vector3 | The starting point. |
| to | Vector3 | The target point. |

**Returns** (Vector3): The normalized direction from from to to.

### `float Distance(Vector3 a, Vector3 b)`
Straight-line distance between two points.

| Parameter | Type | Description |
|-----------|------|-------------|
| a | Vector3 | First point. |
| b | Vector3 | Second point. |

**Returns** (float): The Euclidean distance between a and b.

### `float Dot(Vector3 a, Vector3 b)`
Dot product of two vectors.

| Parameter | Type | Description |
|-----------|------|-------------|
| a | Vector3 | First vector. |
| b | Vector3 | Second vector. |

**Returns** (float): The dot product a · b.

### `Vector3 IntegratePosition(Vector3 pos, Vector3 vel, float dt)`
Advance a position by a velocity over an explicit time step.

| Parameter | Type | Description |
|-----------|------|-------------|
| pos | Vector3 | Current position. |
| vel | Vector3 | Velocity (units per second). |
| dt | float | Time step in seconds. |

**Returns** (Vector3): The new position pos + vel * dt.

### `Vector3 LerpVector(Vector3 a, Vector3 b, float t)`
Linear interpolation between two vectors (t clamped to [0, 1]).

| Parameter | Type | Description |
|-----------|------|-------------|
| a | Vector3 | Start vector (t = 0). |
| b | Vector3 | End vector (t = 1). |
| t | float | Interpolation factor; clamped to [0, 1]. |

**Returns** (Vector3): The interpolated vector.

### `float Magnitude(Vector3 v)`
Euclidean length of a vector.

| Parameter | Type | Description |
|-----------|------|-------------|
| v | Vector3 | The vector. |

**Returns** (float): The magnitude (length) of the vector.

### `Vector3 Normalize(Vector3 v)`
A unit-length vector in the same direction (zero vector returns zero).

| Parameter | Type | Description |
|-----------|------|-------------|
| v | Vector3 | The vector to normalize. |

**Returns** (Vector3): The normalized vector.

### `Vector3 Rotate2D(Vector3 v, float degrees)`
Rotate a 2D vector (x, y) counter-clockwise about the origin by an angle in degrees. The z component is preserved. Replaces hand-rolled cos/sin rotation matrices in descriptor expressions.

| Parameter | Type | Description |
|-----------|------|-------------|
| v | Vector3 | The vector to rotate (x, y used; z preserved). |
| degrees | float | Counter-clockwise rotation angle in degrees. |

**Returns** (Vector3): The rotated vector.

### `Vector3 ScaleVector(Vector3 v, float k)`
Multiply a vector by a scalar.

| Parameter | Type | Description |
|-----------|------|-------------|
| v | Vector3 | The vector. |
| k | float | The scalar factor. |

**Returns** (Vector3): The scaled vector.

### `Vector3 SubtractVector(Vector3 a, Vector3 b)`
Subtract one vector from another.

| Parameter | Type | Description |
|-----------|------|-------------|
| a | Vector3 | The minuend. |
| b | Vector3 | The subtrahend. |

**Returns** (Vector3): The component-wise difference a - b.


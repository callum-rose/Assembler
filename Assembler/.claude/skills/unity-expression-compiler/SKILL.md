---
name: unity-expression-compiler
description: >
  Use this skill whenever generating code for the Unity Expression Compiler system. Trigger this skill
  when the user asks to write scripts, expressions, or logic for this compiler, when they mention
  ExpressionMethodCompiler, or when the context involves writing code for the custom assembler system.
  Even if the user just says "write me some compiler code" or "create a script for the assembler", use
  this skill — getting the syntax wrong will cause runtime errors.
---

# Unity Expression Compiler — Expression Writing Guide

You are writing a **method body** in a procedural subset of C#. Write it as if you're inside a method —
no class, namespace, or `using` directives. Just statements and a `return`.

> **Critical**: This is NOT full C#. Many common features are unsupported and will cause parse errors.
> Follow this guide exactly.

---

## Formatting Rules

- **Every statement ends with a semicolon.**
- **Every semicolon must be followed by a newline.** Never place another statement on the same line after `;`.
- **All blocks (if, for, while, methods) must use braces `{ }`**, even for single statements.
- Opening brace `{` goes on its own line.

```csharp
// ✅ Correct
int x = 5;
int y = x + 1;
return y;

// ❌ Wrong — two statements on one line
int x = 5; int y = x + 1;
```

---

## Supported Types

```csharp
int x = 5;
float y = 3.14f;
double z = 3.14;
bool flag = true;
string text = "Hello";
var auto = 10;        // type inferred from initializer
```

Use `f` suffix for float literals: `3.14f`, not `3.14`.

> **Implicit numeric promotion.** Mixing numeric types in a binary op widens the narrower
> operand up the `int → long → float → double` ladder, mirroring C# (e.g. `float + int` yields
> `float`, `int < float` compares as `float`). This applies to `+ - * / %`, the comparisons
> `< > <= >= == !=`, and compound assignments `+= -= *= /=` (where the result is narrowed back
> to the target's type, like C#). Explicit casts are no longer required for mixed-type math, but
> remain available when you want a specific type.

---

## Operators

```csharp
// Arithmetic
a + b   a - b   a * b   a / b   a % b   -a

// Comparison
a == b   a != b   a < b   a > b   a <= b   a >= b

// Logical
a && b   a || b   !a

// Ternary
condition ? trueValue : falseValue

// Cast
(int)3.14    (float)n    (double)x

// Compound assignment
x += 5;
x -= 3;
x *= 2;
x /= 4;

// Increment / decrement
x++;
x--;
```

---

## Control Flow

```csharp
if (condition)
{
    // ...
}
else if (other)
{
    // ...
}
else
{
    // ...
}

for (int i = 0; i < n; i++)
{
    // ...
}

while (condition)
{
    // ...
}

break;
continue;
return value;
```

Early returns are supported:
```csharp
if (input < 0.0f)
{
    return 0.0f;
}
return input * 2.0f;
```

---

## Local Methods

Define helper methods before using them. They follow the same formatting rules.

```csharp
int square(int x)
{
    return x * x;
}

int result = square(5);
return result;
```

---

## Member Access

```csharp
float x = vector.x;
vector.x = 10.0;
transform.position.x = 100.0;
string s = sb.ToString();
```

---

## Single-Parameter Lambdas

Only single-parameter, expression-body lambdas are supported:

```csharp
x => x * 2
x => x > 5
item => item.value
```

---

## LINQ

```csharp
var filtered = list.Where(x => x > 0);
var mapped = list.Select(x => x * 2);
var total = list.Sum();
var count = list.Count();
var first = list.First();
var chained = list.Where(x => x > 0).Select(x => x * x).Sum();
```

---

## Object Construction

Types must be available in the context where the expression is used:

```csharp
var v = new Vector3(1.0, 2.0, 3.0);
v.x = v.x * scale;
return v;
```

---

## Library helpers — callable by bare name

A set of reusable static helper functions is registered **globally** with the compiler, so you
can call them by bare name from any expression — **no `RegisterTypes` / `RegisterTypeStatics`
needed**. Prefer them over hand-rolling math or registering `UnityEngine.Mathf` /
`UnityEngine.Random`; they are clearer and remove boilerplate.

The full, authoritative list (every signature + description) is in
[`Assets/docs/Libraries.md`](../../../Assets/docs/Libraries.md), generated from the library
source by the Unity menu **`Assembler > Generate Library Docs`**. Read it before writing
math-heavy expressions. If that file is missing, generate it (or read the source under
`Assets/Libraries/`: `GridMath`, `VectorMath`, `NumberMath`, `RandomMath`, `ColorMath`, `HexMath`).

Representative helpers (not exhaustive — consult the doc for the complete set):

```csharp
// Grid          (GridMath)   CellToWorld(cell, ox, oy)  InBounds(col,row,w,h)  NeighbourCell(cell,dCol,dRow)  RotateCellCW(cell,times)
// Vectors       (VectorMath) ScaleVector(v,k)  Magnitude(v)  Normalize(v)  Distance(a,b)  Rotate2D(v,degrees)  IntegratePosition(pos,vel)
// Scalars       (NumberMath) Clamp(x,lo,hi)  Lerp(a,b,t)  Min(a,b)  Max(a,b)  Remap(x,inMin,inMax,outMin,outMax)  Round(x)
// Randomness    (RandomMath) RandomFloat(min,max)  RandomInt(minInc,maxInc)  RandomOnCircle(radius)  RandomColor()  Chance(p)
// Colour        (ColorMath)  LerpColor(a,b,t)  WithAlpha(c,a)  Brighten(c,f)  RgbToHsv(c)  HsvToRgb(h,s,v)
// Hex grids     (HexMath)    HexToWorldPointy(hex,size)  HexDistance(a,b)  HexNeighbour(hex,dir)
```

All numeric parameters are `float`, so passing `int` literals or arguments is fine (they coerce
automatically). Names are disambiguated across libraries so calls stay unambiguous: scalar `Lerp`
vs vector `LerpVector` vs colour `LerpColor`.

---

## Comments

```csharp
// Single-line comments only
```

---

## Not Supported — Will Cause Errors

| Category | Not Supported |
|---|---|
| **Syntax** | String interpolation `$"..."`, null coalescing `??`, `?.`, pattern matching, tuples |
| **Types** | Nullable `int?`, `string?` |
| **Loops** | `foreach`, `do-while` |
| **Statements** | `switch`, `try/catch`, `throw`, `using`, `lock` |
| **Lambdas** | Multi-param `(x, y) => ...`, zero-param `() => ...`, statement bodies `x => { ... }`, typed params `(int x) => ...` |
| **Collections** | Array/collection/dictionary initializers `{ 1, 2, 3 }`, index access `arr[0]` |
| **Operators** | Bitwise `&` `\|` `^` `~` `<<` `>>`, range `..`, `typeof`, `is`, `as`, `default` |
| **OOP** | Classes, structs, interfaces, events, generics (definition) |
| **Misc** | `async/await`, `ref/out/in` params, `params`, named args, `this`, `base`, `static`, LINQ query syntax |
| **Comments** | Multi-line `/* */`, XML doc `///` |

---

## Complete Example

```csharp
float clamp(float v, float lo, float hi)
{
    return v < lo ? lo : (v > hi ? hi : v);
}

int count = 0;
double sum = 0.0;

for (int i = 1; i <= n; i++)
{
    if (i % 2 == 0)
    {
        count++;
        sum += i;
    }
    else
    {
        sum -= i;
    }
}

float normalized = clamp((float)sum, 0.0f, 1.0f);
int category = count > 10 ? 2 : (count > 5 ? 1 : 0);

return normalized + category;
```

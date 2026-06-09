# C# Expression Compiler - Syntax Reference

This document describes the **subset of C#** supported by this expression compiler. This is intended for AI code generation - only the constructs listed here are valid.

---

## Table of Contents
1. [Basic Types](#basic-types)
2. [Literals](#literals)
3. [Variables](#variables)
4. [Operators](#operators)
5. [Control Flow](#control-flow)
6. [Methods](#methods)
7. [Object Construction](#object-construction)
8. [Member Access](#member-access)
9. [Lambda Expressions](#lambda-expressions)
10. [LINQ Operations](#linq-operations)
11. [Comments](#comments)
12. [What is NOT Supported](#what-is-not-supported)

---

## Basic Types

The following primitive types are supported:

- `int` - 32-bit signed integer
- `float` - single-precision floating point
- `double` - double-precision floating point  
- `bool` - boolean (true/false)
- `string` - text strings
- `var` - type inference (inferred from initializer expression)
- `void` - return type for methods with no return value

Examples:
```csharp
int x = 5;
float y = 3.14f;
double z = 3.14;
bool flag = true;
string text = "Hello";
var auto = 10;  // inferred as int
```

---

## Literals

### Numeric Literals
```csharp
int a = 42;           // integer
int b = -10;          // negative integer
double c = 3.14;      // floating point
double d = -2.5;      // negative floating point
```

### String Literals
```csharp
string text = "Hello World";
string empty = "";
string escaped = "Line 1\nLine 2";  // escape sequences supported
```

### Boolean Literals
```csharp
bool t = true;
bool f = false;
```

---

## Variables

### Declaration
```csharp
int x;              // declare without initialization
int y = 10;         // declare with initialization
var z = 20;         // type inferred from initializer
Vector3 dir = new Vector3(0, 1, 0);          // registered type name as the declared type
UnityEngine.Vector3 v = new Vector3(1, 2, 3); // fully-qualified type name also works
```

The declared type can be `var`, a built-in keyword (`int`, `float`, `double`, `bool`, `string`), or
any **registered/resolvable type** (e.g. a type listed in a descriptor's `RegisterTypes`) — used either
by simple name or fully qualified, exactly as in `new` expressions.

### Assignment
```csharp
x = 5;              // simple assignment
x = y + 10;         // assignment with expression
```

### Compound Assignment
```csharp
x += 5;             // add and assign
x -= 3;             // subtract and assign
x *= 2;             // multiply and assign
x /= 4;             // divide and assign
```

### Increment/Decrement
```csharp
x++;                // pre-increment
x--;                // pre-decrement
```

---

## Operators

### Arithmetic Operators
```csharp
a + b               // addition
a - b               // subtraction
a * b               // multiplication
a / b               // division
a % b               // modulo (remainder)
-a                  // negation (unary minus)
```

`UnityEngine.Vector3` and `Vector2` arithmetic is supported via their built-in operators:
```csharp
-v                  // unary negation
v1 + v2             // component-wise addition
v1 - v2             // component-wise subtraction
v * s               // scale by a scalar (s may be int or float — int is widened to float)
v / s               // divide by a scalar
```

### XOR Operator
```csharp
a ^ b               // bool: logical XOR; integers: bitwise XOR
```

### Comparison Operators
```csharp
a == b              // equality
a != b              // inequality
a < b               // less than
a > b               // greater than
a <= b              // less than or equal
a >= b              // greater than or equal
```

### Logical Operators
```csharp
a && b              // logical AND
a || b              // logical OR
!a                  // logical NOT
```

### Ternary Conditional Operator
```csharp
condition ? trueValue : falseValue

// Examples:
int max = a > b ? a : b;
string result = x > 10 ? "high" : "low";

// Nested ternary
int category = x > 20 ? 2 : (x > 10 ? 1 : 0);
```

### Type Cast Operator
```csharp
(int)3.14           // cast double to int
(double)5           // cast int to double
(float)2.5          // cast to float
(bool)x             // cast to bool
(string)obj         // cast to string
```

### Operator Precedence (highest to lowest)
1. Member access (`.`), method calls (`()`), array access (`[]`)
2. Unary operators (`!`, `-`, casts, `++`, `--`)
3. Multiplicative (`*`, `/`, `%`)
4. Additive (`+`, `-`)
5. Comparison (`<`, `>`, `<=`, `>=`)
6. Equality (`==`, `!=`)
7. XOR (`^`)
8. Logical AND (`&&`)
9. Logical OR (`||`)
10. Ternary conditional (`? :`)
11. Assignment (`=`, `+=`, `-=`, `*=`, `/=`)

---

## Control Flow

### If Statement
```csharp
if (condition)
{
    // code
}

if (condition)
{
    // code
}
else
{
    // code
}

// Nested if
if (x > 10)
{
    if (x > 20)
    {
        // code
    }
}

// If-else chain
if (x > 20)
{
    // code
}
else if (x > 10)
{
    // code
}
else
{
    // code
}
```

**Note:** If statements without braces are NOT supported. Always use braces.

### While Loop
```csharp
while (condition)
{
    // code
}

// Example
int i = 0;
while (i < 10)
{
    i++;
}
```

### For Loop
```csharp
for (initialization; condition; increment)
{
    // code
}

// Example
for (int i = 0; i < 10; i++)
{
    // code
}

// Multiple operations
for (int i = 0; i < 10; i++)
{
    int square = i * i;
    result += square;
}
```

### Break and Continue
```csharp
// Break - exit loop
while (x < 100)
{
    if (x == 50)
    {
        break;  // exit the loop
    }
    x++;
}

// Continue - skip to next iteration
for (int i = 0; i < 10; i++)
{
    if (i % 2 == 0)
    {
        continue;  // skip even numbers
    }
    result += i;
}
```

### Return Statement
```csharp
return;              // return from void method
return value;        // return a value
return x + y;        // return expression result
```

**Important:** Early returns are supported. The following works as written — the `return`
exits the method, and any code after the `if` block runs only when the condition is false:
```csharp
if (condition)
{
    return x;
}
// more code
```

---

## Methods

### Local Method Definition
```csharp
returnType methodName(type1 param1, type2 param2)
{
    // method body
    return value;
}

// Examples
int add(int a, int b)
{
    return a + b;
}

double distance(double x, double y)
{
    return x * x + y * y;
}

void doSomething(int x)
{
    // void methods don't return a value
}
```

### Calling Local Methods
```csharp
int result = add(5, 3);
double d = distance(3.0, 4.0);
doSomething(10);
```

### Calling Registered Static Methods
```csharp
// If Math class methods are registered
int absolute = (int)Abs(-5);
double power = Pow(2.0, 3.0);
```

### Method Overloading
The compiler supports calling overloaded methods and will select the best matching overload based on argument types.

---

## Object Construction

### Creating Objects with 'new'
```csharp
// Constructor with no arguments
var sb = new System.Text.StringBuilder();

// Constructor with arguments
var sb = new System.Text.StringBuilder("Hello");

// Using type aliases (if registered)
var v = new Vector3(1.0, 2.0, 3.0);
```

**Note:** Types must be registered with the compiler before they can be constructed. Use fully qualified names or registered type aliases.

---

## Member Access

### Property Access
```csharp
// Reading properties
int length = sb.Length;
double x = vector.x;

// Writing properties
vector.x = 10.0;
transform.position = newPosition;
```

### Field Access
```csharp
// Reading fields
double x = vector.x;

// Writing fields
vector.x = 5.0;
```

### Chained Property Access
```csharp
// Nested property access
transform.position.x = 100.0;
double y = gameObject.transform.position.y;
```

### Method Calls on Objects
```csharp
// Instance methods
sb.Append("text");
string result = sb.ToString();

// Chained method calls
sb.Append("Hello").Append(" ").Append("World");
```

---

## Lambda Expressions

Lambda expressions are supported for use with LINQ and other methods that accept delegates.

### Single Parameter Lambda
```csharp
x => x * 2                    // simple expression
x => x > 5                    // boolean expression
item => item.property         // property access
```

### Lambda Type Inference
The compiler automatically infers lambda parameter types from the context:

```csharp
// Type of 'x' is inferred from the collection element type
list.Where(x => x > 5)
list.Select(x => x * 2)
```

### Lambda Usage Examples
```csharp
// Filtering
var filtered = list.Where(x => x > 5);

// Mapping
var doubled = list.Select(x => x * 2);

// Predicate
var hasAny = list.Any(x => x == target);

// Complex expression
var result = list.Where(x => x > 0).Select(x => x * x);
```

**Note:** Only single-parameter lambdas with simple arrow syntax are supported. Multi-parameter lambdas and statement body lambdas are NOT supported.

---

## LINQ Operations

When `System.Linq.Enumerable` methods are registered, standard LINQ extension methods are available:

### Filtering
```csharp
var filtered = collection.Where(x => x > 5);
```

### Projection
```csharp
var transformed = collection.Select(x => x * 2);
var properties = objects.Select(obj => obj.property);
```

### Aggregation
```csharp
int sum = collection.Sum();
int count = collection.Count();
var first = collection.First();
var last = collection.Last();
```

### Chaining Operations
```csharp
var result = list
    .Where(x => x > 3)
    .Select(x => x * 2)
    .Sum();
```

### Combining with Local Methods
```csharp
int square(int x)
{
    return x * x;
}

var squares = list.Select(x => square(x));
```

---

## Comments

### Single-Line Comments
```csharp
// This is a comment
int x = 5;  // Comment after code
```

**Note:** Multi-line comments (`/* */`) are NOT supported. Only single-line `//` comments.

---

## What is NOT Supported

The following C# features are **NOT** supported and will cause errors:

### Not Supported - Language Features
- ❌ Classes, structs, interfaces, enums (definition)
- ❌ Namespaces
- ❌ using directives
- ❌ Properties (only fields are supported directly)
- ❌ Events
- ❌ Delegates (except in lambda expressions)
- ❌ Indexers
- ❌ Operator overloading
- ❌ Extension methods (definition - calling registered ones is OK)
- ❌ Generics (definition - using generic types is OK)
- ❌ Attributes
- ❌ async/await
- ❌ LINQ query syntax (use method syntax instead)
- ❌ Pattern matching
- ❌ Tuples
- ❌ Nullable types (`int?`, `string?`)
- ❌ null coalescing operators (`??`, `??=`)
- ❌ String interpolation (`$"text {variable}"`)

### Not Supported - Statements
- ❌ switch statements and switch expressions
- ❌ foreach loops
- ❌ do-while loops
- ❌ try-catch-finally
- ❌ throw statements
- ❌ lock statements
- ❌ using statements
- ❌ goto statements

### Not Supported - Operators
- ✅ `^` (XOR) IS supported — see [Operators](#xor-operator)
- ❌ Bitwise operators (`&`, `|`, `~`, `<<`, `>>`)
- ❌ Compound null-conditional operators (`?.`, `?[]`)
- ❌ Compound null-coalescing operators (`??`, `??=`)
- ❌ Range / index-from-end operators (`..`)
- ❌ typeof, sizeof, nameof
- ❌ is, as operators
- ❌ default operator

### Not Supported - Collections
- ❌ Array initializers (`new int[] { 1, 2, 3 }`)
- ❌ Collection initializers (`new List<int> { 1, 2, 3 }`)
- ❌ Dictionary initializers
- ❌ Index access (`array[0]`) - despite token existing, parsing not implemented

### Not Supported - Lambdas
- ❌ Multi-parameter lambdas (`(x, y) => x + y`)
- ❌ Statement body lambdas (`x => { return x * 2; }`)
- ❌ Zero-parameter lambdas (`() => value`)
- ❌ Explicit lambda types (`(int x) => x * 2`)

### Not Supported - Comments
- ❌ Multi-line comments (`/* ... */`)
- ❌ XML documentation comments (`/// <summary>`)

### Not Supported - Misc
- ❌ var in method parameters
- ❌ ref, out, in parameters
- ❌ params arrays
- ❌ Optional parameters with defaults
- ❌ Named arguments
- ❌ this keyword
- ❌ base keyword
- ❌ static keyword (outside registered types)

---

## Complete Valid Example

Here's a comprehensive example showing many supported features:

```csharp
// Local method definitions
int square(int x)
{
    return x * x;
}

double distance(double x1, double y1, double x2, double y2)
{
    double dx = x2 - x1;
    double dy = y2 - y1;
    return dx * dx + dy * dy;
}

// Variable declarations
int count = 0;
double sum = 0.0;
var result = 0;

// For loop with conditionals
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
    
    // Early exit condition
    if (sum > 1000.0)
    {
        break;
    }
}

// Object construction and manipulation
var point = new Vector3(1.0, 2.0, 3.0);
point.x = point.x + 10.0;
point.y *= 2.0;

// LINQ operations with lambdas
var filtered = items.Where(x => x > 5);
var doubled = filtered.Select(x => x * 2);
var total = doubled.Sum();

// Ternary operator
int category = total > 100 ? 2 : (total > 50 ? 1 : 0);

// While loop
while (count < 10)
{
    count++;
    if (count == 5)
    {
        continue;
    }
    result += count;
}

// Method calls
int sq = square(5);
double dist = distance(0.0, 0.0, point.x, point.y);

// Return result
return result + category + sq;
```

---

## Tips for AI Code Generation

1. **Always use braces** for if statements, loops, etc. Single-statement bodies without braces are not supported.

2. **Always end statements with semicolons** - this is required.

3. **Use type inference (`var`)** when the type is obvious from the initializer.

4. **Lambda expressions are single-parameter only** - use `x => expression` format.

5. **Types must be registered** before using them with `new`. Check what types are available before generating construction code.

6. **Early returns are OK** - a `return` inside an `if` exits the method, and code after the `if` runs only when the condition was false.

7. **Use LINQ method syntax**, not query syntax (`from x in list`).

8. **Comments must use `//`** - no multi-line comments.

9. **No null handling** - assume all values are non-null.

10. **Operator precedence follows C# standard** - use parentheses for clarity when needed.

---

## Summary

This compiler supports:
✅ Basic types (int, float, double, bool, string)
✅ Variables and assignments
✅ All arithmetic, comparison, and logical operators
✅ If-else statements
✅ For and while loops
✅ Break and continue
✅ Local method definitions
✅ Object construction with `new`
✅ Property and field access
✅ Method calls (static and instance)
✅ Single-parameter lambda expressions
✅ LINQ extension methods
✅ Ternary conditional operator
✅ Type casting
✅ Single-line comments

This is a **procedural subset of C#** optimized for expression evaluation and Unity-style scripting.


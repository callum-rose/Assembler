# Assembler.Compiler

Runtime compiler that turns C# expression strings (written in YAML game descriptors) into callable delegates. Lexes and parses a procedural C# subset into `System.Linq.Expressions` trees, then compiles them to delegates via `LambdaExpression.Compile()`.

## Public API

`ExpressionMethodCompiler` is the only entry point callers should use. `Lexer`, `Parser`, `Token`, and `TokenType` are internal implementation details.

| Type / Member | Purpose |
|---|---|
| `ExpressionMethodCompiler.RegisterMethod(name, MethodInfo)` | Make a method callable by name inside expressions. |
| `ExpressionMethodCompiler.RegisterStaticMethods(Type)` | Bulk-register all public static methods on a type. |
| `ExpressionMethodCompiler.RegisterType(Type, alias?)` | Make a type available for `new` expressions and static access. |
| `ExpressionMethodCompiler.Compile(code, returnType, out delegateType, params)` | Compile to a raw `Delegate`; outputs the concrete delegate type. |
| `ExpressionMethodCompiler.CompileFunc<T, TResult>(code, paramName)` | Typed shorthand for functions (0–2 parameters). |
| `ExpressionMethodCompiler.CompileAction<T>(code, paramName)` | Typed shorthand for void methods (0–2 parameters). |

## Gotchas

- **Language spec**: see `COMPILER_SYNTAX_REFERENCE.md` — consult before writing expressions. Supports arithmetic, comparisons, logical operators, `if/else`, `for/foreach/while`, `switch`, `return`, `var`, `new`, lambdas, and LINQ.
- **Consumed by**: `ExpressionSource<T>` (in `Assets/Parsing/Info/`) carries the raw string; `ExpressionValueProvider<T>` (in `Assets/Resolving/`) calls `Compile` at resolve time to produce the live delegate.
- All parameters and return types must be nullable-annotated correctly, or the generated expression tree will fail to compile.
- Tests live in `Assets/Tests/Tests.Compiler/`.

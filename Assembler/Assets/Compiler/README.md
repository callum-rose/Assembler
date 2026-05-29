# Assembler.Compiler

Runtime compiler that turns C# expression strings (written in YAML game descriptors) into callable delegates. It lexes and parses a procedural C# subset into `System.Linq.Expressions` trees, then compiles them to delegates via `LambdaExpression.Compile()`.

## Public API

**`ExpressionMethodCompiler`** — the only entry point callers should use.

| Member | Purpose |
|---|---|
| `RegisterMethod(name, MethodInfo)` | Make a method callable by name inside expressions |
| `RegisterStaticMethods(Type)` | Bulk-register all public static methods on a type |
| `RegisterType(Type, alias?)` | Make a type available for `new` expressions and static access |
| `Compile(code, returnType, out delegateType, params)` | Compile to a raw `Delegate`; also outputs the concrete delegate type |
| `CompileFunc<T, TResult>(code, paramName)` | Typed shorthand for functions (0–2 parameters) |
| `CompileAction<T>(code, paramName)` | Typed shorthand for void methods (0–2 parameters) |

`Lexer`, `Parser`, `Token`, and `TokenType` are internal implementation details; do not instantiate them directly.

## Key details

- **Language spec**: see `COMPILER_SYNTAX_REFERENCE.md` — consult it before writing expressions. Supports arithmetic, comparisons, logical operators, `if/else`, `for/foreach/while`, `switch`, `return`, `var`, `new`, lambdas, and LINQ.
- **Consumed by**: `ExpressionSource<T>` (in `Assets/Parsing/Info/`) carries the raw string; `ExpressionValueProvider<T>` (in `Assets/Resolving/`) calls `Compile` at resolve time to produce the live delegate.
- **Nullable types**: enabled via `csc.rsp`; all parameters and return types must be nullable-annotated correctly or the generated expression tree will fail to compile.
- **Tests**: `Assets/Tests/Tests.Compiler/`.
- **Assembly**: `Assembler.Compiler`, namespace `Assembler.Compiler.Compiler`.

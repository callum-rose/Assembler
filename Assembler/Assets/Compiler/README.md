# Assembler.Compiler

A runtime compiler that turns C# expression strings (written in YAML game descriptors) into callable delegates. It lexes and parses a procedural C# subset — arithmetic, comparisons, logical operators, `if/else`, `for/foreach/while`, `switch`, `return`, `var`, `new`, lambdas, and LINQ — into `System.Linq.Expressions` trees, then compiles them to delegates via `LambdaExpression.Compile()`.

`ExpressionMethodCompiler` is the public entry point, with `RegisterMethod`, `RegisterStaticMethods`, and `RegisterType` to populate the symbol table and `Compile` / `CompileFunc` / `CompileAction` to produce delegates. The full language specification lives in `COMPILER_SYNTAX_REFERENCE.md`. Consumed by `ExpressionSource<T>` (in `Assets/Parsing/`) and `ExpressionValueProvider<T>` (in `Assets/Resolving/`), which together carry the raw expression string from YAML through to a live delegate at resolve time.

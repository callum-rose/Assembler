# Compiler

A runtime compiler that turns short C# expression strings (written inside YAML game descriptors) into callable delegates. It lexes and parses a procedural C# subset — arithmetic, comparisons, logical operators, control flow, `var`, `new`, lambdas, and LINQ — into expression trees and compiles them to delegates at runtime.

The supported language and its limits are documented in the syntax reference alongside the code; consult it before writing expressions. Expressions appear as a value type in the parsing layer and are compiled to delegates by the resolving layer at the point where they are first read.

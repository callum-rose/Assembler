# Assembler.Validation

A **runtime** Unity assembly that does a basic, schema-agnostic structural check on YAML. It is built
into the engine (no editor or platform dependencies, included in every build target), so it can
validate a game descriptor at runtime inside a player build on any platform — as well as from the
editor and the command line.

It checks that a document is **well-formed** and free of common structural mistakes, and reports —
with line/column — where and why it is invalid. It does **not** validate against the
game-descriptor schema; that is the job of the runtime parser/transformer once the YAML itself is
known-good.

## What it catches

| Problem | Severity | Example message |
| --- | --- | --- |
| Syntax errors (bad indentation, mis-aligned mappings, unterminated quotes/flows, tabs-as-indentation, …) | error | `While scanning a mapping, found invalid tab as indentation.` |
| Duplicate keys in a mapping (silently drops data at runtime) | error | `duplicate key 'Title' in mapping (first defined at line 2)` |
| Empty document (no content) | warning | `document is empty (no content)` |

The custom descriptor tags (`!vec`, `!colour`, `!var`, `!expr`, …) need no special handling: at the
parser level a tag is just a property on a node, so tagged values validate fine without the DTOs. The
assembly uses the same YamlDotNet (17.1.0) the runtime `GameFileParser` uses, so its parser behaves
identically.

## Using it from code

```csharp
using Assembler.Validation;

YamlValidationResult result = YamlStructureValidator.Validate(yamlText, "MyLevel.yaml");
if (!result.IsValid)
    Debug.LogError(result.FormatReport());   // detailed report with line/column + caret snippets

// Inspect individual issues programmatically:
foreach (YamlValidationIssue issue in result.Issues)
    Debug.Log($"{issue.Severity} @ {issue.Line}:{issue.Column} — {issue.Message}");
```

`YamlStructureValidator.ValidateFile(path)` is a convenience wrapper for platforms with file-system
access; on platforms without it, load the text yourself and call `Validate`.

## Running it during authoring

- **Command line**: `Assembler/Tools/validate-yaml.sh` (boots Unity headlessly, validates all example
  descriptors by default, or the file/dir paths you pass; exits non-zero on errors).
- **Editor menu**: `Assembler > Validate Descriptor YAML`.

Both front-ends call into this assembly via `Editor.YamlValidatorBatch`.

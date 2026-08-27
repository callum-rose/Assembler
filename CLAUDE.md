# CLAUDE.md

Guidance for Claude Code at the repo root. The Unity project lives in
`Assembler/` and has its own [`CLAUDE.md`](Assembler/CLAUDE.md) — read it
before working on any code under `Assembler/`.

## Agent skills

### Issue tracker

Issues live as GitHub issues on `callum-rose/Assembler`, managed via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical triage roles, each label string equal to its name. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context — `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

## Unity assets

Never hand-author Unity asset files (`.prefab`, `.unity`, `.asset`, `.mat`, …). Ask the user to check the branch out in their main checkout and create them via the `unity-mcp` editor tools — see [Creating Unity assets](Assembler/CLAUDE.md#creating-unity-assets).

# AI Runbook — LpAutomation (.NET 8)

Solution: LpAutomation.sln

This repository contains a single Visual Studio solution with the following projects:

- LpAutomation.Core
- LpAutomation.Contracts
- LpAutomation.Desktop (legacy; avoid adding new features)
- LpAutomation.Desktop.Avalonia (primary UI)
- LpAutomation.Server

## Target Environment
- .NET SDK 8.x (built using dotnet 8.0.4)
- Windows 11 primary development environment
- Visual Studio 2026

## Required Change Workflow

When making modifications:

1. Read SPEC.md before making architectural decisions.
2. Propose a concise implementation plan.
3. Make small, incremental changes.
4. Validate the entire solution builds successfully.
5. Only then present final changes.

Do not:
- Perform large rewrites unless explicitly requested.
- Modify project structure without explanation.
- Add unnecessary dependencies.

---

## Build & Validation Commands

Run from repository root:

### Confirm SDK
dotnet --version

Expected: 8.x

### Restore
dotnet restore ./LpAutomation.sln

### Build (Release preferred)
dotnet build ./LpAutomation.sln -c Release

Build must:
- Succeed without errors
- Not introduce new warnings unless explicitly justified

---

## Output Requirements Before Presenting Code

Before providing final changes, include:

- Short implementation summary
- Files modified
- High-level diff summary
- Build result confirmation

If build fails:
- Diagnose
- Fix
- Rebuild
- Repeat until clean
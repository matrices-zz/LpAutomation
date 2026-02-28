# LpAutomation — Specification & Constraints

## High-level intent
LpAutomation is a multi-project C# solution. Keep the design modular:
- Core: domain logic and internal services
- Contracts: shared DTOs/interfaces/contracts used across layers
- Desktop.Avalonia: desktop UI (AXAML), MVVM patterns
- Server: server/host layer

## Architectural rules
1) LpAutomation.Contracts should have minimal dependencies.
   - Prefer: pure POCOs, DTOs, interfaces, enums.
   - Avoid: UI framework references, heavy third-party dependencies.

2) LpAutomation.Core can depend on Contracts.
   - Core should NOT depend on UI projects.
   - Keep Core testable and UI-agnostic.

3) Desktop.Avalonia uses Core + Contracts.
   - Use MVVM-friendly patterns.
   - Keep UI code in UI project; move logic into Core.

4) Server uses Core + Contracts.
   - Keep hosting concerns (config, DI, endpoints) in Server.
   - Push reusable logic into Core.

## Project-specific notes
- LpAutomation.Desktop is legacy and likely to be removed.
  - Do not add new features there.
  - Only touch it when needed for compilation or migration.

## Change discipline (Codex-like workflow)
When implementing a feature or refactor:
- Prefer multiple small commits/patches over one huge rewrite.
- Start with a plan, then implement, then build.
- If build fails, fix it before proposing final code.
- Avoid speculative changes not requested.

## Coding style
- C# 12 / .NET 8 conventions
- Nullable reference types: [state here: enabled/disabled]
- Async: prefer async/await, avoid blocking calls
- Logging: [state here if you use Serilog/Microsoft.Extensions.Logging/etc.]

## Build instructions
See AI_RUNBOOK.md
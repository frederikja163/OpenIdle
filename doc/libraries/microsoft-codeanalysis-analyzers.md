# Microsoft.CodeAnalysis.Analyzers

- Status: adopted
- Date: 2026-08-06
- Decided by: project owner
- Version / commit pinned: 3.11.0 in `Generators/Backend/Generator.Backend.csproj` (`PrivateAssets="all"`)

## 1. Problem

The source generator (`Generators/Backend`) is written against the Roslyn API ([Microsoft.CodeAnalysis.CSharp](./microsoft-codeanalysis-csharp.md)), and the compiler's own API has right and wrong ways to use it. `Microsoft.CodeAnalysis.Analyzers` ships the **RS-prefixed rules** that encode those ways — comparing symbols with the right comparer, not leaking state into static fields, registering incremental pipelines correctly, reporting diagnostics with proper `DiagnosticDescriptor`s, and so on. These rules are the safety net that keeps an incremental generator incremental: miss them and a generator *works* but silently defeats the compiler's caching on every build. This package is included automatically as a development dependency of `Microsoft.CodeAnalysis.CSharp` (≥ 3.11.0), so the rules are active either way; the explicit reference pins the version and documents the intent. It is build-time-only (`PrivateAssets="all"`, `IsRoslynComponent`), never shipped.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **Microsoft.CodeAnalysis.Analyzers** (chosen) | 3.11.0, ~1.5 MB, no deps | The official analyzer-development rules (RSxxxx) shipped from dotnet/roslyn-analyzers; enforces correct `Microsoft.CodeAnalysis` API usage in generator/analyzer authors | First-party, Microsoft; 3.11.0 has 131M downloads | MIT | High: it is the canonical guidance for exactly what we are writing |
| Rely on the transitive copy only | 3.11.0 (via Microsoft.CodeAnalysis.CSharp) | Identical rule set, no explicit reference | Same | MIT | Low-medium: rules still run, but the version is implicit and can drift silently when the CSharp pin changes |
| No analyzer-development rules | n/a | Skip the RS rules entirely; rely on code review | n/a | n/a | Rejected: these rules catch real correctness bugs (wrong symbol comparers, non-incremental pipelines) that are easy to ship past review |

Why the others lost: relying on the transitive copy is functionally close but leaves the rule-set version unpinned and undocumented — the whole point of a decision doc is to make that explicit. Skipping the rules saves 1.5 MB of build-time tooling and loses the automated enforcement that `EnforceExtendedAnalyzerRules` turns into build errors; a solo owner does not want to hand-audit API usage that has a first-party checker.

## 3. Decision & rationale

Reference **Microsoft.CodeAnalysis.Analyzers 3.11.0** explicitly in `Generators/Backend`, `PrivateAssets="all"`, with `EnforceExtendedAnalyzerRules=true` in the project so the RS rules are enforced as errors rather than warnings. Rationale: the package is first-party, MIT, dependency-free, and is the same analyzer-development SDK Microsoft ships for everyone building on Roslyn. The explicit pin documents the rule-set version and decouples it from the `Microsoft.CodeAnalysis.CSharp` pin — so bumping the compiler API version cannot silently change (or drop) the rules we rely on.

### Pros

- **First-party compiler guidance.** The rules are written by the team that owns the API; there is no closer source of "correct usage".
- **No dependencies, no weight.** ~1.5 MB, no transitive packages, analyzers-only assets.
- **Cheap and continuous.** Catches API misuse in the same build it compiles the generator; no extra step.
- **Pinned independently.** The explicit reference means the rule set does not drift when the Roslyn package version changes.

### Cons

- **Pure build-time tooling.** It is enforced value but produces no output of its own; a package the generator never ships.
- **Rules can be fussy.** RS rules sometimes demand ceremony (e.g. `[Generator]` attributes, output-kind configuration) that feels redundant until it matters; the payoff is real but indirect.

## 4. Build-vs-buy

You cannot meaningfully "build" API-usage rules — this is first-party guidance on the platform's own API, and reimplementing any of it in-house would be re-deriving the compiler team's judgment from first principles for no benefit. The honest alternatives are the ones in §2: take the rules, take them transitively, or take none. Since the transitive copy already arrives with `Microsoft.CodeAnalysis.CSharp`, the *marginal cost* of the explicit reference is near zero — its value is the pin and the documentation. "Build" does not apply; "buy" is nearly free.

## 5. Risk

### Undo risk — low

Confined to the `Generators/Backend` project. Removing the package turns the RS rules off (or back to transitive) without changing a byte of generated output. Nothing downstream depends on it.

### Security risk — low

No dependencies, 1.5 MB, analyzers-only assets, MIT, first-party Microsoft. It executes only at build time against the generator project's own code, is never shipped, and has no known CVEs as of 2026-08.

# Microsoft.CodeAnalysis.CSharp (Roslyn)

- Status: adopted
- Date: 2026-08-06
- Decided by: project owner
- Version / commit pinned: 4.14.0 in `Generators/Backend/Generator.Backend.csproj` (`PrivateAssets="all"`)

## 1. Problem

The backend generates its DTOs from `types.xml` at compile time via a Roslyn source generator (`Backend.Generators.TypesGenerator`). Writing any source generator means implementing the compiler-platform API — `IIncrementalGenerator`, `IncrementalGeneratorInitializationContext`, `AdditionalTextsProvider`, `SourceProductionContext`, `DiagnosticDescriptor` — and those types ship in the `Microsoft.CodeAnalysis` NuGet packages. This decision is about *which slice* of the compiler platform the generator project references. This is a **build-time-only** dependency: the package is referenced with `PrivateAssets="all"`, the generator assembly is loaded by the compiler during the backend build (`OutputItemType="Analyzer"`), and nothing it contains is ever shipped or runs at runtime.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **Microsoft.CodeAnalysis.CSharp** (chosen) | 4.14.0, ~16.5 MB | The compiler platform itself: syntax/semantic API + incremental generator pipeline. Minimal slice for a generator (CSharp + Common; no Workspaces/services) | First-party, dotnet/roslyn, ships in every .NET SDK; 4.14.0 is one of the most-downloaded versions (27.3M, ~1.7M/day) | MIT | High: it *is* the platform — there is no other way to write a Roslyn generator |
| Microsoft.CodeAnalysis.CSharp.Workspaces / all-in-one Microsoft.CodeAnalysis | 4.14.0 | Adds solution/project services, code fixes, refactoring hosts | First-party | MIT | Low: a generator needs none of the Workspaces surface — would add assemblies for nothing |
| Run the existing CLI as an MSBuild pre-build step instead of a source generator | n/a (in-house) | `Generators/Generator -t cs` writes `Dto.g.cs` into the project before compile; no Roslyn API referenced at all | Ours | n/a | Medium: avoids the package, but loses incremental caching, IDE diagnostics and build error integration — see §4 |
| Hand-written DTOs (no generator) | n/a | Write classes + registry by hand as before | Ours | n/a | Rejected by the [DTO XML contract](./dto-xml-contract.md) problem statement |

Why the others lost: Workspaces adds service-layer assemblies the generator never touches — the extra weight buys nothing. The CLI-as-pre-build-step is the strongest honest alternative and is examined in §4; it loses on integration, not on feasibility. Hand-writing DTOs contradicts the whole point of the contract decision.

## 3. Decision & rationale

Reference **Microsoft.CodeAnalysis.CSharp 4.14.0** from the source generator project (`Generators/Backend`), with `PrivateAssets="all"`. Rationale: a Roslyn source generator is defined by this API — this is not a "which library" decision so much as a "which slice and which version" one. The CSharp package is the minimal correct slice, it is the exact same code the .NET SDK compiler is built from, and it carries no external transitive dependencies beyond its own `Microsoft.CodeAnalysis.Common` and the standard `System.*` support packages.

The **version skew is deliberate and must be recorded**: the SDK's compiler (Roslyn 5.0.0, C# 14, in the .NET 10 SDK) *hosts* the generator, while the generator is *built against* 4.14.0 (C# 13-era). That is safe because Roslyn loads analyzers built against older versions than the host, and it is the normal state of a pinned generator. The pin should be reviewed on SDK upgrades and never raised above the SDK's Roslyn version (a newer-than-host analyzer will not load).

### Pros

- **First-party and unavoidable.** It is the compiler platform; every source generator in the ecosystem runs on this API, so there is no integration or compatibility gamble.
- **Minimal slice.** CSharp + Common only; no Workspaces/services assemblies.
- **Hosts everywhere.** Targets `netstandard2.0`, so the same assembly loads in the CLI, MSBuild, and IDE language services.
- **Zero external risk surface.** MIT, Microsoft-owned, no native binaries, no install scripts; the only packages it pulls are its own Common plus `System.Collections.Immutable`/`System.Reflection.Metadata`.
- **No known vulnerabilities.** Sonatype reports none for 4.14.0.

### Cons

- **Big package for build-time use only.** ~16.5 MB downloaded, but `PrivateAssets="all"` means none of it leaves the build.
- **Version skew to manage.** Pinned 4.14.0 against an SDK compiler of 5.0.0; must never exceed the host version on upgrade.
- **Real API discipline required.** The incremental pipeline only pays off if the generator is written correctly (caching, `WithTrackingName`, no side effects). The companion analyzer package ([Microsoft.CodeAnalysis.Analyzers](./microsoft-codeanalysis-analyzers.md)) plus `EnforceExtendedAnalyzerRules` exist to enforce that discipline.

## 4. Build-vs-buy

There is no "build" alternative for the API itself — it is the platform. The real build-vs-buy question is whether the generator should be a *source generator* at all, or whether the existing CLI should be invoked as an MSBuild pre-build step that writes `Dto.g.cs` into the project. Both were viable; the source generator won on integration: it is **incremental** (re-runs only when `types.xml` changes), it surfaces errors through the build **and the IDE** as diagnostics (DTC001/DTC002) instead of an MSBuild task's stderr, and it needs no up-to-date-check plumbing or ordering with the compiler. The pre-build CLI would re-add exactly the "when do I regenerate" bookkeeping the generator removes, for the sake of avoiding a package that costs the build nothing and ships nowhere. The marginal cost of the package is effectively zero — it is the same binaries the SDK already installs.

## 5. Risk

### Undo risk — low

Confined to the `Generators/Backend` source-generator project. Removing the package means either hand-writing the DTOs (reverting the contract decision) or switching to the pre-build CLI approach from §4 — a contained change. Nothing downstream (backend runtime, frontend) references Roslyn.

### Security risk — low

First-party MIT, no native binaries, no build/install scripts, no known CVEs as of 2026-08. The package is never shipped (build-time analyzer only), runs on committed contract files, and has no network access of its own. The `PrivateAssets="all"` reference is the guarantee that none of it leaks into the backend output.

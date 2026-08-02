# C# / .NET

- Status: adopted
- Date: 2026-08-02
- Decided by: project owner
- Version / commit pinned: .NET 10 (LTS), 10.0.10 (latest patch as of 2026-07-14)

## 1. Problem

We need a backend language and runtime to build the game server for an open-source idle MMORPG. The backend must: run a tick/simulation engine that advances player state while they're offline, persist typed game data (inventories, money, levels) with strong safety so player progress doesn't corrupt, and deploy cheaply and simply (a solo-owner open-source project — no team, no ops budget). No language is chosen yet; this is the first foundational dependency the whole project sits on.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **C# / .NET 10** (chosen) | LTS, supported to Nov 2028 | Strong static typing + records for data safety; solid single-threaded + task-based concurrency; self-hosted Kestrel; single-file publish; GC is fine at idle-game tick rates | Microsoft, LTS security patches until 2028 | MIT (runtime/BCL) | High: owner is fluent in C#; typing matches data-safety priority |
| TypeScript / Node.js | Node ~23+, no LTS-equivalent commitment | Shares types with a TS web client (Melvor-style browser game); huge ecosystem | Very active | MIT | Medium: loses server-side type safety strength, gains client type-sharing; weak for CPU-bound tick work |
| Go | Current release, ~15-30MB static binary | Goroutines, zero-dependency HTTP stdlib, single static binary, no GC jitter concerns at our scale | Very active (Google) | BSD-3 | Medium: deployment is unbeatable, but owner not fluent; ecosystem smaller |
| Rust | Current release, static binary | No GC → deterministic ticks, memory safety, top performance | Very active | MIT/Apache-2.0 | Low: months of ramp-up for a solo owner; slowest iteration |
| Python | Current release | Fastest to write, huge ecosystem | Very active | PSF | Low: concurrency story and runtime perf are the weakest of the set |
| Build in-house | n/a | n/a | n/a | n/a | Not applicable: a language/runtime cannot be built in hours-not-weeks; the "build-vs-buy" axis applies to libraries, not this platform |

Why the others lost: TypeScript/Go/Rust/Python are all credible, but none beat the combination of the owner's fluency and C#'s type safety. TypeScript's one genuine advantage (client type sharing) is not yet relevant — no client exists, and idle MMO servers don't do CPU-bound work that Node handles badly but nothing that Node's single-thread model makes painful. Rust's determinism and Go's binary size are real wins we don't need at idle-game scale.

## 3. Decision & rationale

Adopt **C# on .NET 10 (LTS)**. The decision rests primarily on owner productivity (fluent in the language) plus strong static typing that protects the thing this game cares most about: player data. .NET 10 is the current LTS with security support until November 2028, MIT-licensed, cross-platform, and deploys as a framework-dependent or single-file self-contained app on a cheap Linux VPS.

### Pros

- Owner is already fluent — the single biggest lever for a solo open-source project.
- Strong static typing + immutable-ish data idioms (records) reduce the class of bugs that corrupt player state.
- .NET 10 is current LTS: free security patches until Nov 2028, pinned version.
- First-class HTTP (ASP.NET Core / Kestrel) and WebSocket support in the BCL ecosystem if we add real-time later.
- MIT licensed, no cost, active Microsoft support.
- Cross-platform — develops on Windows, deploys on Linux.
- Task/async model is straightforward for game tick loops and background processing.

### Cons

- No type sharing with a TypeScript browser client — the codebase is two-language by default.
- GC adds jitter vs a no-GC runtime; irrelevant at idle MMO tick rates, but not free of caveats.
- Runtime/deploy footprint is heavier than Go's single static binary (~15MB); irrelevant at our scale.
- Annual major release cadence means an LTS upgrade every ~3 years; minor maintenance cost.

## 4. Build-vs-buy

Not applicable in the usual sense — a language/runtime is not buildable in a short time, so "build in-house" is not a real option (recorded in the table for completeness). The equivalent decision axis is language choice, covered above. Note for later: this does NOT license ecosystem libraries (EF Core, ORMs, etc.) — each of those is a separate decision that must hold up under the same scrutiny.

## 5. Risk

### Undo risk — medium

A backend language is load-bearing: swapping to Go/Rust later means rewriting the entire server, and habits/patterns will have baked in. Mitigants: the project is greenfield (nothing written yet), and the language is confined to the server process, so a rewrite is costly but not catastrophic and no data is lost. If we ever needed client/server type sharing badly, TypeScript is the escape hatch — but it is a full rewrite, not a refactor.

### Security risk — low

.NET is a Microsoft-supported, widely-audited platform with a monthly Patch Tuesday cadence; CVEs are patched promptly. Main obligations: stay on the latest LTS patch and move off a version when it reaches EOL (don't let it drift past Nov 2028). Supply-chain risk comes later, from the NuGet packages we add — each of those gets its own decision document.

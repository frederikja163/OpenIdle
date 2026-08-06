# CommandLineParser

- Status: adopted
- Date: 2026-08-06
- Decided by: project owner
- Version / commit pinned: 2.9.1 (latest stable, 2022-05; no stable release since — accepted, see §3)

## 1. Problem

The `Generator` console app (`Generators/Generator`, a net10.0 exe) is the standalone command-line front-end to the DTO generator core. It needs to turn a handful of command-line flags (input XML path, output file, which emitter to run) into typed values, with free help text, sensible error messages for bad input, and a process exit code — and it needs to stay easy to extend and maintain as the tool grows. This is a **developer tool only**: it is never part of the shipped product. The problem this library solves is not "parsing is hard" but "argument handling, help, validation and error reporting are maintenance surface that a solo owner does not want to own by hand." This decision was originally made the other way (System.CommandLine 2.0.10); the project owner reversed it after seeing both syntaxes, preferring CommandLineParser's attribute-based API.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **CommandLineParser** (chosen) | 2.9.1 (May 2022), 0 deps | Attribute-based verbs/options, terse one-line API; proven (BenchmarkDotNet); declarative options-as-a-class that reads like a spec | Dormant: no stable release since 2022-05; a 2.9.2-preview1 went nowhere; DepScope health 36/100 | MIT | High: the owner prefers its syntax; 0 deps; the frozen API is fine for a small stable CLI |
| System.CommandLine | 2.0.10, ~537 KB | Typed options/arguments, auto help + `--version`, validation, exit codes, shell-completion hookup; POSIX and Windows conventions; the parser behind the `dotnet` CLI | Microsoft, GA 2025-11-11, monthly 2.0.x patches, 92.9M downloads, 258.7K/day | MIT | Low (owner): maintained and first-party, but its fluent API reads worse to the owner than the attribute approach; a young API that is still settling |
| McMaster.Extensions.CommandLineUtils | 5.1.0 (Apr 2026), 0 deps | Community fork of Microsoft's abandoned parser; attribute API + DI support | Revived Jan 2026 (5.0.0) after years in maintenance mode; single-owner-ish | MIT | Medium: credible and thin, but the owner already had CommandLineParser's syntax in mind |
| Spectre.Console.Cli | 0.55.0, ~1.33 MB | Full CLI *framework*: type-safe settings, DI, conventions, rich help | Active, but stuck at 0.x; the CLI half of the Spectre.Console project | MIT | Low: a framework with DI for a few flags in a dev tool is far more than the surface needs |
| Build in-house | n/a | A `switch` over `args` in `Program.cs`, hand-rolled help/errors | Ours | n/a | Medium on effort (~1 day), low on fit: hands back exactly the help/error/validation maintenance the problem names |

Why the others lost: System.CommandLine is the previous pick and loses on *syntax taste* — the owner read both implementations side by side and prefers CommandLineParser's options-as-an-attributed-class over the fluent builder; maintenance health, the deciding factor last time, is here outweighed by (a) owner preference and (b) the practical reality that a two-flag dev-tool CLI sits at the extreme edge of System.CommandLine's "grows with the tool" argument, where a frozen API is a non-issue. Spectre.Console.Cli drags in a DI-capable framework to parse two flags. McMaster.Extensions.CommandLineUtils is a credible runner-up but is not the syntax the owner asked for. Hand-rolling is *feasible* — the surface is small enough that it was genuinely considered — but it fails the explicit priority (easy to make and maintain) by re-creating help text, error formatting and flag conventions that the library gives for free.

## 3. Decision & rationale

Adopt **CommandLineParser 2.9.1** for the `Generator` console app, replacing the `System.CommandLine` 2.0.10 reference currently in `Generators/Generator/Generator.csproj`. Rationale: the owner reviewed the two candidate implementations and the attribute-declared options class (`[Option('i', "input", HelpText = …)]` on properties, one `ParseArguments<T>(args).MapResult(...)` call to run) is the syntax they prefer and find more readable than the fluent command-builder. It is zero-dependency, so it is the smallest possible addition to a dev tool, and its API has been stable since 2022 — for a fixed two-flag surface that stability is a feature, not a risk. The known con — dormancy — is acknowledged and accepted because the tool is dev-only, its CLI is small and not expected to grow a large verb surface, and nothing in the tool's design locks to the library: options are plain properties and the parse-to-run wiring is one line, so a future migration to another parser or to hand-rolled parsing is a half-day change confined to `Program.cs`.

### Pros

- **The syntax the owner prefers.** Options declared as an attributed class reads like a spec; `ParseArguments<T>().MapResult(run, _ => 1)` is the entire wiring. Readable at a glance and easy to extend with another flag.
- **Zero dependencies.** Smaller footprint than the rejected alternative in a tool whose whole point is being small.
- **Free help and errors.** Unknown option, missing value, and `--help`/`--version` output come from the parser, with a non-zero exit code on parse failure.
- **Stable and proven.** No stable release since 2022, but that is exactly why the surface is settled; widely used (BenchmarkDotNet and many others).
- **Trivial to reverse.** The parse step is one line and the options class is plain properties; if the tool ever outgrows this, migrating is a half-day change in one file.

### Cons

- **Effectively unmaintained.** No stable release since 2022-05; DepScope health 36/100. For a dev tool this is acceptable, but it must be recorded: there is no upstream to fix bugs or answer issues.
- **Frozen feature set.** No new features are coming (e.g. the shell-completion hooks and richer validation of the newer parsers). The current surface covers the tool's needs.
- **Help output is dated.** The generated help screen has a `Copyright (C) 2026 Generator` banner and `ERROR(S):` header from the library's defaults; harmless but visibly older-style.
- **Bias in this decision.** This reverses a decision made two days earlier on maintenance health, on the basis of syntax preference for a frozen library. That is a defensible trade — the maintenance argument is weak at two flags — but it is a preference call, and the maintenance argument would win again if the tool's CLI ever grows real complexity.

## 4. Build-vs-buy

In-house effort: roughly **half a day to a day** — parse a handful of flags, plus hand-written help text, error messages, and exit-code behavior, and a little more each time a flag or verb is added. That is genuinely small, and normally this project's policy would say build it. The buy still wins. First, the user's stated priority is *easy to make and maintain*, and the library's value is not parsing — it is not owning the help/error/validation surface plus its test burden, which is the part that accretes over a tool's lifetime. Second, this is a dev-only tool, so the "smaller is better" dependency cost that usually tips these decisions the other way does not apply — and at zero dependencies this library is barely a dependency at all. The honest counterweight is recorded in §3's cons: if the CLI never grows past a few flags, a future owner could remove the package and hand-roll in a day; nothing in the tool's design is locked in.

## 5. Risk

### Undo risk — low

Confined entirely to the `Generator` console app's argument-handling code (`Program.cs` and its call sites). The tool is not part of the shipped product, and the CLI surface is small enough that removing the package and hand-rolling the same few flags — or migrating to another parser — is a day's work. Nothing else in the codebase (generator core, source generator, backend) touches it.

### Security risk — low

Pure managed code, zero dependencies, no native binaries, no install or build scripts. The supply-chain exposure is a single MIT-licensed assembly that sits in a dev tool and is never shipped. The main caveat is the reverse of the usual one: dormancy means no upstream maintainers actively auditing, but it also means the code is frozen and the exact pinned version is what we run forever. There are no known CVEs against the package as of 2026-08.

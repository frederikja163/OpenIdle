# NUnit (test framework)

- Status: adopted
- Date: 2026-08-04
- Decided by: project owner
- Version / commit pinned: NUnit 4.6.1, `NUnit3TestAdapter` 6.2.0, `Microsoft.NET.Test.Sdk` 17.14.1, `coverlet.collector` 6.0.4

## 1. Problem

The project has no automated tests yet, and a test project (`tests/OpenIdle.Tests`) was just scaffolded for the socket-controller work. The unit and integration tests we are about to write (socket registry, endpoint dispatch, and later end-to-end integration tests against the real app) need a test framework: something that discovers tests, runs them, reports results, and plugs into `dotnet test` and the IDE. The framework initially added was xUnit (the `dotnet new` default). The project owner has a strong preference for NUnit's naming (`[Test]`/`[TestCase]`) and its constraint-based assertion model (`Assert.That`), has deep existing proficiency in NUnit (including custom extension work), and wants one test framework to standardize on going forward. This is a **dev-time dependency only** — nothing in the shipped product depends on it.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **NUnit 4** (chosen) | 4.6.1, ~1.5 MB | `[Test]`/`[TestCase]` naming, `Assert.That` constraint model, richest parameterization (`[TestCase]`/`[TestCaseSource]`/`[Values]`), supports VSTest and Microsoft Testing Platform | Active: .NET Foundation project, regular 4.x releases through 2026 | MIT | High: matches daily-driver preference and existing mastery; lifecycle objection mitigable (see §3) |
| xUnit (status quo) | 2.9.3 (currently installed) | Constructor + `IDisposable` lifecycle, parallel-by-default, `[Theory]`/`[InlineData]`/`[MemberData]`, terse `Assert.*` | Active: .NET Foundation project, used by the ASP.NET Core team | MIT | Medium: already installed and green, but naming/assertion model not preferred by the owner |
| MSTest | v4 (Microsoft `testfx`) | Microsoft first-party, source generators, `[TestClass]`/`[TestMethod]` attribute model, both VSTest and MTP | Microsoft-supported, strict semver | MIT | Low: attribute lifecycle, Microsoft-centric tooling, no owner preference |
| TUnit | current (2025/2026) | Modern source-generated test framework | Young, smaller community | MIT | Low: too new to bet a "default going forward" on |
| Build in-house | n/a | Hand-rolled runner + assertions | n/a | n/a | Rejected: not "one focused feature" — see §4 |

Why the others lost: **xUnit** is the only zero-new-dependency option (it is already installed) and its constructor-lifecycle philosophy is one the owner likes; it lost because the owner's naming and assertion-model preferences are the stronger signal for a tool used daily, and switching now costs nothing (17 tests, dev-only). **MSTest** offers nothing the owner prefers and is Microsoft-tooling-centric. **TUnit** is too young to standardize on. **In-house** is not viable (see §4).

## 3. Decision & rationale

Adopt **NUnit 4**, replacing xUnit in the test project. The deciding factors are subjective but durable: the owner prefers `[Test]`/`[TestCase]` naming and finds the `Assert.That` constraint model more intuitive than xUnit's `Assert.*`, and has deep existing proficiency (including custom NUnit extensions). For a framework that will be written and read on a daily basis, developer preference and mastery outweigh the objective-but-marginal differences between the frameworks, which are otherwise functionally equivalent for this project's needs (all support `dotnet test`, parallel execution, parameterized tests, and integration testing).

Two honest caveats:

- **Lifecycle model**: the owner dislikes NUnit's attribute-based `[SetUp]`/`[TearDown]` lifecycle. Mitigation: NUnit 3+ instantiates a fresh fixture per test, so the constructor + `IDisposable` pattern the owner prefers works in NUnit too. We will write tests constructor-style and avoid `[SetUp]`/`[TearDown]`. This preserves the lifecycle that was the strongest argument for xUnit.
- **Open source**: both NUnit and xUnit are MIT-licensed, .NET Foundation projects. "Open source" is not a differentiator; it is simply noted as a shared property.

### Pros

- Matches the owner's daily-driver preference and existing NUnit mastery.
- Constraint-based `Assert.That` model is more expressive and found more intuitive.
- Richest parameterization (`[TestCase]`, `[TestCaseSource]`, `[Values]`) for future data-driven tests.
- Supports both VSTest and Microsoft Testing Platform; works with `dotnet test`, VS, and Rider.
- Dev-only: no impact on the shipped product or its dependency tree; replaces xUnit rather than adding alongside it, so dependency count is unchanged.
- Lifecycle concern fully mitigable by writing constructor/`IDisposable`-style tests.

### Cons

- Replaces a working, already-installed framework (a one-time rewrite of ~17 tests).
- xUnit's parallel-by-default execution is lost; NUnit parallelizes only when opted in. Immaterial at current suite size, worth enabling later if the suite grows.
- NUnit's idiomatic style is attribute-based, so we are deliberately going against the grain of the framework's conventions.
- The "no new dependency" option (keep xUnit) was available and was declined.

## 4. Build-vs-buy

The cheap parts of a test framework — `Assert.Equal`-style helpers — are trivial to write in-house, and a basic runner is a weekend project. But a usable framework is discovery, parallel execution, fixtures/lifecycle, parameterization, assertion failure reporting, `dotnet test`/IDE/CI integration, and cross-platform behavior — months of work with high defect risk, against mature MIT frameworks that are essentially free to adopt. This is a clear buy. The realistic choice is *which* framework, not whether to buy one, and "smaller is better" is satisfied by replacing xUnit with NUnit rather than carrying both.

## 5. Risk

### Undo risk — low

Use is confined to the test project. Swapping frameworks is mechanical (attributes + assertion syntax) and the current suite is 17 tests; we already know the migration cost is trivial. No production code, services, or DI registrations depend on the test framework. A future change of heart is cheap.

### Security risk — low

Dev-only dependency: it runs in local dev and CI, never in the shipped product, and never processes untrusted input. NUnit is a .NET Foundation project under MIT with regular, active 4.x releases (4.6.1 current as of 2026-08) and no native binaries in its package. Supply-chain exposure is limited to the normal package-restore path; the pinned versions are recorded above.

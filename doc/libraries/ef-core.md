# EF Core (Entity Framework Core) + SQLite

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: EF Core 10.0.10, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.10 (first-party, aligns with .NET 10.0.10); `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 (non-Microsoft, see §3 and §5)

## 1. Problem

The backend currently keeps all state in memory: `UserService` and `ProfileService` are DI singletons holding plain C# collections, and there is no way for a user, a profile, or any game state to survive a server restart. The problem this library solves is **full game-state persistence**: players' profiles, inventories, money, levels, and progress must be durably stored so progress survives restarts and offline-time can be simulated against saved data. The schema will grow and evolve as the game is built (auth entities now, game-state entities later), and the data is player-critical — corruption or loss is the worst failure mode this project has. We need: durable storage, typed queries over that storage, schema versioning/migrations, and transactional writes. This is the third decision doc and the second "library" — like ASP.NET Core, EF Core is a first-party Microsoft package maintained as part of the .NET platform, so this is closer to *which slice of the platform* than *which third-party package*.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **EF Core 10 + SQLite provider** (chosen) | 10.0.10, ~a few MB + native SQLite bundle | Code-first model, LINQ queries, change tracking, transactions, migrations, strongly typed entities; SQLite provider is first-party | Microsoft, aligned with .NET 10 LTS patches until Nov 2028 | MIT | High: first-party, migrations + LINQ match a schema that will grow, provider swappable later |
| Dapper (micro-ORM) | 2.1.79, tiny, no deps | Extension methods over ADO.NET; you write raw SQL and map by hand | Active (200 contributors, frequent releases) | Apache-2.0 | Medium: lean and fast, but we'd write and maintain SQL for every entity plus hand-roll migrations |
| Raw ADO.NET (`Microsoft.Data.Sqlite`) | 10.0.x, first-party | Full control, no ORM abstraction; every query and mapping hand-written | Microsoft | MIT | Low: same maintenance cost as Dapper with none of the convenience |
| JSON file storage | n/a (in-house) | Zero dependencies, one file on disk | n/a | n/a | Low: no transactions, no querying, no concurrency safety, doesn't scale to full game state |
| No persistence (keep in-memory singletons) | n/a | Zero work today | n/a | n/a | Rejected: the problem statement is explicitly that state must survive restarts |

Why the others lost: Dapper and raw ADO.NET both force hand-written SQL and hand-rolled migrations — fine when the schema is 2 tables, a real tax once the game schema grows to dozens of evolving entities; they only win where performance or full SQL control dominates, which an idle game does not need. JSON files fail on transactionality and querying. Doing nothing contradicts the stated problem. The honest caveat on the chosen side: EF Core's SQLite provider bundles the native SQLite library (`SQLitePCLRaw.bundle_e_sqlite3`), so the decision does bring in one native binary — documented in §5.

## 3. Decision & rationale

Adopt **EF Core 10 with the `Microsoft.EntityFrameworkCore.Sqlite` provider**. Rationale: it is first-party (Microsoft, MIT, on the same monthly LTS patch cadence as the .NET 10 we already adopted), and it directly answers the two hard parts of this problem — a **growing** player-data schema (code-first migrations) and **typed, transactional** access to that schema (LINQ + change tracking + `SaveChanges`/transactions). The repository is literally named `efcore` and the entities (`User`, `Profile`) already exist as plain classes; making them EF entities and introducing a `DbContext` is the lowest-friction path.

On SQLite: chosen for development simplicity — zero setup, single file, no server to run — and because EF Core's provider abstraction keeps the door open. The escalation path is documented: when (not if) a real MMO needs multi-writer concurrency and live queries, swap the provider to PostgreSQL via `Npgsql` (third-party — would trigger its own decision doc). SQLite's single-writer model is fine for a solo-owner project and local dev.

Obligations we take on: every entity gets a proper primary key and value-generation strategy up front; migrations are committed from the first schema change so there is always a clean baseline; all writes go through EF transactions; and the `DbContext` is registered as scoped in DI (already chosen in the ASP.NET Core decision), never a singleton.

### Pros

- First-party at the top level except one package: EF Core and the SQLite provider ride the same LTS patch train as the platform, while `SQLitePCLRaw.bundle_e_sqlite3` is a non-Microsoft package pinned at 3.0.3 that must be kept current manually — EF Core 10.0.10's transitive pin (2.1.11) bundles a vulnerable SQLite (CVE-2025-6965), so the override cannot rely on the EF pin, see §5.
- Migrations solve the schema-evolution problem the game will actually hit — no hand-rolled versioning.
- LINQ + strongly typed entities catch query bugs at compile time; change tracking handles the load-save cycle.
- Provider swap (SQLite → PostgreSQL) is mostly configuration once `Npgsql` is vetted.
- Integrates with the existing DI container with one registration.

### Cons

- Brings a native binary (e_sqlite3) transitively — the first native component in the dependency tree, see §5.
- Change tracking + expression translation add runtime overhead vs raw ADO.NET; irrelevant at idle-game scale, worth remembering.
- Locks us into EF conventions; entities designed around EF are harder to move to another ORM.
- SQLite is single-writer; not the eventual multi-user deployment target (documented escalation path, not a blocker).

## 4. Build-vs-buy

For the *current two-entity surface*, an in-house ADO.NET layer over `Microsoft.Data.Sqlite` is maybe a day or two — which the "build first" rule would normally favor. But the problem statement is explicitly **full game-state persistence**: the schema will grow into dozens of entities with relationships, evolving shape (new columns, renames, data fixes), and transactional multi-entity writes. That changes the calculus: the expensive parts are not the CRUD — they're migrations, query translation, change tracking, and relational mapping across an evolving schema. Hand-rolling a migration framework and a mapping layer for a growing, versioned, player-critical schema is weeks of work with high defect risk, against a first-party package whose marginal cost is effectively zero (it's maintained by the same team as the platform we already run). Dapper + a hand-rolled migration tool is the strongest "buy cheaper" counter-offer, but it still pays the per-entity SQL maintenance tax for no benefit at this project's performance needs. Buy.

## 5. Risk

### Undo risk — medium

EF Core spreads across the data layer (a `DbContext`, entity annotations, migrations, service registrations). Removing it means rewriting the data access and regenerating migrations — real work, but confined to one layer: game logic talks to services, not to the ORM, so nothing else needs to change. The mitigant against a costly un-do is exactly the one we already committed to: keep persistence behind services (as `UserService`/`ProfileService` do) so the ORM never leaks past the data boundary. The provider abstraction means the *database* is cheap to change; the ORM is the sticky part.

### Security risk — low

Top-level packages are first-party Microsoft (MIT, monthly patching on the platform cadence) except one: `SQLitePCLRaw.bundle_e_sqlite3` is a non-Microsoft package (Apache-2.0) explicitly pinned at 3.0.3 to override EF Core 10.0.10's transitive 2.1.11 bundle, which shipped a vulnerable SQLite (CVE-2025-6965, fixed in ≥ 3.50.2). The bundle does not track the platform releases, and a plain EF Core bump can silently fall back to the vulnerable transitive pin — so this override must be verified to stay current on every upgrade. SQLite is among the most audited C codebases in existence, so risk is low, but it is a native third-party component worth that check. Operational obligations: keep the bundle at ≥ 3.0.3, apply platform patches on cadence, and never hand SQL into `ExecuteSqlRaw` from untrusted input (we'll avoid raw SQL entirely — the LINQ surface keeps parameterization automatic).

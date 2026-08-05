# @internationalized/date

- Status: adopted (**declared but currently unused — see section 3**)
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 3.12.3 (declared `^3.12.0`)

## 1. Problem

[bits-ui](./bits-ui.md) declares `@internationalized/date` as a **peer dependency**. Its calendar, date-picker and range-calendar primitives need a date model that JavaScript's built-in `Date` cannot provide: a calendar date without a time or timezone, correct arithmetic across daylight-saving transitions, and support for non-Gregorian calendar systems. Because it is a peer rather than a regular dependency, the consumer — us — must declare it, and the [shadcn-svelte](./shadcn-svelte.md) CLI added it to `package.json` during `init`.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@internationalized/date** (chosen) | 3.12.3, 1.19 MB unpacked, 1 dependency (`@swc/helpers` 0.5.23) | Immutable `CalendarDate` / `CalendarDateTime` / `ZonedDateTime` types that separate "a date" from "an instant"; DST-safe arithmetic; non-Gregorian calendars | Active; Adobe, part of React Spectrum | **Apache-2.0** | Entailed: it is what bits-ui's peer range names, so nothing else satisfies it |
| Omit it | 0 bytes | Leave the peer requirement unmet | n/a | n/a | **Medium: viable today — see section 3** |
| Native `Date` + `Intl` | 0 bytes | Built in | Platform | n/a | Low: cannot satisfy the peer requirement, and `Date` is exactly the thing this library exists to avoid |
| Temporal (TC39) | stage 3 | The standard-track replacement that solves the same problems natively | Shipping across browsers | n/a | Low **today**, high **later** — see section 5 |

Why the others lost: nothing else can satisfy a peer dependency that names this package specifically. `Date` and `Intl` are what the library exists to work around — `Date` conflates instants with calendar dates and mishandles DST arithmetic. Temporal is the genuine long-term answer but is not what bits-ui's peer range accepts.

## 3. Decision & rationale

Adopt **@internationalized/date 3.12.3**, entailed by [bits-ui](./bits-ui.md), with an important qualification recorded plainly:

**It is declared but not used, and it costs the browser nothing today.** No file under `src/` imports it — the vendored [shadcn-svelte](./shadcn-svelte.md) `button` does not touch dates, and neither did either of the two evaluated components (`button`, `dialog`). Because it is a peer dependency rather than a regular one, it is *declared* so package resolution is clean and no unmet-peer warning appears — but nothing imports it, so bundlers never include it and it contributes **zero bytes** to the client. It becomes live only if a calendar, date-picker or range-calendar component is added.

**It could be removed today.** Deleting it would leave an unmet peer warning on install while the build, type-check and bundle all continue to work, since nothing resolves the import. It is kept because the warning is noise that invites someone to "fix" it later by reinstalling the package anyway, and because the cost of keeping it is a `package.json` line and a lockfile entry rather than anything a user downloads. That is a deliberate, low-stakes call and is easy to reverse — if the project decides no date components will ever be used, dropping this is a clean one-line saving.

Two details worth recording. It is **Apache-2.0**, the only Apache-licensed package in the frontend set — permissive and compatible, but it carries an explicit patent grant and a NOTICE-file requirement that MIT does not, which matters if this project's licensing is ever audited. And it is the only member of the [bits-ui](./bits-ui.md) tree with a transitive of its own, `@swc/helpers` 0.5.23 (MIT), a small SWC runtime-helper shim.

### Pros

- Satisfies bits-ui's peer requirement cleanly, so installs are warning-free.
- **Zero bundle cost while unused** — nothing imports it, so nothing ships.
- The right model if date components are ever added: immutable types that distinguish a calendar date from an instant, and arithmetic that survives DST.
- Adobe-maintained as part of React Spectrum; actively developed and widely deployed.
- No install or postinstall scripts; no native binaries.

### Cons

- **Currently unused** — a declared dependency serving no purpose in the running application.
- Apache-2.0: the only non-MIT/ISC/OFL package in the set, with attribution obligations MIT does not impose.
- Brings `@swc/helpers`, the one transitive behind a transitive in this part of the tree.
- 1.19 MB unpacked for capability the project may never exercise.
- Likely to be obsoleted by Temporal — see section 5.

## 4. Build-vs-buy

Not applicable in the usual sense: this is not a capability we chose to acquire, it is a peer requirement of a library we did choose. The real question is *keep or omit*, not *build or buy* — and the honest answer is that omitting it works today and costs only a console warning.

Were the underlying capability ever needed on its own, building it would be firmly out of the question. Timezone-correct date arithmetic, DST transition handling and non-Gregorian calendar support are the canonical example of a problem that looks tractable and is not: the edge cases are political (timezone rules change by legislation) and the correct data lives in the IANA database. That is years of accumulated correctness, not hours.

The realistic in-house alternative is neither building this nor keeping it, but **not needing it** — using native `<input type="date">`, which is fully accessible, localised by the browser and free, instead of a custom calendar component. For an idle game that is very likely the right call, and it is the path that would let this dependency be dropped outright.

## 5. Risk

### Undo risk — low

Nothing imports it, so removing it changes no behaviour and breaks no build — it produces an unmet-peer warning and nothing else. Should date components be adopted later, the risk would rise to match [bits-ui](./bits-ui.md)'s, since those components are built on its types.

### Security risk — low

Apache-2.0, one small MIT transitive (`@swc/helpers`), no install or postinstall scripts, no native binaries, no known CVEs, Adobe-maintained.

Its unusual property is that **an unused dependency has no runtime attack surface at all**: never imported means never bundled and never executed, so a compromised release could only affect the build machine, not players. This is the one browser-adjacent package in the [shadcn-svelte](./shadcn-svelte.md) set whose risk today is effectively build-time only. That changes the moment a date component is added, so this rating should be revisited alongside any such addition.

**Standing note.** The TC39 Temporal API — `Temporal.PlainDate` and friends — solves precisely the problems this library was built to work around, and is reaching stable browser support. When bits-ui migrates to it, or when its peer requirement is dropped, this dependency should be removed rather than carried forward. Worth revisiting whenever [bits-ui](./bits-ui.md) is next updated.

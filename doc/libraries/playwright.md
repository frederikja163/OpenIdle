# Playwright (@playwright/test)

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 1.62.1 (declared `^1.60.0`)

## 1. Problem

Some client behaviour cannot be verified by unit tests: that a page actually renders, that navigation between game screens works, that a WebSocket connection to the C# backend establishes and updates the UI, and that a full production build serves correctly. End-to-end tests drive a real browser against a real build to check those things. For a game client whose entire purpose is a long-lived stateful session driven by server messages, this is the only test category that exercises the real thing.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@playwright/test** (chosen) | 1.62.1; `playwright-core` is 14 MB installed, plus browser binaries downloaded separately (hundreds of MB) | Drives real Chromium, Firefox, and WebKit; auto-waiting removes most flakiness; trace viewer; network interception, which suits testing against a WebSocket backend | Very active: 52M weekly downloads, 3308 releases, latest 2026-07-30, Microsoft | Apache-2.0 | High: the strongest tool in the category, and the only one whose network interception suits a socket-driven client |
| Cypress | 15.x | Mature, good debugging UX | Active | MIT | Low: single-browser-family origins, heavier, slower; Playwright has overtaken it |
| [Vitest](./vitest.md) browser mode | already installed | Component-level browser testing using a runner we already have; no second framework | Active | MIT | **Medium-high**: overlaps at the component level at zero additional dependency cost. Does not cover full-build or cross-page journeys |
| Manual testing | 0 bytes | Nothing to install | n/a | n/a | Low: unrepeatable, and cannot gate CI |
| No e2e tests at all | 0 bytes | Removes the heaviest item in the toolchain | n/a | n/a | Low: leaves the socket-to-UI path — the client's core behaviour — unverified |
| Build in-house | n/a | Exactly our needs | Us | n/a | Low: browser automation is not an hours-not-weeks item |

Why the others lost: Cypress is behind Playwright on the merits. Manual testing cannot gate CI. Vitest browser mode is the one genuine overlap and is addressed below.

## 3. Decision & rationale

Adopt **@playwright/test**, with one configuration defect recorded as a required fix.

The case rests on what this particular client does. An idle MMORPG client is not a set of pages — it is one long-lived session where the UI is driven by a WebSocket stream from the C# backend. The failure modes that matter most are the ones that only appear against a real browser and a real build: a socket that reconnects but leaves stale state on screen, a counter that stops updating after a tab is backgrounded, a production build whose assets resolve differently from dev. Unit tests under [Vitest](./vitest.md) cannot reach any of that. Playwright's network interception is the specific capability that makes a socket-driven client testable — it can hold a connection open, inject server messages, and assert on what the UI does with them.

Playwright is also the right pick within the category rather than merely the default one: auto-waiting is what keeps an e2e suite from becoming flaky enough to be ignored, and the trace viewer is what makes a CI failure debuggable six months later. Both are the difference between a suite that survives and one that gets deleted.

**One configuration defect was found and has been fixed:**

1. **`playwright install` ran on every `bun run test:e2e`.** The script was `playwright install && playwright test`. With no arguments, `playwright install` downloads *all* browser families — Chromium, Firefox, and WebKit — even though `playwright.config.ts` defines no `projects` and therefore exercises only the default Chromium. That was a large download and a hard network dependency on every test run, for browsers that are never launched. `test:e2e` is now `playwright test` alone, and the download lives in a separate `test:e2e:setup` script pinned to `playwright install chromium`, which CI runs and caches once before invoking `test:e2e`.

The overlap with Vitest browser mode is real and worth keeping in view: for component-level assertions, Vitest is already installed and is the cheaper tool. The sensible division is Vitest for components, Playwright for full-build journeys and socket behaviour. If that division is never actually used — if e2e tests end up asserting things a component test could have covered — the case for carrying hundreds of megabytes of browser binaries weakens, and this decision should be revisited.

### Pros

- Best-in-class browser automation: auto-waiting eliminates most of the flakiness that makes e2e suites get ignored.
- Cross-browser (Chromium, Firefox, WebKit) from one API.
- Trace viewer makes CI failures genuinely debuggable, which is what usually determines whether an e2e suite survives.
- Network interception suits a client whose correctness depends on WebSocket traffic from the C# backend.
- Apache-2.0, Microsoft-maintained, 52M weekly downloads, released roughly weekly.
- Only one direct npm dependency (`playwright`), so the *npm* footprint is small — the weight is browser binaries.

### Cons

- Hundreds of megabytes of browser binaries per environment; by far the largest disk cost in the project.
- The browser download is a separate manual step (`test:e2e:setup`), so a fresh checkout that runs `test:e2e` straight away fails until it has been run once — the price of taking it out of the per-run test command.
- Browser binaries are opaque prebuilt executables downloaded outside the npm integrity model — see security risk.
- e2e suites are the highest-maintenance test category: slower than unit tests, more prone to flakiness, and quickest to be ignored once they start failing intermittently.
- Overlaps with [Vitest](./vitest.md) browser mode at the component level, so the two need a clear division of labour to avoid paying twice.

## 4. Build-vs-buy

Not buildable — browser automation means a CDP or WebDriver client, process lifecycle management, and reliable waiting primitives, and Playwright's auto-waiting is the accumulated result of years of flakiness work. There is no in-house version.

To be concrete about the scale: driving a browser means implementing a Chrome DevTools Protocol or WebDriver client, managing browser process lifecycles across platforms, and — the genuinely hard part — reliable waiting primitives. Playwright's auto-waiting exists because everyone who has built e2e tooling has learned that `sleep`-based waits produce suites so flaky they get abandoned. That is years of accumulated fixes, not a feature we could approximate.

The realistic in-house alternative is narrower: use [Vitest](./vitest.md) browser mode alone, which is already installed, and accept that full-build and multi-page journeys go untested. That is a legitimate lighter-weight position and the main reason this document flags the Vitest overlap as something to keep watching. It loses today because the socket-to-UI path — the client's core behaviour — is exactly what component-level testing cannot reach.

## 5. Risk

### Undo risk — low

Confined to `playwright.config.ts`, the `test:e2e` script, and files matching `**/*.e2e.ts`. No application code imports it, and nothing that ships depends on it. The cost of removal is proportional to how many e2e tests exist at the time — the tests themselves would be lost, but no production or application code would need to change.

### Security risk — medium

Rated above `low` because of the browser binaries, not the npm package. `playwright install` downloads prebuilt browser executables from a Microsoft CDN at test time. These are large opaque binaries fetched outside npm's integrity-hash model and not recorded in `bun.lock`, so the reproducibility and supply-chain guarantees that cover every other dependency here do not apply to them. They are then executed locally.

Mitigating factors: the source is Microsoft's official distribution over HTTPS, Playwright is Apache-2.0 with no known outstanding CVEs, the npm package itself has one direct dependency and no install or postinstall scripts, and everything is development-only — nothing reaches production or the player's browser. Chromium and Firefox also receive prompt upstream security patches, so the binaries are not stale.

Required handling, now in place: the download is pinned to `chromium` only and lives in an explicit `test:e2e:setup` script rather than inside `test:e2e`, so CI can run and cache it once instead of re-downloading on every run. That holds both the exposure window and the download surface to the one browser actually launched.

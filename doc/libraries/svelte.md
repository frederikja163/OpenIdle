# Svelte

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 5.56.8 (declared `^5.56.1`)

## 1. Problem

The game needs a browser client. An idle MMORPG UI is a dense dashboard of numbers that change constantly — resource counters, progress bars, inventory grids, skill levels — driven by a stream of state updates arriving over WebSockets from the C# backend. Hand-writing DOM updates for hundreds of frequently-changing values is the exact kind of tedious, bug-prone code we do not want to own. We need something that maps game state to DOM and keeps them in sync, without shipping a large runtime to the player's browser.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **Svelte 5** (chosen) | 5.56.8, 3.6 MB installed, 16 direct deps; compiles away — minimal runtime shipped | Compiler, not a runtime library: components compile to direct DOM operations. Runes (`$state`, `$derived`) give fine-grained reactivity without a virtual DOM. Component-scoped CSS built in — no styling library needed. Single-file components | Very active: 5.3M weekly downloads, 1089 releases, latest 2026-07-24, Svelte core team + Vercel backing | MIT | High: fine-grained updates suit a dashboard of many independently-changing counters; scoped CSS removes a whole dependency category |
| React | 19.x, ~6 MB installed + react-dom | Largest ecosystem and hiring pool; huge component library selection | Very active (Meta) | MIT | Medium: virtual-DOM diffing is the wrong shape for hundreds of tiny independent updates; needs a state library and a styling story on top, so it costs more dependencies, not fewer |
| Vue 3 | 3.x, ~4 MB | SFCs and scoped CSS like Svelte; fine-grained reactivity via proxies; gentle learning curve | Very active | MIT | Medium: genuinely close on the merits. Loses on runtime size and on the reactivity model being proxy-based rather than compile-time |
| SolidJS | 1.x, ~1 MB | Truly fine-grained signals, no virtual DOM, very small runtime — arguably the best raw fit for high-frequency counter updates | Active but much smaller community | MIT | Medium: technically excellent, ecosystem and documentation depth are a real cost for a solo owner |
| Vanilla JS / Web Components | 0 bytes | Zero dependencies. Total control | n/a | n/a | Low: see build-vs-buy — this is weeks of work reinventing reactivity, and the result would be worse |
| Build in-house (own reactivity + templating layer) | n/a | Exactly our requirements, nothing more | Us, forever | n/a | Low: this is a framework, not a feature. Months, not hours |

Why the others lost: React is the default choice for the wrong reasons here — its diffing model is a poor fit for many small independent updates, and it drags in satellite dependencies (state management, styling) that Svelte covers natively, which conflicts with this project's few-dependencies principle. Vue and Solid are both defensible; Svelte wins on compile-time reactivity plus built-in scoped CSS. Vanilla JS loses on effort, not on principle.

## 3. Decision & rationale

Adopt **Svelte 5** with runes mode forced project-wide (see `vite.config.ts`, which sets `runes: true` for all non-`node_modules` files). Svelte is a compiler: components become plain DOM instructions at build time, so the framework largely does not exist at runtime. For an idle game whose UI is a large number of small values updating on a tick, fine-grained compile-time reactivity is the right model — updating one counter touches one text node, not a diff of a component tree.

The secondary reason matters as much for this project: Svelte's built-in component-scoped CSS means the "how do we style things" question has a zero-dependency answer. Frameworks that lack this push you toward a CSS library, and every such library is another decision document.

### Pros

- Compile-time reactivity: no virtual DOM, no diffing cost on high-frequency counter updates.
- Very small shipped runtime — matters for a game players leave open in a background tab.
- Component-scoped CSS is built in, removing the need for a separate styling dependency.
- Runes (`$state`/`$derived`/`$effect`) are explicit and readable; closer to the owner's C# mental model than hook rules.
- MIT, 5.3M weekly downloads, active core team, frequent releases.
- Single-file components keep markup, logic, and styles co-located — good for a solo maintainer.

### Cons

- 16 direct dependencies (acorn, magic-string, zimmerframe, esrap, etc.) — a real compiler toolchain, not a small package. All build-time, none shipped to the browser.
- Smaller ecosystem than React: fewer off-the-shelf components, which for this project is arguably a feature (we build our own) but is a genuine cost when we need something non-trivial.
- Svelte 5 runes were a significant break from Svelte 4 idioms; older tutorials and Stack Overflow answers are frequently wrong. Expect to read the official docs, not blog posts.
- The compiler is load-bearing: a Svelte bug is a bug we cannot route around in application code.
- No type sharing with the C# backend — DTOs must be duplicated or generated. This is a known consequence of the [C# / .NET](./csharp-dotnet.md) decision, not of Svelte.

## 4. Build-vs-buy

Not a real build candidate. A reactivity system plus a template compiler is a framework — months of work, and the outcome would be a worse Svelte with no documentation and one maintainer. The honest in-house alternative is not "build a framework" but "write vanilla DOM code with no framework at all", which is viable for a small UI and becomes untenable at idle-MMO dashboard scale — dozens of live values, inventory grids, modals, and routing. We accept a framework here precisely because it is the category of problem that does not decompose into hours-not-weeks of work.

Note that this decision deliberately does **not** license a component library, a state-management library, or a CSS-in-JS library on top. Svelte covers state and styling natively. Anything further needs its own document.

## 5. Risk

### Undo risk — high

The UI framework is the most load-bearing frontend choice. Every `.svelte` file, the reactivity idioms inside them, and the routing layer above ([SvelteKit](./sveltekit.md)) are all coupled to it. Swapping to React or Vue later is a full client rewrite, not a refactor. Mitigants: the client is greenfield, so the coupling is all ahead of us rather than behind us, and the server is a separate process behind an HTTP/WebSocket boundary, so no backend work is at risk. The rewrite would be expensive but contained and would lose no player data.

### Security risk — low

Svelte is build-time tooling; almost nothing of it reaches the browser. No known outstanding CVEs. 3 npm maintainers on a package with 5.3M weekly downloads is a modest bus/compromise factor, but the project is backed by an active core team with corporate sponsorship and ships releases weekly. The relevant application-level risk is not Svelte itself but `{@html ...}`, which bypasses escaping — treat any use of it against server- or player-supplied strings as an XSS hole. Svelte escapes interpolated values by default, so the safe path is the default path.

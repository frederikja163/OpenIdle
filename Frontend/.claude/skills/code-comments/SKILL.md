---
name: code-comments
description: When to write code comments
---

# Code comments

Comments are allowed when they earn their place. Follow these rules.

1. **Comments should not duplicate the code.** If a comment only restates what the
   adjacent code already says, delete it.
2. **Good comments do not excuse unclear code.** Prefer clearer names and structure
   over a comment that explains a tangle.
3. **If you can't write a clear comment, there may be a problem with the code.**
   Treat the difficulty as a signal to revisit the code.
4. **Comments should dispel confusion, not cause it.** Remove comments that are
   stale, vague, or contradict the code.
5. **Explain unidiomatic code in comments.** When code has to be surprising — a
   platform quirk, a workaround, a non-obvious decision — say why.
6. **Provide links to the original source of copied code.**
7. **Include links to external references where they will be most helpful.**
8. **Use comments to mark incomplete implementations, and start with `TODO:`**.

When reviewing or editing, remove comments that violate rules 1–4 and keep the ones
that serve rules 5–8.

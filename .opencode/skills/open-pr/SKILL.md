---
name: open-pr
description: Use when opening a pull request for the current branch - trigger words include "open a PR", "create a pull request", "raise a PR", "put this up for review". Covers the whole shape of one here - current branch against main, a description opening with "Closes #<id>", a short bullet summary, and a table of lines changed split across docs, devops, generators, backend, backend tests, frontend and frontend tests. For writing the commits that go into it, use the committing skill instead.
---

# Opening a pull request

One PR per branch, opened with `gh pr create` from the branch you are on, against `main`
unless told otherwise. Never open one from `main` itself — branch and move the commits first.

## 1. Find the issue number

The description opens with `Closes #<id>`, so the number has to be right. In order:

1. The number passed when the skill was invoked (`/open-pr 82`).
2. The number in the branch name — `feat/46-show-versions` → `46`, `fix/7-gh-workflows` → `7`.
3. Otherwise **ask**. Do not infer one from the diff, and do not open the PR without the line.

## 2. Read the branch before describing it

The PR describes the branch, not the last commit:

```shell
git fetch origin main --quiet
git log --oneline origin/main..HEAD
git diff --stat origin/main...HEAD
```

Push first, so the PR has something to point at: `git push -u origin HEAD`.

## 3. The description

Exactly this shape, in this order, and nothing else:

```markdown
Closes #82

- Fetch the debug console's request catalogue from the backend's `GET /schema`
- Drop the generator's unported `TsSchema` target and the build steps that called it
- Cover the mapping with unit tests and the console with an e2e stub

### Lines changed

| Area | Added | Removed |
| --- | ---: | ---: |
| docs | +14 | -17 |
| frontend | +466 | -61 |
| frontend tests | +615 | -1 |
| **total** | **+1095** | **-79** |
```

The bullets are a *summary*, not a changelog: one line per change a reviewer would care
about, three to six of them, imperative mood, no sub-bullets and no paragraph underneath.
If a bullet needs a clause explaining why, the why belongs in a code comment or a doc.
Leave out anything the table already says — file counts, line counts, which directories moved.

## 4. Build the table

Run this from the repository root. It buckets every changed file into one area, and the
total reconciles with `git diff --shortstat --no-renames`, so a row that looks wrong is a
mapping to fix rather than a number to edit by hand.

`--no-renames` is load-bearing: with rename detection `--numstat` prints the path as
`{old => new}/file`, which has spaces in it, so `$3` stops being a path and the file lands
in `other`. Listing each side separately also puts a file moved between areas on the right
two rows — removed from where it left, added where it arrived.

```shell
git diff --numstat --no-renames "$(git merge-base origin/main HEAD)"..HEAD | awk '
{
  add = $1; del = $2; f = $3
  if (add == "-") { add = 0; del = 0 }   # binary file
  if      (f ~ /^doc\// || f ~ /\.md$/)                            a = "docs"
  else if (f ~ /^\.github\// || f ~ /^deploy\// || f ~ /Dockerfile/ \
        || f ~ /^\.dockerignore$/ || f ~ /docker-compose/)         a = "devops"
  else if (f ~ /^Generators\// || f == "types.xml")                a = "generators"
  else if (f ~ /^tests\//)                                         a = "backend tests"
  else if (f ~ /^Backend\//)                                       a = "backend"
  else if (f ~ /^Frontend\/e2e\// || f ~ /\.(spec|test)\.[tj]s$/)  a = "frontend tests"
  else if (f ~ /^Frontend\//)                                      a = "frontend"
  else                                                             a = "other"
  A[a] += add; D[a] += del; TA += add; TD += del
}
END {
  split("docs,devops,generators,backend,backend tests,frontend,frontend tests,other", order, ",")
  for (i = 1; i <= 8; i++) { k = order[i]; if (k in A) printf "| %s | +%d | -%d |\n", k, A[k], D[k] }
  printf "| **total** | **+%d** | **-%d** |\n", TA, TD
}'
```

Keep the rows in that order and drop the ones with no changes. The mapping, where a path
could plausibly land in two areas:

| Area | Holds |
| --- | --- |
| docs | `doc/`, and every `.md` anywhere — including `Frontend/README.md` |
| devops | `.github/`, `deploy/`, any `Dockerfile`, `.dockerignore`, compose files |
| generators | `Generators/`, and `types.xml` — the contract belongs with what parses it |
| backend | `Backend/` |
| backend tests | `tests/` — both the unit and the integration projects |
| frontend | `Frontend/`, minus its tests |
| frontend tests | `Frontend/e2e/`, and every `*.spec.ts` / `*.test.ts` |
| other | anything left, e.g. `OpenIdle.slnx`. A row here usually means the mapping needs a case |

## 5. Open it

Write the body to a file and hand `gh` the path — a heredoc mangles the markdown, and a
`--body` string mangles the newlines:

```shell
gh pr create --base main --head "$(git branch --show-current)" \
  --title "<terse, like a commit subject>" --body-file <path>
```

The title follows the commit convention without the type prefix — `Host the schema for the
debug console`, not `feat: …` and not `Schema hosting frontend`. Report the URL `gh` prints.

Do not mark it draft unless asked, do not add reviewers or labels the user did not ask for,
and do not merge it.

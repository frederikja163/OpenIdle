---
name: consult-docs
description: Use at the START of any coding or research task in the OpenIdle repository, before reading or editing code. This is the documentation bootstrap — it tells you to read doc/README.md (the documentation table of contents) first, then open every doc/ file relevant to your task and confirm its claims against source. Trigger words include "add a dto", "types.xml", "new socket endpoint", "socket request", "add a controller", "add an http endpoint", "new route", "backend", "how does X work", "library", "dependency". Applies to any task that will modify code, types.xml, or docs under doc/.
---

# Consult the project documentation first

This repository keeps its knowledge in `doc/`. Before you read or change any code, load the relevant documentation. This skill makes that a habit.

## Why

- `doc/README.md` is the single index of all project documentation. It maps tasks to documents, so you do not have to rediscover the architecture.
- Critical rules live in prose, not code: the DTO contract grammar, the endpoint discovery constraints, the library decisions and their security standing notes. Code alone will not tell you them.
- The docs cite exact `file:line` locations; follow them instead of guessing.

## Procedure

### Step 1 — Read the index

Read `doc/README.md` first, in full. It contains the documentation map, the task→document lookup table, and the repository layout.

### Step 2 — Read the relevant documents

Using the task lookup table in `doc/README.md`, open every document that matches your task **in full** — do not stop at the first section. The table maps common tasks to documents:

| Task | Documents to read first |
|---|---|
| Add a socket request/response/event payload | `doc/backend/dto-contract.md`, `doc/libraries/dto-xml-contract.md` |
| Add a socket endpoint handler | `doc/backend/socket-endpoints.md`, `doc/backend/dto-contract.md` |
| Add an HTTP endpoint | `doc/backend/http-endpoints.md` |
| Understand the socket request pipeline | `doc/backend/socket-endpoints.md`, `doc/backend/dto-contract.md` |
| Add/change an EF entity or migration | `doc/libraries/ef-core.md` |
| Add/evaluate/replace a third-party dependency | `doc/libraries/README.md`, `doc/libraries/TEMPLATE.md` |
| Frontend work (components, styling, tests) | `doc/libraries/README.md`, then the decision doc for the affected area |
| Security / dependency audit | `doc/libraries/README.md` (Open items + Standing notes) |

If the task does not match a row, read `doc/backend/dto-contract.md` and `doc/backend/socket-endpoints.md` anyway when the task touches the backend, and `doc/libraries/README.md` when it touches dependencies.

### Step 3 — Confirm against source

Documentation can drift from code. After reading, open the source files the documents cite (the `file:line` references) and confirm the claims you rely on before writing any code. If you find a discrepancy, follow the code and flag the outdated document to the user.

### Step 4 — Write the code

Implement against the confirmed rules, then follow the document's verification section (usually `dotnet build Backend\Backend.csproj` and possibly the generator CLI).

## Rules

- Never start implementing a backend, DTO, or dependency change without first reading the matching documents in Step 2.
- Always read the full document body, not just the table of contents.
- When a doc disagrees with code, the code wins — but report the drift.
- When you create or update a document, register it in `doc/README.md` (map + task table) and keep the library summary there in sync with `doc/libraries/README.md`.

# Version Control And Changelog Rules

Use this when changing player-facing behavior, build flow, saves, UI, networking, or milestone
docs.

## Current Version

`0.1.0` - Project Setup

## Versioning

- Stay in `0.x` while the game is unpublished.
- Do not create a new version for every change.
- Use `0.x.0` for meaningful internal milestones.
- Use `0.x.y` only for fixes to a milestone.
- Reserve `1.0.0` for a real public launch candidate.

## Changelog

- Keep `Development/CHANGELOG.md` updated.
- Write public-facing entries: plain, professional, and easy for testers to understand.
- Do not write changelog entries that sound generated, promotional, or overly detailed.
- Do not list every file touched.
- Do not mention AI agents or internal tool execution.
- For gameplay, UI, save, or networking changes, update the current changelog entry in the same
  task.

## Before Finishing Work

Ask:

- Did this affect players, testers, saves, UI, networking, tools, or build flow?
- If yes, update `Development/CHANGELOG.md`.
- If it is only internal docs or tooling, changelog is optional unless it changes the development
  process.

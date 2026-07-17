# Multiplayer

Networking model for Orion SCP RP. Read before UI, networking, or scene flow work. Delete this
doc if the project is single-player.

## Model

TODO: host-authoritative? dedicated servers? lobby flow? which scene owns the network session?

## Canonical Gotchas

The shared networking gotchas doc lives at
`C:\Users\Jordan\Documents\s&box projects\1AI DEEP RESEARCH\SBox-Packages\Multiplayer\README.md`.
Key ones that bite every project:

- `[Sync]`, `[Sync(SyncFlags.FromHost)]`, `[Rpc.Broadcast]`, `[Rpc.Host]`, `[Rpc.Owner]` are
  current. `[Broadcast]` and `[Authority]` are obsolete.
- `Networking.IsHost` is true offline too. "Am I in a real session" is `Networking.IsActive`.
- After `NetworkSpawn()`, added components, children, and enable-toggles do not replicate until
  `Network.Refresh()`. Build the whole rig, then spawn once.

## Required Scene Objects

TODO: which networked objects each scene must contain.

## Smoke Test

Never ship UI or networking changes without a client smoke test: host plus one joined client,
exercise the changed flow on both.

# Free Camera — Workshop publishing

This mod is **not yet published** (`publishedfileid` = TBD). The publishing flow mirrors PerkOracle's.

## Pack the content folder

```powershell
pwsh -File workshop/pack-dist.ps1
```

Builds Release and assembles `workshop/Dist/` = `FreeCamera.dll` + `meta.json` (+ `Assets/` if present).
`Dist/` is gitignored.

## Publish (first time / updates)

The SteamworksPy headless publisher used by PerkOracle is not bundled in this repo yet. To publish:

1. Provision the publisher tooling from PerkOracle (`PerkOracle/workshop/steamugc/`) — the native deps
   (`steam_api64.dll`, `SteamworksPy64.dll`, `steamworks/`, `steam_appid.txt` = `839770`) are
   environment-local and gitignored; re-provision per the PerkOracle `docs/OPERATIONS.md` prerequisites.
2. With the Steam client running and logged in as the owner, run the publisher pointed at this repo's
   `workshop/Dist/`. The **first** publish creates the item and yields a `publishedfileid` — record it in
   `CLAUDE.md`, `docs/OPERATIONS.md`, and `workshop/steamugc/published_id.txt`.

Steam appid: **839770**. See `docs/OPERATIONS.md` for the full runbook (placeholders until first publish).

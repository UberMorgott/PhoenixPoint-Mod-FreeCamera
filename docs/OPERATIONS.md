# Free Camera — Agent Operations Runbook

Agent-facing runbook for routine Steam Workshop operations on the **Free Camera** Phoenix Point mod.
Mirrors PerkOracle's runbook structure. **Free Camera is not yet published** — several identity values
below are placeholders (`TBD`) until the first publish creates the Workshop item.

CWD assumption: the mod repo root `E:\DEV\PhoenixPoint\FreeCamera`.

---

## Identity & paths

| Thing | Value |
|---|---|
| Workshop publishedfileid | **TBD** (set on first publish) |
| Steam appid (Phoenix Point) | **839770** |
| Item URL | `https://steamcommunity.com/sharedfiles/filedetails/?id=<publishedfileid>` (TBD) |
| Owner SteamID64 (Morgott) | **76561197996210591** |
| Repo path | `E:\DEV\PhoenixPoint\FreeCamera` |
| Remote (origin, branch main) | **TBD** |
| Build/pack script | `workshop/pack-dist.ps1` |
| Assembly / mod ID | `FreeCamera.dll` / `Morgott.FreeCamera` |

Suggested Workshop tags: **`["Gameplay", "Tactical"]`** (valid PP tags: Geoscape, Tactical, Difficulty,
Gameplay, Bionics, Mutations).

---

## Build & deploy locally

```powershell
# Build the mod assembly (Release, against the real game DLLs):
dotnet build -c Release

# Run the pure-math unit tests:
dotnet test tests/FreeCamera.Tests.csproj

# Build + copy into the live game Mods folder for in-game testing:
pwsh -File deploy.ps1
```

---

## Task: Publish / update the mod

> The headless SteamworksPy publisher is **not bundled** in this repo yet (see `workshop/WORKSHOP.md`).
> Provision it from PerkOracle (`PerkOracle/workshop/steamugc/`) before the first publish.

1. Pack the clean content folder (`workshop/Dist/` = `FreeCamera.dll` + `meta.json`):
   ```powershell
   pwsh -File workshop/pack-dist.ps1
   ```
   Throws on build failure — do not proceed if it errors.

2. With the **Steam client running and logged in as the owner** (Morgott, 76561197996210591), run the
   provisioned publisher pointed at `workshop/Dist/`.
   - **First publish** creates the item and prints a new `publishedfileid`. Record it in this file,
     `CLAUDE.md`, and `workshop/steamugc/published_id.txt`.
   - **Updates** re-upload `Dist/` to the existing item. Block until the publisher reports `EResult.OK`.

3. Before a versioned publish, bump `<Version>` in `FreeCamera.csproj` and `"Version"` in `meta.json`
   so they agree on `vX.Y.Z`, then commit.

---

## Prerequisites (before any publish)

1. Steam desktop client running and logged in as the owner account (publisher rides the active session,
   no password).
2. Native publisher deps are gitignored and must be re-provisioned on a fresh clone (mirror PerkOracle's
   `docs/OPERATIONS.md` prerequisites): `steamworks/`, `SteamworksPy64.dll`, a `steam_api64.dll` that
   exports `SteamInternal_SteamAPI_Init` (SDK ≥ ~1.57), and `steam_appid.txt` containing `839770`.

---

## Notes

- **Gallery images** and **comment posting** are web-only steps (no official write API) — drive them via
  Playwright on the logged-in Steam session, same as PerkOracle.
- Keep store-description files < 8000 UTF-8 **bytes** each if/when localized descriptions are added.
- After any change (code, description, tags, repo images), commit; push once a remote exists.

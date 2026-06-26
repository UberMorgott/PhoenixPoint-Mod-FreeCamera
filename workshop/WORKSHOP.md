# Free Camera - Workshop publishing

End-to-end guide for publishing and updating Free Camera on the Steam Workshop
for **Phoenix Point** (appid **839770**). The headless SteamworksPy publisher is
bundled in `workshop/steamugc/` (mirrors PerkOracle's proven setup).

> **Not yet published** - the first publish creates the item and assigns the
> `publishedfileid`. The full, command-by-command runbook lives in
> [`../docs/OPERATIONS.md`](../docs/OPERATIONS.md); this file is the overview.

## 0. What gets uploaded

A Phoenix Point Workshop item is just a plain folder containing:

```
FreeCamera.dll
meta.json
Assets/
```

`workshop/pack-dist.ps1` assembles exactly this into `workshop/Dist/`
(gitignored). `meta.json` already matches the required PP schema (Id/AssemblyName/
Version/Dependencies + localized Author/Name/Description arrays).

## 1. Pack the content folder

```powershell
pwsh -File workshop/pack-dist.ps1
```

Builds Release and assembles `workshop/Dist/` = `FreeCamera.dll` + `meta.json`
(+ `Assets/` if present).

## 2. First publish (create) - headless via SteamworksPy

With the Steam client running and logged in as the owner (Morgott,
76561197996210591):

```powershell
python workshop/steamugc/publish_ugc.py --create --changenote "v1.0.0 initial release" --visibility public
```

This creates the item, uploads `workshop/Dist/`, sets title/description/preview/
visibility/tags, then prints the new `publishedfileid` and writes it to
`workshop/steamugc/published_id.txt` + stamps it into `workshop/freecamera.vdf`.
Record the id in `CLAUDE.md` and `docs/OPERATIONS.md`.

> One-time native-dep setup (git-ignored): see `workshop/steamugc/README.md`.
> The deps (`steam_api64.dll`, `SteamworksPy64.dll`, `steamworks/`,
> `steam_appid.txt` = `839770`) are already provisioned in this checkout.

## 3. Updates

```powershell
pwsh -File workshop/pack-dist.ps1
python workshop/steamugc/publish_ugc.py --update --item <publishedfileid> --changenote "vX.Y.Z - what changed"
```

Store-description-only edits (no build) use `--localize-descriptions`; see the
runbook.

## 4. SteamCMD fallback

`workshop/freecamera.vdf` is a SteamCMD build descriptor (the id is stamped in on
first publish). It is a fallback path only; the SteamworksPy publisher above is
the recommended route.

## 5. Store page content

- **Description** - 8 BBCode files in `workshop/locale/` (english is the Steam
  fallback), pushed by `--localize-descriptions`. BBCode, not Markdown; no
  `[color]`; no em/en dashes; each file < 8000 UTF-8 bytes.
- **Preview image** - `image/steam_preview.jpg` (<= 1 MB), set headlessly.
- **Gallery screenshots** - web-only step (the binding lacks `AddItemPreviewFile`).

## 6. Comments workflow

- **Read** (no login): `python workshop/comments/read_comments.py --owner <SteamID64> --item <publishedfileid> --count 50`
  (`pip install -r workshop/comments/requirements.txt` once).
- **Draft** EN/RU replies: see `workshop/comments/draft_replies.md`.
- **Post**: manually in the browser (recommended). `post_comment.py` is
  experimental, unofficial, and ToS-risky.

See [`../docs/OPERATIONS.md`](../docs/OPERATIONS.md) for the full runbook.

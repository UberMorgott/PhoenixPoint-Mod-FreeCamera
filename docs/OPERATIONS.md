# Free Camera - Agent Operations Runbook

Agent-facing runbook for routine Steam Workshop operations on the **Free Camera**
Phoenix Point mod. Every command below is verified against the actual scripts in
`workshop/`. Execute these directly for routine publish / update / description /
tags / gallery / comments tasks - **no clarifying questions needed**.

All commands assume **CWD = the mod repo root** `E:\DEV\PhoenixPoint\FreeCamera`.
(If a session starts at the outer monorepo `E:\DEV\PhoenixPoint`, prefix paths
with `FreeCamera\` or `cd` into the repo first.)

> **Free Camera is not yet published.** The first publish (`--create`) creates the
> Workshop item and assigns the `publishedfileid`; the script then writes it to
> `workshop/steamugc/published_id.txt` and stamps it into `workshop/freecamera.vdf`.
> Record that id here (and in `CLAUDE.md`) once it exists. Everywhere below that
> says `<publishedfileid>`, use the real id from the first publish.

---

## Identity & paths

| Thing | Value |
|---|---|
| Workshop publishedfileid | **not yet published** (set on first `--create`) |
| Steam appid (Phoenix Point) | **839770** |
| Item URL | `https://steamcommunity.com/sharedfiles/filedetails/?id=<publishedfileid>` |
| Owner SteamID64 (Morgott) | **76561197996210591** |
| Repo path | `E:\DEV\PhoenixPoint\FreeCamera` |
| Remote (origin, branch main) | <https://github.com/UberMorgott/PhoenixPoint-Mod-FreeCamera> |
| Mod ID / assembly | `Morgott.FreeCamera` / `FreeCamera.dll` |
| Publisher script | `workshop/steamugc/publish_ugc.py` |
| Build/pack script | `workshop/pack-dist.ps1` |
| Locale descriptions | `workshop/locale/description.<lang>.txt` (8 languages) |
| SteamCMD descriptor | `workshop/freecamera.vdf` (fallback path) |
| Comment reader | `workshop/comments/read_comments.py` |
| Preview image (square) | `image/steam_preview.jpg` (set headlessly; <= 1 MB) |

Workshop tags: **`["Quality of Life", "Gameplay", "Tactical"]`** (hardcoded in
`publish_ugc.py` as `WORKSHOP_TAGS`).
Locale order pushed: **english, russian, german, french, spanish, italian,
polish, schinese** (Steam shows each viewer their client-language description;
**english is the fallback**).

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

## Publish: two DIFFERENT operations - do not confuse them

> **`--localize-descriptions` does NOT publish the mod build.** It only edits the
> store description text (+ tags). To ship a **new version** that subscribers
> download, you MUST run **`--update`** (it uploads packed `workshop/Dist/`). If
> subscribers don't see the new version, you ran descriptions-only - rerun
> `--update`.

| Goal | Command | What it uploads | Verify |
|---|---|---|---|
| **First publish (create the item)** | `pwsh -File workshop/pack-dist.ps1` then `python workshop/steamugc/publish_ugc.py --create --changenote "v1.0.0 initial release" --visibility public` | Creates item, then uploads packed `workshop/Dist/` (FreeCamera.dll + meta.json + Assets) + title/description/preview/visibility/tags | `[create] OK -> publishedfileid=...` and `EResult.OK`; record the new id |
| **Publish a NEW VERSION (build/content)** | `pwsh -File workshop/pack-dist.ps1` then `python workshop/steamugc/publish_ugc.py --update --item <publishedfileid> --changenote "vX.Y.Z - ..."` | Packed `workshop/Dist/` + title/description/preview/visibility/tags | `[update] OK -> upload committed` and `SubmitItemUpdate ... EResult.OK`; item **"Last updated"** changes |
| **Update DESCRIPTION / tags ONLY (no build)** | `python workshop/steamugc/publish_ugc.py --localize-descriptions --item <publishedfileid> --changenote "..."` | Only `workshop/locale/description.<lang>.txt` per language (+ tags on english pass) - **NOT** the build/content/preview/title | Per-language `EResult.OK` table; **"Last updated"** does NOT change |

Before a NEW VERSION publish, bump the version in **`meta.json`** (repo root -
`pack-dist.ps1` copies this into `Dist/`) and the `FreeCamera.csproj` `<Version>`
so the changenote and `meta.json` agree on `vX.Y.Z`.

---

## Quick command cheat-sheet

```powershell
# FIRST publish (create the item) - pack, then create:
pwsh -File workshop/pack-dist.ps1
python workshop/steamugc/publish_ugc.py --create --changenote "v1.0.0 initial release" --visibility public

# UPDATE the mod (new code/build) - rebuild Dist, then upload:
pwsh -File workshop/pack-dist.ps1
python workshop/steamugc/publish_ugc.py --update --item <publishedfileid> --changenote "<what changed>"

# Push localized store descriptions for all 8 languages (also re-applies tags):
python workshop/steamugc/publish_ugc.py --localize-descriptions --item <publishedfileid> --changenote "<note>"

# READ Workshop comments (no login):
python workshop/comments/read_comments.py --owner 76561197996210591 --item <publishedfileid> --count 50

# After ANY change, commit the mod repo (push once the change is ready):
git -C E:\DEV\PhoenixPoint\FreeCamera add -A
git -C E:\DEV\PhoenixPoint\FreeCamera commit -m "<message>"
git -C E:\DEV\PhoenixPoint\FreeCamera push origin main
```

---

## Prerequisites (CRITICAL - read before any publish)

1. **Steam desktop client must be RUNNING and LOGGED IN as the owner account
   (Morgott, SteamID64 76561197996210591).** The publisher is headless and rides
   the active Steam session - **no username/password is used** (same auth model
   as the official PPWorkshopTool). If Steam is closed or logged into another
   account, the publish will bind to the wrong user or fail to initialize.

2. **Native deps in `workshop/steamugc/` are git-ignored** and must be
   re-provisioned on a fresh clone. Required local-only files:
   - `steamworks/` - the SteamworksPy python package, copied from
     <https://github.com/philippj/SteamworksPy> (the repo's `steamworks/` folder).
   - `SteamworksPy64.dll` - native ctypes shim, from that repo's
     `redist/windows/SteamworksPy64.dll` (built against Steamworks SDK 1.64).
   - `steam_api64.dll` - **must export `SteamInternal_SteamAPI_Init`**
     (Steamworks SDK ~1.57 or newer). Phoenix Point's own bundled
     `steam_api64.dll` is **too old** -> fails with `WinError 127`
     (missing `SteamInternal_SteamAPI_Init`). A known-working DLL was sourced
     from **Slay the Spire 2**
     (`...\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\steam_api64.dll`).
   - `steam_appid.txt` - a file containing exactly `839770` (binds the API to PP).

   (These four are already provisioned in this checkout, copied from PerkOracle's
   working setup.)

3. **Smoke test** the binding before a real publish:
   ```powershell
   python workshop/steamugc/init_test.py
   ```
   It should report `appid=839770` and the logged-in user. If it errors, fix the
   deps above before proceeding.

4. **Python read-comments deps** (one-time): `pip install -r workshop/comments/requirements.txt`
   (`requests`, `beautifulsoup4`).

---

## Task: First publish (create the Workshop item)

This mod is not yet published, so the very first publish uses `--create`.

1. Rebuild the clean content folder (`workshop/Dist/` = `FreeCamera.dll` +
   `meta.json` + `Assets/`):
   ```powershell
   pwsh -File workshop/pack-dist.ps1
   ```
   It runs `dotnet build -c Release` then assembles `Dist/`. It throws if the
   build fails - do not proceed if it errors.

2. Create the item and upload (blocks until upload commits):
   ```powershell
   python workshop/steamugc/publish_ugc.py --create --changenote "v1.0.0 initial release" --visibility public
   ```
   - On success it prints `[create] OK -> publishedfileid=<id>` and the item URL,
     writes `workshop/steamugc/published_id.txt`, and stamps the id into
     `workshop/freecamera.vdf`.
   - If it reports the **workshop legal agreement** flag, open the item URL once
     in the Steam client / browser and accept it (one-time per account).

3. **Record the new `publishedfileid`** in this file's Identity table and in
   `CLAUDE.md`.

4. Push the 8 localized descriptions (sets per-language text + tags):
   ```powershell
   python workshop/steamugc/publish_ugc.py --localize-descriptions --item <publishedfileid> --changenote "v1.0.0 localized descriptions"
   ```

5. Commit + push the repo (see git block above).

---

## Task: Update the mod (new code / new build)

Trigger phrases: "update the mod", "publish a new build".

1. Rebuild `workshop/Dist/`:
   ```powershell
   pwsh -File workshop/pack-dist.ps1
   ```

2. Upload to the existing Workshop item (blocks until upload commits):
   ```powershell
   python workshop/steamugc/publish_ugc.py --update --item <publishedfileid> --changenote "<what changed>"
   ```
   - Sets title/description (english fallback)/content/preview/visibility/tags,
     then `SubmitItemUpdate`. **Block until it prints `EResult.OK`**. On failure
     it raises `SystemExit` with the `EResult` code.
   - Default `--visibility` is `public`; pass `--visibility friends|private` only
     if explicitly requested.

3. Commit + push the repo.

---

## Task: Edit / localize the store description

Trigger phrases: "update the description", "localize".

> **WARNING - this is DESCRIPTION-ONLY, not a build publish.**
> `--localize-descriptions` edits only `workshop/locale/description.<lang>.txt`
> (+ tags); it does **NOT** upload the mod build/content.

1. Edit the relevant file(s) in `workshop/locale/`:
   `description.english.txt`, `.russian.txt`, `.german.txt`, `.french.txt`,
   `.spanish.txt`, `.italian.txt`, `.polish.txt`, `.schinese.txt`.
   Rules:
   - **BBCode**, not Markdown. **No `[color]`** (unsupported by Steam).
   - **No em-dash (U+2014) or en-dash (U+2013)** anywhere - use a plain ASCII
     hyphen `-`, commas, and periods only.
   - **Each file must stay < 8000 UTF-8 BYTES** - the publisher validates the
     *byte* length and aborts otherwise. Cyrillic/CJK chars cost >= 2 bytes each.

2. Push all 8 localized descriptions (also **re-applies the tags**
   `["Quality of Life","Gameplay","Tactical"]` on the english pass):
   ```powershell
   python workshop/steamugc/publish_ugc.py --localize-descriptions --item <publishedfileid> --changenote "<note>"
   ```
   It prints a per-language `EResult.OK` / `FAILED` table. Confirm all OK.

3. Commit + push.

---

## Task: Change tags

Current tags: **`["Quality of Life", "Gameplay", "Tactical"]`**.
Commonly-accepted Phoenix Point Workshop tags include: **Geoscape, Tactical,
Difficulty, Gameplay, Bionics, Mutations, Quality of Life** (an unknown tag can
fail the submit).

Tags are item-global and are set via `SetItemTags` during the **english pass** of
`--localize-descriptions` (the `WORKSHOP_TAGS` constant in `publish_ugc.py`).

To change them:
1. Edit `WORKSHOP_TAGS = [...]` near the top of `workshop/steamugc/publish_ugc.py`.
2. Re-apply by running the localize-descriptions command (it re-pushes tags):
   ```powershell
   python workshop/steamugc/publish_ugc.py --localize-descriptions --item <publishedfileid> --changenote "update tags"
   ```
   (Alternatively, `--update` also accepts an ad-hoc `--tags "a,b,c"`
   comma-separated list, but it re-uploads content; prefer the localize path for a
   tags-only change.)
3. Commit + push.

---

## Task: Add / replace gallery images

The SteamworksPy build in this repo **does NOT expose `AddItemPreviewFile`**, so
gallery (screenshot) images **cannot be added headlessly** - `publish_ugc.py`
will just print them as a manual web step. Add/replace them via the **Steam web
UI on the logged-in session, using Playwright**:

1. Navigate to the manage-previews page (logged-in Steam session):
   `https://steamcommunity.com/sharedfiles/managepreviews/?id=<publishedfileid>`
2. Click **"Choose File"** and upload the image path(s) from `image/`.
3. Click **Upload** for each image.
4. Click **Save** to commit the changes.

> The **main square preview** (`image/steam_preview.jpg`) IS set headlessly by
> `publish_ugc.py` via `SetItemPreview` on every `--create` / `--update` - no web
> step needed for the square preview, only for gallery screenshots.

After changing source images that live in the repo, commit + push.

---

## Task: Read & reply to Workshop comments

Trigger phrases: "read/reply to comments".

### Read (no login)
```powershell
python workshop/comments/read_comments.py --owner 76561197996210591 --item <publishedfileid> --count 50
```
Prints author + text + timestamp per comment. Uses an undocumented Steam render
endpoint (read-only; may break without notice).

### Draft
Follow the tone in `workshop/comments/draft_replies.md`: **helpful, concise,
thank reporters, stay positive.** Produce a short **EN** and short **RU** reply.
For **bug reports**, steer the reporter to:
- open a **GitHub Issue** at the repo for detailed reports, and
- attach **`Player.log`**, a **save** from just before the issue, the **mod load
  order**, and exact repro steps (expected vs. observed).

### Post (no official write API)
There is **no official write API** for Workshop comments. Post replies via
**Playwright on the logged-in Steam session**:
1. Navigate to
   `https://steamcommunity.com/sharedfiles/filedetails/comments/<publishedfileid>`.
2. Find the comment text box, type the reply, submit it.

> Posting comments this way is **unofficial, fragile, and ToS-risky**. The
> scripted alternative is the experimental `workshop/comments/post_comment.py`,
> which reads cookies from env vars (`STEAM_SESSIONID`, `STEAM_LOGIN_SECURE`) and
> refuses to run without `--i-understand-the-risk`. Prefer manual/Playwright
> posting; treat the script as last resort.

---

## Gotchas

- **`steam_api64.dll` version**: must export `SteamInternal_SteamAPI_Init`
  (SDK >= ~1.57). PP's bundled DLL is too old -> `WinError 127`. Use the
  Slay-the-Spire-2 DLL (see Prerequisites).
- **No long dashes** in store descriptions - em-dash (U+2014) and en-dash
  (U+2013) make text look machine-generated; use ASCII `-` only.
- **8000-byte UTF-8 limit** per description file - count BYTES, not chars.
- **Gallery needs Playwright** - the SteamworksPy binding lacks
  `AddItemPreviewFile`. (Square preview is headless.)
- **Comment posting is web/Playwright only** - no official write API; ToS-risky.
- **Steam client must be running + logged in** as the owner for any publish.
- **Native deps are git-ignored** - re-provision `steamworks/`, `SteamworksPy64.dll`,
  `steam_api64.dll`, `steam_appid.txt` on a fresh clone.
- **Block on `EResult.OK`** - never report a publish as done until the script
  prints it.

---

## Related docs

- `workshop/WORKSHOP.md` - broader publishing playbook.
- `workshop/steamugc/README.md` - native-dep setup detail.
- `workshop/comments/draft_replies.md` - reply tone + reusable EN/RU snippets.

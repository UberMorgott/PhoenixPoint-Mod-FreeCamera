# Free Camera — project notes

Baldur's-Gate-style free-orbit tactical camera mod for Phoenix Point (Approach A:
thin MonoBehaviour driving `Base.Cameras.PlanarScrollCamera` + one Harmony prefix).
See `docs/design.md` for the verified engine facts and component map.

For all routine Steam Workshop operations on this mod — updating/publishing a build,
editing/localizing the store description, changing tags, gallery images, reading/
replying to comments — **read and follow [`docs/OPERATIONS.md`](docs/OPERATIONS.md)**
and execute its steps directly.

- AssemblyName / mod ID: `FreeCamera` / `Morgott.FreeCamera`
- Steam appid (Phoenix Point): **839770**
- Workshop item id (`publishedfileid`): **TBD** (not yet published)
- Remote: **TBD**

## Build / test

```powershell
dotnet build -c Release                       # mod assembly (net472, real game DLLs)
dotnet test tests/FreeCamera.Tests.csproj     # pure OrbitInputMath core (net8 + xUnit)
pwsh -File deploy.ps1                          # build + copy to the live game Mods\FreeCamera\
```

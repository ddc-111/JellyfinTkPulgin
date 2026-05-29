# AGENTS.md — Jellyfin Clips Plugin

## Build & Test

```bash
dotnet restore
dotnet build --configuration Release    # TreatWarningsAsErrors=true, CS1591 suppressed via <NoWarn>
dotnet test --configuration Release --no-build
```

No local .NET SDK on this machine — CI runs on GitHub Actions (ubuntu-latest, .NET 8.0.x). Push to `main` triggers build; push tag `v*` triggers build + release.

## Release

```bash
git tag v1.0.0
git push origin v1.0.0
```

CI packages `Jellyfin.Clips.dll` + `Jellyfin.Clips.deps.json` into a zip, creates a GitHub Release, then auto-commits an updated `manifest.json` with the download URL and SHA256.

## Jellyfin Namespace Traps

These are the #1 source of build failures. Jellyfin types are split across three NuGet packages:

| Type | Package | Namespace |
|------|---------|-----------|
| `InternalItemsQuery` | Jellyfin.Controller | `MediaBrowser.Controller.Entities` |
| `BaseItemKind` | **Jellyfin.Data** | `Jellyfin.Data.Enums` |
| `ItemSortBy` | **Jellyfin.Data** | `Jellyfin.Data.Enums` |
| `SortOrder` | **Jellyfin.Data** | `Jellyfin.Data.Enums` |
| `ILibraryManager` | Jellyfin.Controller | `MediaBrowser.Controller.Library` |
| `ISessionManager` | Jellyfin.Controller | `MediaBrowser.Controller.Session` |
| `IScheduledTask` | Jellyfin.Model | `MediaBrowser.Model.Tasks` |
| `BasePlugin<T>` | Jellyfin.Model | `MediaBrowser.Common.Plugins` |
| `IPluginServiceRegistrator` | Jellyfin.Controller | `MediaBrowser.Controller.Plugins` |

All three Jellyfin packages must have `<ExcludeAssets>runtime</ExcludeAssets>` — the server provides these at runtime.

## Plugin Architecture

- **Entry point**: `Plugin.cs` — singleton, implements `BasePlugin<PluginConfiguration>` + `IHasWebPages`
- **DI registration**: `PluginServiceRegistrator.cs` — implements `IPluginServiceRegistrator`, auto-discovered by Jellyfin
- **API controllers**: `Api/` — standard ASP.NET Core `ControllerBase`, auto-discovered. Routes are under `/Plugins/Clips/`
- **Config page**: `Configuration/configPage.html` — embedded resource, uses Jellyfin's `emby-*` web components + `ApiClient.getPluginConfiguration()`
- **Frontend**: `wwwroot/` — embedded resources, vanilla JS + CSS, TikTok-style vertical scroll feed
- **Database**: SQLite via EF Core, stored at `{AppData}/jellyfin/plugins/clips/clips.db`
- **Background service**: `IdleClipGenerator` — registered as `IHostedService`, monitors `ISessionManager` for idle state
- **Scheduled task**: `ClipGenerationTask` — implements `IScheduledTask`, visible in Jellyfin dashboard

## Adding a New API Controller

1. Create file in `Api/`
2. Inherit from `ControllerBase`, add `[ApiController]`, `[Authorize]`, `[Route("Plugins/Clips/...")]`
3. Inject services via constructor — they're registered in `PluginServiceRegistrator`
4. No manual registration needed — Jellyfin scans plugin assemblies

## Adding a New Service

1. Create interface + implementation in `Services/`
2. Register as `AddSingleton<IFoo, Foo>()` in `PluginServiceRegistrator.cs`
3. For hosted background services use `AddHostedService<T>()`

## Frontend Files

All files under `wwwroot/` must also be listed as `<EmbeddedResource>` in the `.csproj` and registered in `Plugin.GetPages()` for Jellyfin to serve them.

## Plugin GUID

`a1b2c3d4-e5f6-7890-abcd-ef1234567890` — used in `Plugin.cs`, `build.yaml`, `manifest.json`, and `configPage.html`'s `pluginUniqueId`. Must stay consistent.

## Key Dependencies

- `Microsoft.EntityFrameworkCore.Sqlite` 8.0.0 — for data layer
- `Microsoft.AspNetCore.App` framework reference — for API controllers
- FFmpeg must be available on the Jellyfin server at runtime (not a NuGet dep)

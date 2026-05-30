<p align="center">
  <h1 align="center">🎬 Jellyfin Clips</h1>
  <p align="center">
    <b>English | <a href="README_CN.md">中文</a></b>
  </p>
  <p align="center">
    A TikTok-style short video recommendation plugin for Jellyfin
  </p>
  <p align="center">
    <a href="https://github.com/ddc-111/JellyfinTkPulgin/actions">
      <img src="https://github.com/ddc-111/JellyfinTkPulgin/actions/workflows/build-and-release.yml/badge.svg" alt="Build Status">
    </a>
    <img src="https://img.shields.io/badge/Jellyfin-10.11.x-blue" alt="Jellyfin Version">
    <img src="https://img.shields.io/badge/.NET-9.0-purple" alt=".NET Version">
    <img src="https://img.shields.io/github/license/ddc-111/JellyfinTkPulgin" alt="License">
  </p>
</p>

---

## Features

- **Smart Highlight Extraction** — Automatically extracts highlights from your video library using FFmpeg scene detection during server idle time
- **TikTok-style Feed** — Full-screen vertical scrolling, double-tap to like, immersive experience
- **Recommendation Engine** — Personalized recommendations based on user behavior (watch time, completion rate, likes, skips)
- **Clip to Original** — Jump from any highlight clip directly to the original movie or episode
- **Vertical Crop Modes** — Center / Top / Bottom / Smart crop for converting horizontal video to vertical
- **Background Processing** — Detects server idle state and extracts clips automatically without affecting playback
- **Scheduled Tasks** — Daily scheduled scanning and clip generation
- **Full Configuration UI** — Visual configuration panel in Jellyfin Dashboard
- **Multimodal AI Analysis** — Integrates Xiaomi's multimodal LLM to analyze video frames, auto-generate titles, semantic tags, and mood labels for clips

## Requirements

| Dependency | Version |
|-----------|---------|
| Jellyfin Server | 10.11.x |
| .NET Runtime | 9.0 |
| FFmpeg | Any version (must be in system PATH or configured manually) |

## Installation

### Option 1: Plugin Repository (Recommended)

1. Open Jellyfin Dashboard → **Plugins** → **Repositories**
2. Click **+** to add a new repository:
   - **Repository Name**: `Clips`
   - **Repository URL**:
     ```
     https://raw.githubusercontent.com/ddc-111/JellyfinTkPulgin/main/manifest.json
     ```
3. Go to **Plugins** → **Catalog**, find **Clips** and install
4. **Restart Jellyfin server**
5. Configure at **Dashboard** → **Plugins** → **Clips**

### Plugin Pages

| Page | URL |
|------|-----|
| Configuration | `http://{your-jellyfin-server}/web/index.html?#/configurationpage?name=Clips` |
| Feed (TikTok-style) | `http://{your-jellyfin-server}/web/index.html?#/configurationpage?name=feed` |

### Option 2: Manual Installation

1. Download the latest `Jellyfin.Clips_x.x.x.zip` from [Releases](https://github.com/ddc-111/JellyfinTkPulgin/releases)
2. Extract to Jellyfin plugins directory:
   - **Windows**: `%ProgramData%\Jellyfin\Server\plugins\Clips\`
   - **Linux**: `/var/lib/jellyfin/plugins/Clips/`
   - **Docker**: `/config/plugins/Clips/`
3. Restart Jellyfin server

### Option 3: Build from Source

```bash
git clone https://github.com/ddc-111/JellyfinTkPulgin.git
cd JellyfinTkPulgin
dotnet restore
dotnet build --configuration Release
```

Copy `Jellyfin.Clips/bin/Release/net8.0/Jellyfin.Clips.dll` to your Jellyfin plugins directory.

## Configuration

Configure at **Dashboard** → **Plugins** → **Clips**:

### General Settings

| Setting | Default | Description |
|---------|---------|-------------|
| FFmpeg Path | `ffmpeg` | Path to FFmpeg binary |
| Max Clip Duration | 60s | Maximum length of extracted clips |
| Min Clip Duration | 10s | Minimum length of extracted clips |
| Scene Detection Threshold | 0.3 | FFmpeg scene change sensitivity (0.1-0.9, lower = more sensitive) |
| Max Clips Per Video | 5 | Maximum clips to extract from each video |
| Max Total Clips | 500 | Maximum clips stored in database |
| Max Storage | 5120 MB | Maximum disk space for clip files |

### Vertical Crop Modes

| Mode | Description |
|------|-------------|
| Center | Crop from the center of the frame |
| Top | Keep the top portion of the frame |
| Bottom | Keep the bottom portion of the frame |
| Smart | Intelligently select crop area based on content |
| Letterbox | No crop, add black bars top and bottom |

### Background Processing

| Setting | Default | Description |
|---------|---------|-------------|
| Idle Detection Minutes | 10 | Minutes of inactivity before auto-extraction starts |
| Enable Auto Extraction | Yes | Whether to extract clips automatically when idle |

### Recommendation Algorithm Weights

| Weight | Default | Description |
|--------|---------|-------------|
| Genre Preference | 0.30 | Weight for user's preferred genres |
| Completion Rate | 0.25 | Weight for clip watch completion rate |
| Recency Bonus | 0.15 | Boost for newly extracted clips |
| Diversity | 0.10 | Random perturbation for recommendation diversity |
| Scene Score | 0.10 | Weight for FFmpeg scene detection score |

### Multimodal AI Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| Enable Multimodal Analysis | Off | Enable AI-powered semantic analysis for clips |
| API Base URL | `https://api.mimodel.com/v1` | Xiaomi multimodal LLM API endpoint |
| API Key | (empty) | Your Xiaomi API key |
| Model Name | `MiMo-VL-7B` | Multimodal model identifier |
| Sample Frame Count | 3 | Number of frames to extract per clip (1-8) |
| Request Timeout | 30s | API request timeout |

**Features:**
- Auto-generates catchy titles for each clip
- Extracts semantic tags (actions, scenes, objects)
- Assigns mood labels (funny, tense, touching, etc.)
- Built-in risk content detection to filter inappropriate results

## API Endpoints

The plugin exposes the following REST API endpoints (authentication required):

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/Plugins/Clips/Feed?count=20&cursor=xxx&genre=xxx` | Get recommended feed |
| `GET` | `/Plugins/Clips/Clip/{id}` | Get clip details |
| `GET` | `/Plugins/Clips/Clip/{id}/stream` | Stream clip video |
| `GET` | `/Plugins/Clips/Clip/{id}/thumbnail` | Get clip thumbnail |
| `POST` | `/Plugins/Clips/Like/{id}` | Toggle like status |
| `POST` | `/Plugins/Clips/Interaction` | Record user interaction (view/like/skip/etc.) |
| `POST` | `/Plugins/Clips/Admin/Generate` | Manually trigger clip generation (admin) |
| `GET` | `/Plugins/Clips/Admin/Status` | Check generation task status (admin) |

## Frontend

TikTok-style vertical feed interface:

```
http://{your-jellyfin-server}/web/#/plugin-pages/ClipsFeed
```

Or access via Jellyfin plugin entry point after installation.

## Architecture

```
Jellyfin.Clips/
├── Plugin.cs                        # Plugin entry point
├── PluginServiceRegistrator.cs      # DI registration
├── Configuration/                   # Config model & dashboard page
├── Api/                             # REST API controllers
│   ├── FeedController.cs            #   Feed endpoint
│   ├── ClipController.cs            #   Clip playback/detail endpoint
│   └── InteractionController.cs     #   User interaction endpoint
├── Services/                        # Core business logic
│   ├── FfmpegWrapper.cs             #   FFmpeg command wrapper
│   ├── HighlightDetectionService.cs #   Highlight detection algorithm
│   ├── ClipExtractionService.cs     #   Clip extraction orchestration
│   ├── RecommendationEngine.cs      #   Recommendation algorithm
│   └── FeedService.cs               #   Feed assembly
├── Data/                            # Data layer
│   ├── ClipsDbContext.cs            #   EF Core SQLite context
│   ├── Entities/                    #   Data entities
│   └── Repositories/                #   Data repositories
├── BackgroundServices/              # Background services
│   └── IdleClipGenerator.cs         #   Idle detection & auto extraction
├── Tasks/                           # Scheduled tasks
│   └── ClipGenerationTask.cs        #   Daily generation task
└── wwwroot/                         # Frontend assets
    ├── feed.html                    #   TikTok-style feed page
    ├── feed.js                      #   Feed interaction logic
    ├── feed.css                     #   Dark theme styles
    └── components/                  #   Web Components
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | C# / .NET 8.0, ASP.NET Core WebAPI |
| Database | SQLite (EF Core) |
| Video Processing | FFmpeg |
| Frontend | Vanilla JavaScript + CSS, Web Components |
| CI/CD | GitHub Actions |

## Development

```bash
# Clone repository
git clone https://github.com/ddc-111/JellyfinTkPulgin.git
cd JellyfinTkPulgin

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test

# Release a new version
git tag v1.0.0
git push origin v1.0.0
# GitHub Actions will automatically build and create a Release
```

## License

[MIT](LICENSE)

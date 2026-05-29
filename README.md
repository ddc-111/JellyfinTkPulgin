# Jellyfin Clips Plugin

TikTok-style short video feed for Jellyfin.

## Features

- Automatic highlight extraction from your video library using FFmpeg scene detection
- Vertical scrollable feed with TikTok-like UX
- Recommendation engine based on user behavior (watch time, likes, skips)
- Jump from any clip directly to the original content
- Idle-time background processing
- Configurable crop modes for vertical video

## Install via Jellyfin Plugin Repository

1. Open Jellyfin Dashboard → Plugins → Repositories
2. Add a new repository with URL:
   ```
   https://raw.githubusercontent.com/ddc-111/JellyfinTkPulgin/main/manifest.json
   ```
3. Go to Plugins → Catalog, find **Clips** and install
4. Restart Jellyfin server
5. Configure the plugin at Dashboard → Plugins → Clips

## Prerequisites

- Jellyfin 10.9.x
- FFmpeg installed on the server

## Build from Source

```bash
dotnet restore
dotnet build --configuration Release
```

Copy `Jellyfin.Clips/bin/Release/net8.0/Jellyfin.Clips.dll` to your Jellyfin plugins directory.

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| FFmpeg Path | `ffmpeg` | Path to FFmpeg binary |
| Max Clip Duration | 60s | Maximum length of extracted clips |
| Min Clip Duration | 10s | Minimum length of extracted clips |
| Scene Detection Threshold | 0.3 | FFmpeg scene change sensitivity |
| Max Clips Per Video | 5 | Max clips to extract from each video |
| Vertical Crop Mode | center | How to crop horizontal video to vertical |
| Idle Detection Minutes | 10 | Minutes of inactivity before auto-extraction |
| Enable Auto Extraction | true | Automatically extract clips when idle |

## License

MIT

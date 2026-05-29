<p align="center">
  <h1 align="center">🎬 Jellyfin Clips</h1>
  <p align="center">
    <b>中文 | <a href="#english">English</a></b>
  </p>
  <p align="center">
    为 Jellyfin 打造的抖音风格短视频推荐插件
  </p>
  <p align="center">
    <a href="https://github.com/ddc-111/JellyfinTkPulgin/actions">
      <img src="https://github.com/ddc-111/JellyfinTkPulgin/actions/workflows/build-and-release.yml/badge.svg" alt="Build Status">
    </a>
    <img src="https://img.shields.io/badge/Jellyfin-10.9.x-blue" alt="Jellyfin Version">
    <img src="https://img.shields.io/badge/.NET-8.0-purple" alt=".NET Version">
    <img src="https://img.shields.io/github/license/ddc-111/JellyfinTkPulgin" alt="License">
  </p>
</p>

---

## 功能特性

- **智能精彩片段提取** — 利用 FFmpeg 场景检测，在服务器空闲时自动从视频库中提取精彩片段
- **抖音风格信息流** — 全屏竖屏滚动浏览，双击点赞，沉浸式体验
- **推荐算法** — 基于用户行为（观看时长、完成率、点赞、跳过）的个性化推荐
- **片段直达原片** — 从任何精彩片段一键跳转到原片播放
- **竖屏智能裁剪** — 支持居中/顶部/底部/智能裁剪模式，横屏视频自动转竖屏
- **后台自动处理** — 检测服务器空闲状态，自动提取片段，不影响正常播放
- **定时任务** — 支持每日定时扫描视频库并生成片段
- **完整配置面板** — 在 Jellyfin 后台可视化配置所有参数

## 系统要求

| 依赖 | 版本 |
|------|------|
| Jellyfin Server | 10.9.x |
| .NET Runtime | 8.0 |
| FFmpeg | 任意版本（需在系统 PATH 中或手动指定路径） |

## 安装方式

### 方式一：通过插件仓库安装（推荐）

1. 打开 Jellyfin 后台 → **仪表盘** → **插件** → **存储库**
2. 点击 **+** 添加新存储库，填入以下信息：
   - **存储库名称**：`Clips`
   - **存储库 URL**：
     ```
     https://raw.githubusercontent.com/ddc-111/JellyfinTkPulgin/main/manifest.json
     ```
3. 保存后进入 **插件** → **目录**，找到 **Clips** 并点击安装
4. **重启 Jellyfin 服务器**
5. 进入 **仪表盘** → **插件** → **Clips** 进行配置

### 方式二：手动安装

1. 从 [Releases](https://github.com/ddc-111/JellyfinTkPulgin/releases) 页面下载最新版本的 `Jellyfin.Clips_x.x.x.zip`
2. 解压到 Jellyfin 插件目录：
   - **Windows**：`%ProgramData%\Jellyfin\Server\plugins\Clips\`
   - **Linux**：`/var/lib/jellyfin/plugins/Clips/`
   - **Docker**：`/config/plugins/Clips/`
3. 重启 Jellyfin 服务器

### 方式三：从源码构建

```bash
git clone https://github.com/ddc-111/JellyfinTkPulgin.git
cd JellyfinTkPulgin
dotnet restore
dotnet build --configuration Release
```

将 `Jellyfin.Clips/bin/Release/net8.0/Jellyfin.Clips.dll` 复制到 Jellyfin 插件目录。

## 配置说明

安装后在 **仪表盘** → **插件** → **Clips** 中配置：

### 基础设置

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| FFmpeg 路径 | `ffmpeg` | FFmpeg 可执行文件路径 |
| 最大片段时长 | 60 秒 | 提取片段的最大长度 |
| 最小片段时长 | 10 秒 | 提取片段的最小长度 |
| 场景检测阈值 | 0.3 | FFmpeg 场景变化灵敏度（0.1-0.9，越低越灵敏） |
| 每部影片最大片段数 | 5 | 每部影片最多提取的片段数量 |
| 总片段数上限 | 500 | 数据库中最多保存的片段数 |
| 最大存储空间 | 5120 MB | 片段文件占用的最大磁盘空间 |

### 竖屏裁剪模式

| 模式 | 说明 |
|------|------|
| 居中裁剪 | 从画面中心裁剪出竖屏区域 |
| 顶部裁剪 | 保留画面顶部区域 |
| 底部裁剪 | 保留画面底部区域 |
| 智能裁剪 | 基于画面内容智能选择裁剪区域 |
| 信箱模式 | 不裁剪，上下添加黑边 |

### 后台处理

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| 空闲检测时间 | 10 分钟 | 服务器无播放活动多久后开始自动生成 |
| 启用自动提取 | 是 | 是否在空闲时自动提取片段 |

### 推荐算法权重

| 权重 | 默认值 | 说明 |
|------|--------|------|
| 类型偏好 | 0.30 | 用户偏好的影片类型权重 |
| 完成率 | 0.25 | 片段观看完成率的权重 |
| 新近度 | 0.15 | 新提取片段的加权 |
| 多样性 | 0.10 | 推荐多样性的随机扰动 |
| 场景评分 | 0.10 | FFmpeg 场景检测评分的权重 |

## API 接口

插件暴露以下 REST API 端点（需要认证）：

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/Plugins/Clips/Feed?count=20&cursor=xxx&genre=xxx` | 获取推荐信息流 |
| `GET` | `/Plugins/Clips/Clip/{id}` | 获取片段详情 |
| `GET` | `/Plugins/Clips/Clip/{id}/stream` | 流式播放片段视频 |
| `GET` | `/Plugins/Clips/Clip/{id}/thumbnail` | 获取片段缩略图 |
| `POST` | `/Plugins/Clips/Like/{id}` | 切换点赞状态 |
| `POST` | `/Plugins/Clips/Interaction` | 上报用户交互（观看/点赞/跳过等） |
| `POST` | `/Plugins/Clips/Admin/Generate` | 手动触发片段生成（管理员） |
| `GET` | `/Plugins/Clips/Admin/Status` | 查看生成任务状态（管理员） |

## 项目架构

```
Jellyfin.Clips/
├── Plugin.cs                        # 插件入口点
├── PluginServiceRegistrator.cs      # 依赖注入注册
├── Configuration/                   # 配置模型与后台页面
├── Api/                             # REST API 控制器
│   ├── FeedController.cs            #   信息流接口
│   ├── ClipController.cs            #   片段播放/详情接口
│   └── InteractionController.cs     #   用户交互接口
├── Services/                        # 核心业务逻辑
│   ├── FfmpegWrapper.cs             #   FFmpeg 命令封装
│   ├── HighlightDetectionService.cs #   精彩片段检测算法
│   ├── ClipExtractionService.cs     #   片段提取编排
│   ├── RecommendationEngine.cs      #   推荐算法引擎
│   └── FeedService.cs               #   信息流组装
├── Data/                            # 数据层
│   ├── ClipsDbContext.cs            #   EF Core SQLite 上下文
│   ├── Entities/                    #   数据实体
│   └── Repositories/                #   数据仓库
├── BackgroundServices/              # 后台服务
│   └── IdleClipGenerator.cs         #   空闲检测与自动提取
├── Tasks/                           # 定时任务
│   └── ClipGenerationTask.cs        #   每日定时生成任务
└── wwwroot/                         # 前端资源
    ├── feed.html                    #   抖音风格信息流页面
    ├── feed.js                      #   信息流交互逻辑
    ├── feed.css                     #   暗黑主题样式
    └── components/                  #   Web Components
```

## 技术栈

| 层 | 技术 |
|---|---|
| 后端 | C# / .NET 8.0, ASP.NET Core WebAPI |
| 数据库 | SQLite (EF Core) |
| 视频处理 | FFmpeg |
| 前端 | 原生 JavaScript + CSS, Web Components |
| CI/CD | GitHub Actions |

## 开发贡献

```bash
# 克隆仓库
git clone https://github.com/ddc-111/JellyfinTkPulgin.git
cd JellyfinTkPulgin

# 还原依赖
dotnet restore

# 构建
dotnet build

# 运行测试
dotnet test

# 发布新版本
git tag v1.0.0
git push origin v1.0.0
# GitHub Actions 会自动构建并创建 Release
```

## 许可证

[MIT](LICENSE)

---

<a id="english"></a>

# Jellyfin Clips

A TikTok-style short video recommendation plugin for Jellyfin.

## Features

- **Smart Highlight Extraction** — Automatically extracts精彩片段 from your video library using FFmpeg scene detection during server idle time
- **TikTok-style Feed** — Full-screen vertical scrolling, double-tap to like, immersive experience
- **Recommendation Engine** — Personalized recommendations based on user behavior (watch time, completion rate, likes, skips)
- **Clip to Original** — Jump from any highlight clip directly to the original movie or episode
- **Vertical Crop Modes** — Center / Top / Bottom / Smart crop for converting horizontal video to vertical
- **Background Processing** — Detects server idle state and extracts clips automatically without affecting playback
- **Scheduled Tasks** — Daily scheduled scanning and clip generation
- **Full Configuration UI** — Visual configuration panel in Jellyfin Dashboard

## Requirements

| Dependency | Version |
|-----------|---------|
| Jellyfin Server | 10.9.x |
| .NET Runtime | 8.0 |
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

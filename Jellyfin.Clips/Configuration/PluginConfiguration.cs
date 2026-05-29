using MediaBrowser.Model.Plugins;

namespace Jellyfin.Clips.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        FfmpegPath = "ffmpeg";
        MaxClipDurationSeconds = 60;
        MinClipDurationSeconds = 10;
        SceneDetectionThreshold = 0.3;
        MaxClipsPerVideo = 5;
        MaxTotalClips = 500;
        MaxStorageMb = 5120;
        IdleDetectionMinutes = 10;
        EnableAutoExtraction = true;
        VerticalCropMode = "center";
        TargetResolution = "1080x1920";
        FeedPageSize = 20;
        RecommendationWeights = new RecommendationWeights();
    }

    public string FfmpegPath { get; set; }
    public int MaxClipDurationSeconds { get; set; }
    public int MinClipDurationSeconds { get; set; }
    public double SceneDetectionThreshold { get; set; }
    public int MaxClipsPerVideo { get; set; }
    public int MaxTotalClips { get; set; }
    public int MaxStorageMb { get; set; }
    public int IdleDetectionMinutes { get; set; }
    public bool EnableAutoExtraction { get; set; }
    public string VerticalCropMode { get; set; }
    public string TargetResolution { get; set; }
    public int FeedPageSize { get; set; }
    public RecommendationWeights RecommendationWeights { get; set; }
}

public class RecommendationWeights
{
    public double GenrePreference { get; set; } = 0.3;
    public double CompletionRate { get; set; } = 0.25;
    public double RecencyBonus { get; set; } = 0.15;
    public double DiversityBonus { get; set; } = 0.1;
    public double SimilarUserPreference { get; set; } = 0.1;
    public double SceneScore { get; set; } = 0.1;
}

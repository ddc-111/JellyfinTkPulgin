using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Clips.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Clips.Services;

public interface IMultimodalAnalysisService
{
    Task<MultimodalAnalysisResult?> AnalyzeClipAsync(string videoPath, long startTicks, long endTicks, CancellationToken ct = default);
}

public class MultimodalAnalysisResult
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string> SemanticTags { get; set; } = new();
    public string? MoodTag { get; set; }
    public bool IsSuccess { get; set; }
    public string? FailureReason { get; set; }
}

public class MultimodalAnalysisService : IMultimodalAnalysisService
{
    private readonly IFfmpegWrapper _ffmpeg;
    private readonly ILogger<MultimodalAnalysisService> _logger;
    private readonly HttpClient _httpClient;

    private static readonly HashSet<string> RiskKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "暴力", "血腥", "色情", "裸露", "恐怖", "惊悚", "死亡", "自杀",
        "毒品", "赌博", "政治敏感", "违法", "犯罪", "虐待", "歧视",
        "暴力", "血腥", "色情", "裸露", "恐怖", "惊悚", "死亡", "自杀",
        "暴力", "血腥", "色情", "裸露", "恐怖", "惊悚", "死亡", "自杀",
        "violence", "blood", "nude", "naked", "gore", "恐怖", "horror",
        "explicit", "sexual", "drug", "gambling", "abuse", "self-harm"
    };

    private static readonly HashSet<string> RiskPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "无法生成", "无法分析", "内容违规", "触发审核", "敏感内容",
        "不适合", "违反规定", "违规内容", "无法完成", "拒绝回答",
        "sorry", "cannot", "inappropriate", "violation", "policy"
    };

    public MultimodalAnalysisService(
        IFfmpegWrapper ffmpeg,
        ILogger<MultimodalAnalysisService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _ffmpeg = ffmpeg;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<MultimodalAnalysisResult?> AnalyzeClipAsync(
        string videoPath, long startTicks, long endTicks, CancellationToken ct = default)
    {
        var config = Plugin.Instance?.Configuration?.MultimodalConfig;
        if (config == null || !config.EnableMultimodalAnalysis)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            _logger.LogWarning("Multimodal API key is not configured");
            return null;
        }

        List<string>? frames = null;
        var maxRetries = config.MaxRetryCount;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Multimodal analysis cancelled for {Path}", videoPath);
                    return new MultimodalAnalysisResult { IsSuccess = false, FailureReason = "分析被取消" };
                }

                frames = await ExtractSampleFramesAsync(videoPath, startTicks, endTicks, config.SampleFrameCount, ct);
                if (frames.Count == 0)
                {
                    _logger.LogWarning("No frames extracted for analysis");
                    return new MultimodalAnalysisResult { IsSuccess = false, FailureReason = "无法提取视频帧" };
                }

                var result = await CallMultimodalApiAsync(frames, config, ct);
                CleanupTempFiles(frames);
                frames = null;

                if (result.IsSuccess || result.FailureReason == "内容触发风控")
                {
                    return result;
                }

                if (attempt < maxRetries)
                {
                    _logger.LogWarning("Multimodal analysis attempt {Attempt} failed: {Reason}, retrying...",
                        attempt + 1, result.FailureReason);
                    await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), ct).ConfigureAwait(false);
                }
                else
                {
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                CleanupTempFiles(frames ?? new List<string>());
                throw;
            }
            catch (Exception ex)
            {
                CleanupTempFiles(frames ?? new List<string>());
                frames = null;

                if (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Multimodal analysis attempt {Attempt} error, retrying...", attempt + 1);
                    await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), ct).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogError(ex, "Multimodal analysis failed for {Path} after {MaxRetries} retries", videoPath, maxRetries);
                    return new MultimodalAnalysisResult { IsSuccess = false, FailureReason = $"分析异常: {ex.Message}" };
                }
            }
        }

        return new MultimodalAnalysisResult { IsSuccess = false, FailureReason = "分析失败" };
    }

    private async Task<List<string>> ExtractSampleFramesAsync(
        string videoPath, long startTicks, long endTicks, int frameCount, CancellationToken ct)
    {
        var frames = new List<string>();
        var tempDir = Path.Combine(Path.GetTempPath(), "clips_multimodal", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var durationTicks = endTicks - startTicks;
            var intervalTicks = durationTicks / (frameCount + 1);

            for (var i = 1; i <= frameCount; i++)
            {
                var frameTicks = startTicks + intervalTicks * i;
                var framePath = Path.Combine(tempDir, $"frame_{i}.jpg");

                var success = await _ffmpeg.ExtractFrameAsync(videoPath, framePath, frameTicks, ct);
                if (success && File.Exists(framePath))
                {
                    frames.Add(framePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract sample frames");
            CleanupTempFiles(frames);
        }

        return frames;
    }

    private async Task<MultimodalAnalysisResult> CallMultimodalApiAsync(
        List<string> framePaths, MultimodalConfiguration config, CancellationToken ct)
    {
        var base64Frames = new List<string>();
        foreach (var framePath in framePaths)
        {
            var bytes = await File.ReadAllBytesAsync(framePath, ct);
            base64Frames.Add(Convert.ToBase64String(bytes));
        }

        var messages = BuildMessages(base64Frames);

        var requestBody = new
        {
            model = config.ModelName,
            messages,
            max_tokens = config.MaxTokens,
            temperature = config.Temperature
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds);

        var response = await _httpClient.PostAsync($"{config.BaseUrl}/chat/completions", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Multimodal API returned {StatusCode}: {Body}", response.StatusCode, responseBody);
            return new MultimodalAnalysisResult { IsSuccess = false, FailureReason = $"API错误: {response.StatusCode}" };
        }

        var apiResponse = JsonSerializer.Deserialize<MultimodalApiResponse>(responseBody);
        var assistantMessage = apiResponse?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(assistantMessage))
        {
            return new MultimodalAnalysisResult { IsSuccess = false, FailureReason = "API返回空内容" };
        }

        var result = ParseAnalysisResult(assistantMessage);
        if (result.IsSuccess && ContainsRiskContent(result))
        {
            _logger.LogWarning("Content risk detected in AI analysis result: {Title}", result.Title);
            return new MultimodalAnalysisResult
            {
                IsSuccess = false,
                FailureReason = "内容触发风控",
                Title = result.Title,
                Description = result.Description,
                SemanticTags = result.SemanticTags,
                MoodTag = result.MoodTag
            };
        }

        return result;
    }

    private List<object> BuildMessages(List<string> base64Frames)
    {
        var contentItems = new List<object>
        {
            new
            {
                type = "text",
                text = @"请分析这个视频片段的内容，返回以下JSON格式（不要返回其他内容）：
{
  ""title"": ""10字以内的吸引人标题"",
  ""description"": ""30字以内的内容描述"",
  ""tags"": [""标签1"", ""标签2"", ""标签3""],
  ""mood"": ""情感标签(如: 搞笑/紧张/感人/震撼/治愈/悬疑/热血/日常)""
}

注意：
1. 标题要简洁有吸引力
2. 标签要准确描述画面内容和场景
3. 情绪标签从给定选项中选择
4. 确保内容适合公开展示"
            }
        };

        foreach (var frame in base64Frames)
        {
            contentItems.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:image/jpeg;base64,{frame}" }
            });
        }

        return new List<object>
        {
            new { role = "user", content = contentItems }
        };
    }

    private MultimodalAnalysisResult ParseAnalysisResult(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<ParsedAnalysis>(jsonStr, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed != null)
                {
                    return new MultimodalAnalysisResult
                    {
                        IsSuccess = true,
                        Title = parsed.Title,
                        Description = parsed.Description,
                        SemanticTags = parsed.Tags ?? new List<string>(),
                        MoodTag = parsed.Mood
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse multimodal response: {Response}", response);
        }

        return new MultimodalAnalysisResult { IsSuccess = false, FailureReason = "解析响应失败" };
    }

    private bool ContainsRiskContent(MultimodalAnalysisResult result)
    {
        var textsToCheck = new List<string?> { result.Title, result.Description, result.MoodTag };
        textsToCheck.AddRange(result.SemanticTags);

        foreach (var text in textsToCheck)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            foreach (var keyword in RiskKeywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (var pattern in RiskPatterns)
            {
                if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void CleanupTempFiles(List<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        try
        {
            if (filePaths.Count > 0)
            {
                var dir = Path.GetDirectoryName(filePaths[0]);
                if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
        catch { }
    }

    private class ParsedAnalysis
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
        [JsonPropertyName("mood")]
        public string? Mood { get; set; }
    }

    private class MultimodalApiResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public MessageContent? Message { get; set; }
    }

    private class MessageContent
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}

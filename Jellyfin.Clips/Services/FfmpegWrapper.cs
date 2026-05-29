using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Clips.Services;

public interface IFfmpegWrapper
{
    Task<double[]> DetectSceneChangesAsync(string inputPath, double threshold, CancellationToken ct = default);
    Task<bool> ExtractClipAsync(string inputPath, string outputPath, long startTicks, long endTicks,
        string cropMode, string targetResolution, CancellationToken ct = default);
    Task<bool> GenerateThumbnailAsync(string inputPath, string outputPath, long atTicks, CancellationToken ct = default);
    Task<bool> ExtractFrameAsync(string inputPath, string outputPath, long atTicks, CancellationToken ct = default);
    Task<TimeSpan> GetDurationAsync(string inputPath, CancellationToken ct = default);
}

public class FfmpegWrapper : IFfmpegWrapper
{
    private readonly ILogger<FfmpegWrapper> _logger;
    private readonly Configuration.PluginConfiguration _config;

    public FfmpegWrapper(ILogger<FfmpegWrapper> logger)
    {
        _logger = logger;
        _config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();
    }

    public async Task<double[]> DetectSceneChangesAsync(string inputPath, double threshold, CancellationToken ct = default)
    {
        var args = string.Format(CultureInfo.InvariantCulture,
            "-i \"{0}\" -vf \"select='gt(scene,{1})',showinfo\" -f null - 2>&1",
            inputPath, threshold);

        var output = await RunFfmpegAsync(args, ct).ConfigureAwait(false);
        var timestamps = new List<double>();

        foreach (var line in output.Split('\n'))
        {
            if (line.Contains("pts_time:", StringComparison.Ordinal))
            {
                var idx = line.IndexOf("pts_time:", StringComparison.Ordinal) + 9;
                var end = line.IndexOf(' ', idx);
                if (end < 0) end = line.Length;
                var timeStr = line[idx..end].Trim();
                if (double.TryParse(timeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
                {
                    timestamps.Add(time);
                }
            }
        }

        return timestamps.ToArray();
    }

    public async Task<bool> ExtractClipAsync(
        string inputPath, string outputPath, long startTicks, long endTicks,
        string cropMode, string targetResolution, CancellationToken ct = default)
    {
        var startSec = startTicks / 10_000_000.0;
        var durationSec = (endTicks - startTicks) / 10_000_000.0;
        var cropFilter = GetCropFilter(cropMode, targetResolution);

        var args = string.Format(CultureInfo.InvariantCulture,
            "-ss {0:F3} -i \"{1}\" -t {2:F3} {3} -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 128k -movflags +faststart -y \"{4}\"",
            startSec, inputPath, durationSec, cropFilter, outputPath);

        var output = await RunFfmpegAsync(args, ct).ConfigureAwait(false);
        var success = File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;

        if (!success)
        {
            _logger.LogError("Failed to extract clip: {Output}", output);
        }

        return success;
    }

    public async Task<bool> GenerateThumbnailAsync(string inputPath, string outputPath, long atTicks, CancellationToken ct = default)
    {
        var atSec = atTicks / 10_000_000.0;

        var args = string.Format(CultureInfo.InvariantCulture,
            "-ss {0:F3} -i \"{1}\" -vframes 1 -vf \"scale=480:-1\" -y \"{2}\"",
            atSec, inputPath, outputPath);

        var output = await RunFfmpegAsync(args, ct).ConfigureAwait(false);
        return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
    }

    public async Task<bool> ExtractFrameAsync(string inputPath, string outputPath, long atTicks, CancellationToken ct = default)
    {
        var atSec = atTicks / 10_000_000.0;

        var args = string.Format(CultureInfo.InvariantCulture,
            "-ss {0:F3} -i \"{1}\" -vframes 1 -vf \"scale=768:-1\" -q:v 2 -y \"{2}\"",
            atSec, inputPath, outputPath);

        var output = await RunFfmpegAsync(args, ct).ConfigureAwait(false);
        return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
    }

    public async Task<TimeSpan> GetDurationAsync(string inputPath, CancellationToken ct = default)
    {
        var args = $"-i \"{inputPath}\" -show_entries format=duration -v quiet -of csv=\"p=0\"";
        var output = await RunProcessAsync(_config.FfmpegPath.Replace("ffmpeg", "ffprobe"), args, ct).ConfigureAwait(false);

        if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.Zero;
    }

    private string GetCropFilter(string cropMode, string targetResolution)
    {
        var parts = targetResolution.Split('x');
        if (parts.Length != 2) return string.Empty;

        var targetW = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var targetH = int.Parse(parts[1], CultureInfo.InvariantCulture);

        return cropMode switch
        {
            "center" => $"-vf \"crop=ih*{targetW}/{targetH}:ih,scale={targetW}:{targetH}\"",
            "top" => $"-vf \"crop=ih*{targetW}/{targetH}:ih:0:0,scale={targetW}:{targetH}\"",
            "bottom" => $"-vf \"crop=ih*{targetW}/{targetH}:ih:0:ih-ow*{targetH}/{targetW},scale={targetW}:{targetH}\"",
            "smart" => $"-vf \"crop=ih*{targetW}/{targetH}:ih,scale={targetW}:{targetH}\"",
            _ => $"-vf \"scale={targetW}:{targetH}:force_original_aspect_ratio=decrease,pad={targetW}:{targetH}:(ow-iw)/2:(oh-ih)/2\""
        };
    }

    private async Task<string> RunFfmpegAsync(string arguments, CancellationToken ct)
    {
        return await RunProcessAsync(_config.FfmpegPath, arguments, ct).ConfigureAwait(false);
    }

    private async Task<string> RunProcessAsync(string fileName, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        return errorBuilder.Length > 0 ? errorBuilder.ToString() : outputBuilder.ToString();
    }
}

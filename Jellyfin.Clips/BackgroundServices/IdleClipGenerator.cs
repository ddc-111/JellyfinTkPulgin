using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Jellyfin.Clips.Configuration;
using Jellyfin.Clips.Services;

namespace Jellyfin.Clips.BackgroundServices;

public class IdleClipGenerator : BackgroundService
{
    private readonly ISessionManager _sessionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IClipExtractionService _clipExtractionService;
    private readonly ILogger<IdleClipGenerator> _logger;
    private readonly PluginConfiguration _config;

    private DateTime _lastActivityCheck = DateTime.MinValue;
    private bool _isGenerating = false;

    public IdleClipGenerator(
        ISessionManager sessionManager,
        ILibraryManager libraryManager,
        IClipExtractionService clipExtractionService,
        ILogger<IdleClipGenerator> logger)
    {
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;
        _clipExtractionService = clipExtractionService;
        _logger = logger;
        _config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IdleClipGenerator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_config.EnableAutoExtraction && IsServerIdle())
                {
                    await GenerateClipsAsync(stoppingToken).ConfigureAwait(false);
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IdleClipGenerator");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private bool IsServerIdle()
    {
        try
        {
            var activeSessions = _sessionManager.Sessions
                .Where(s => s.NowPlayingItem is not null)
                .ToList();

            if (activeSessions.Count > 0)
            {
                _lastActivityCheck = DateTime.UtcNow;
                return false;
            }

            var idleMinutes = (DateTime.UtcNow - _lastActivityCheck).TotalMinutes;
            return idleMinutes >= _config.IdleDetectionMinutes;
        }
        catch
        {
            return false;
        }
    }

    private async Task GenerateClipsAsync(CancellationToken ct)
    {
        if (_isGenerating) return;

        _isGenerating = true;
        try
        {
            _logger.LogInformation("Starting idle clip generation");

            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
                IsVirtualItem = false,
                Limit = 10,
                OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) }
            };

            var items = _libraryManager.GetItemList(query);
            var extracted = 0;

            foreach (var item in items)
            {
                if (ct.IsCancellationRequested) break;
                if (!IsServerIdle()) break;

                try
                {
                    var count = await _clipExtractionService.ExtractClipsFromItemAsync(
                        item.Id.ToString(), false, ct).ConfigureAwait(false);
                    extracted += count;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract clips from {Name}", item.Name);
                }
            }

            _logger.LogInformation("Idle clip generation completed. Extracted {Count} clips", extracted);
        }
        finally
        {
            _isGenerating = false;
        }
    }
}

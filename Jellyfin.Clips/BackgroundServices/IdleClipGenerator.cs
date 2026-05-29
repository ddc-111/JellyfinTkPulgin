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
    private bool _isShuttingDown = false;

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

        try
        {
            await _clipExtractionService.RecoverInterruptedTasksAsync(stoppingToken).ConfigureAwait(false);
            _logger.LogInformation("Interrupted task recovery completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover interrupted tasks");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_isShuttingDown)
                {
                    _logger.LogInformation("Shutting down, skipping generation");
                    break;
                }

                if (_config.EnableAutoExtraction && IsServerIdle())
                {
                    await GenerateClipsAsync(stoppingToken).ConfigureAwait(false);
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("IdleClipGenerator cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IdleClipGenerator");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("IdleClipGenerator stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IdleClipGenerator stopping gracefully...");
        _isShuttingDown = true;

        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await base.StopAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("IdleClipGenerator stop timed out");
        }

        _logger.LogInformation("IdleClipGenerator stopped");
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
        if (_isGenerating || _isShuttingDown) return;

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
                if (ct.IsCancellationRequested || _isShuttingDown)
                {
                    _logger.LogInformation("Clip generation interrupted by shutdown");
                    break;
                }

                if (!IsServerIdle())
                {
                    _logger.LogInformation("Server no longer idle, stopping generation");
                    break;
                }

                try
                {
                    var count = await _clipExtractionService.ExtractClipsFromItemAsync(
                        item.Id.ToString(), false, ct).ConfigureAwait(false);
                    extracted += count;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Clip extraction cancelled for {Name}", item.Name);
                    throw;
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

using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Clips.Services;
using Jellyfin.Clips.Data.Repositories;
using Jellyfin.Clips.BackgroundServices;
using Jellyfin.Clips.Data;

namespace Jellyfin.Clips;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(
        IServiceCollection serviceCollection,
        IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ClipsDbContext>(sp =>
        {
            var ctx = new ClipsDbContext();
            ctx.Database.EnsureCreated();
            return ctx;
        });

        serviceCollection.AddSingleton<IClipRepository, ClipRepository>();
        serviceCollection.AddSingleton<IInteractionRepository, InteractionRepository>();
        serviceCollection.AddSingleton<IProcessingStateRepository, ProcessingStateRepository>();

        serviceCollection.AddSingleton<IFfmpegWrapper, FfmpegWrapper>();
        serviceCollection.AddSingleton<IHighlightDetectionService, HighlightDetectionService>();
        serviceCollection.AddSingleton<IClipExtractionService, ClipExtractionService>();
        serviceCollection.AddSingleton<IRecommendationEngine, RecommendationEngine>();
        serviceCollection.AddSingleton<IFeedService, FeedService>();
        serviceCollection.AddSingleton<IMultimodalAnalysisService, MultimodalAnalysisService>();

        serviceCollection.AddHttpClient();
        serviceCollection.AddHostedService<IdleClipGenerator>();
    }
}

using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Clips.Configuration;

namespace Jellyfin.Clips;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "Clips";

    public override Guid Id => Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public override string Description => "TikTok-style short video feed. Extracts highlights from your library and presents them in a scrollable feed with smart recommendations.";

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = "feed",
                EmbeddedResourcePath = $"{GetType().Namespace}.wwwroot.feed.html",
                EnableInMainMenu = true
            },
            new PluginPageInfo
            {
                Name = "feed.js",
                EmbeddedResourcePath = $"{GetType().Namespace}.wwwroot.feed.js"
            },
            new PluginPageInfo
            {
                Name = "feed.css",
                EmbeddedResourcePath = $"{GetType().Namespace}.wwwroot.feed.css"
            },
            new PluginPageInfo
            {
                Name = "video-card.js",
                EmbeddedResourcePath = $"{GetType().Namespace}.wwwroot.components.video-card.js"
            },
            new PluginPageInfo
            {
                Name = "interaction-bar.js",
                EmbeddedResourcePath = $"{GetType().Namespace}.wwwroot.components.interaction-bar.js"
            },
            new PluginPageInfo
            {
                Name = "progress-bar.js",
                EmbeddedResourcePath = $"{GetType().Namespace}.wwwroot.components.progress-bar.js"
            }
        ];
    }
}

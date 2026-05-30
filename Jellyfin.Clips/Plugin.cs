using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Clips.Configuration;
using Jellyfin.Clips.Data;

namespace Jellyfin.Clips;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        InitializeDatabase().GetAwaiter().GetResult();
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
                Name = "Clips",
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = "ClipsFeed",
                EmbeddedResourcePath = $"{GetType().Namespace}.wwwroot.feed.html",
                EnableInMainMenu = true
            }
        ];
    }

    private static async Task InitializeDatabase()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "jellyfin", "plugins", "clips", "clips.db");
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var options = new DbContextOptionsBuilder<ClipsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        using var context = new ClipsDbContext(options);
        await context.Database.EnsureCreatedAsync();
    }
}

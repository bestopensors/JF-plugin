using Jellyfin.Plugin.PosterTags.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PosterTags;

/// <summary>
/// Main plugin class for Poster Tags - embeds quality, 4K, flags, IMDB, RT badges on library posters.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly ILogger<Plugin> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer.</param>
    /// <param name="logger">Logger instance.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        _logger = logger;
        Instance = this;
        const string EmbeddedPath = "Jellyfin.Plugin.PosterTags.Configuration.configPage.html";
        var asm = GetType().Assembly;
        var names = asm.GetManifestResourceNames();
        _logger.LogInformation(
            "Poster Tags: GetPages embedded path = {Path}. Assembly manifest resources: [{Resources}]",
            EmbeddedPath,
            names.Length > 0 ? string.Join(", ", names) : "(none)");
    }

    /// <inheritdoc />
    public override string Name => "Poster Tags";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("a7b8c9d0-e1f2-3456-7890-abcdef123456");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        const string EmbeddedPath = "Jellyfin.Plugin.PosterTags.Configuration.configPage.html";
        _logger.LogInformation("Poster Tags: GetPages returning config page with EmbeddedResourcePath = {Path}", EmbeddedPath);
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = EmbeddedPath
            }
        };
    }
}

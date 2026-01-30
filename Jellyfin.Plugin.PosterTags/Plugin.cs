using System.Reflection;
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
        var asm = GetType().Assembly;
        var names = asm.GetManifestResourceNames();
        _logger.LogInformation(
            "Poster Tags: Assembly manifest resource names: [{Resources}]",
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
        var asm = GetType().Assembly;
        var allNames = asm.GetManifestResourceNames();

        // Try exact path first, then any name containing "configPage"
        var pathToUse = "Jellyfin.Plugin.PosterTags.Configuration.configPage.html";
        using (var stream = asm.GetManifestResourceStream(pathToUse))
        {
            if (stream is null)
            {
                var fallback = allNames.FirstOrDefault(n => n.EndsWith("configPage.html", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(fallback))
                {
                    pathToUse = fallback;
                    _logger.LogWarning("Poster Tags: Using fallback resource name: {Name}", pathToUse);
                }
                else
                {
                    _logger.LogError(
                        "Poster Tags: Embedded resource not found. Tried: {Path}. Available: [{Resources}]",
                        pathToUse,
                        allNames.Length > 0 ? string.Join(", ", allNames) : "(none)");
                }
            }
        }

        _logger.LogInformation("Poster Tags: GetPages returning config page with EmbeddedResourcePath = {Path}", pathToUse);
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = pathToUse
            }
        };
    }
}

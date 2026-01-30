using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Jellyfin.Plugin.PosterTags.Configuration;
using Jellyfin.Plugin.PosterTags.Services;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.PosterTags.Api;

/// <summary>
/// API controller for poster tag preview (used in plugin settings).
/// </summary>
[ApiController]
[Route("Plugins/PosterTags")]
public class PosterTagsApiController : ControllerBase
{
    private const string ConfigPageResourceName = "Jellyfin.Plugin.PosterTags.Configuration.config.html";
    private const string ConfigContentResourceName = "Jellyfin.Plugin.PosterTags.Configuration.configContent.html";

    private readonly ILibraryManager _libraryManager;
    private readonly PosterTagService _posterTagService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PosterTagsApiController"/> class.
    /// </summary>
    public PosterTagsApiController(
        ILibraryManager libraryManager,
        PosterTagService posterTagService)
    {
        _libraryManager = libraryManager;
        _posterTagService = posterTagService;
    }

    /// <summary>
    /// GET plugin version (e.g. {"version":"1.0.8"}). Use to confirm updates.
    /// </summary>
    [HttpGet("Version")]
    [Produces("application/json")]
    public IActionResult GetVersion()
    {
        return Ok(new { version = Plugin.AssemblyVersion });
    }

    /// <summary>
    /// GET configuration page HTML (workaround when Dashboard → Plugins → Poster Tags → Settings shows a blank page).
    /// Open this URL in the same browser where you're logged into Jellyfin: {server}/Plugins/PosterTags/ConfigurationPage
    /// </summary>
    [HttpGet("ConfigurationPage")]
    [Produces("text/html")]
    public IActionResult GetConfigurationPage()
    {
        var asm = typeof(Plugin).Assembly;
        // Serve full settings HTML (configContent) so iframe and direct URL both work
        using var stream = asm.GetManifestResourceStream(ConfigContentResourceName)
            ?? asm.GetManifestResourceStream(ConfigPageResourceName);
        if (stream is null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();
        html = html.Replace("{{POSTERTAGS_VERSION}}", Plugin.AssemblyVersion, StringComparison.Ordinal);

        // Wrap in full document: body grows with content and scrolls (entire page scrollable)
        var doc = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>html{height:100%;overflow:auto;-webkit-overflow-scrolling:touch;}body{min-height:100%;margin:0;padding:0;position:relative;}</style></head><body>" + html + "</body></html>";
        return Content(doc, "text/html; charset=utf-8");
    }

    /// <summary>
    /// GET preview image. Uses saved config. If itemId is provided, use that item; otherwise pick a random movie/series from selected libraries.
    /// </summary>
    /// <param name="itemId">Optional item ID. If empty, a random item from selected libraries is used.</param>
    /// <returns>PNG image or 404.</returns>
    [HttpGet("Preview")]
    [Produces("image/png")]
    public async Task<IActionResult> GetPreview([FromQuery] string? itemId, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            return NotFound();
        }

        BaseItem? item;
        if (!string.IsNullOrWhiteSpace(itemId) && Guid.TryParse(itemId, out var id))
        {
            item = _libraryManager.GetItemById(id);
        }
        else
        {
            item = null;
        }

        if (item == null)
        {
            item = GetRandomPreviewItem(config);
        }

        if (item == null)
        {
            return NotFound();
        }

        var bytes = await _posterTagService.GetPreviewImageAsync(item, config, cancellationToken).ConfigureAwait(false);
        if (bytes == null || bytes.Length == 0)
        {
            return NotFound();
        }

        return File(bytes, "image/png");
    }

    /// <summary>
    /// POST preview with optional config override (for live preview without saving). Body: { "ItemId": "guid?", "Config": { ... } }.
    /// If Config is provided it is used for this preview only; otherwise saved config is used.
    /// </summary>
    [HttpPost("Preview")]
    [Produces("image/png")]
    public async Task<IActionResult> PostPreview([FromBody] PreviewRequest? request, CancellationToken cancellationToken)
    {
        PluginConfiguration config;
        if (request?.Config != null && request.Config.Count > 0)
        {
            config = ParseConfigFromDictionary(request.Config);
        }
        else
        {
            config = Plugin.Instance?.Configuration;
        }

        if (config == null)
        {
            return NotFound();
        }

        BaseItem? item = null;
        if (!string.IsNullOrWhiteSpace(request?.ItemId) && Guid.TryParse(request.ItemId, out var id))
        {
            item = _libraryManager.GetItemById(id);
        }

        if (item == null)
        {
            item = GetRandomPreviewItem(config);
        }

        if (item == null)
        {
            return NotFound();
        }

        var bytes = await _posterTagService.GetPreviewImageAsync(item, config, cancellationToken).ConfigureAwait(false);
        if (bytes == null || bytes.Length == 0)
        {
            return NotFound();
        }

        return File(bytes, "image/png");
    }

    /// <summary>
    /// GET list of libraries (views) for the config page. Returns all user-visible libraries so the dropdown populates without relying on client ApiClient.
    /// </summary>
    [HttpGet("Libraries")]
    [Produces("application/json")]
    public IActionResult GetLibraries()
    {
        var root = _libraryManager.RootFolder;
        if (root == null)
        {
            return Ok(new List<object>());
        }

        var children = _libraryManager.GetItemList(new InternalItemsQuery
        {
            ParentId = root.Id,
            IncludeItemTypes = new[] { BaseItemKind.CollectionFolder, BaseItemKind.Folder }
        });

        var list = children
            .Where(c => c != null && !string.IsNullOrEmpty(c.Name))
            .Select(c => new { Id = c!.Id.ToString("N", CultureInfo.InvariantCulture), Name = c.Name })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList<object>();

        return Ok(list);
    }

    /// <summary>
    /// GET a random item from selected libraries that has a poster (for preview dropdown/label).
    /// </summary>
    [HttpGet("PreviewItem")]
    public IActionResult GetPreviewItem()
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            return NotFound();
        }

        var item = GetRandomPreviewItem(config);
        if (item == null)
        {
            return NotFound();
        }

        return Ok(new { Id = item.Id.ToString("N", CultureInfo.InvariantCulture), Name = item.Name });
    }

    private BaseItem? GetRandomPreviewItem(PluginConfiguration config)
    {
        var libraryIds = config.SelectedLibraryIds;
        if (libraryIds == null || libraryIds.Length == 0)
        {
            var root = _libraryManager.RootFolder;
            if (root == null)
            {
                return null;
            }

            var children = _libraryManager.GetItemList(new InternalItemsQuery
            {
                ParentId = root.Id,
                IncludeItemTypes = new[] { BaseItemKind.CollectionFolder, BaseItemKind.Folder }
            });
            libraryIds = children.Select(c => c.Id.ToString("N", CultureInfo.InvariantCulture)).ToArray();
        }

        var guids = libraryIds
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToArray();

        if (guids.Length == 0)
        {
            return null;
        }

        var query = new InternalItemsQuery
        {
            AncestorIds = guids,
            IsVirtualItem = false,
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series }
        };

        var items = _libraryManager.GetItemList(query)
            .Where(i => i != null && i.HasImage(ImageType.Primary, 0))
            .Where(i => _posterTagService.HasUsablePrimaryImage(i!))
            .ToList();

        if (items.Count == 0)
        {
            return null;
        }

        var rnd = new Random();
        return items[rnd.Next(items.Count)];
    }

    private static PluginConfiguration ParseConfigFromDictionary(Dictionary<string, JsonElement> dict)
    {
        var config = new PluginConfiguration();
        if (dict.TryGetValue("SelectedLibraryIds", out var libs) && libs.ValueKind == JsonValueKind.Array)
        {
            config.SelectedLibraryIds = libs.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToArray();
        }

        if (dict.TryGetValue("Show4K", out var v)) config.Show4K = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("ShowHD", out v)) config.ShowHD = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("ShowQuality", out v)) config.ShowQuality = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("ShowAudioLanguageFlags", out v)) config.ShowAudioLanguageFlags = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("ShowImdbRating", out v)) config.ShowImdbRating = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("ShowRottenTomatoes", out v)) config.ShowRottenTomatoes = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("BadgePosition", out v) && Enum.TryParse<BadgePosition>(v.GetString(), out var badgePos))
        {
            config.BadgePosition = badgePos;
        }

        if (dict.TryGetValue("UseLetterResolution", out v)) config.UseLetterResolution = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("SkipItemsWithNoAudioLanguage", out v)) config.SkipItemsWithNoAudioLanguage = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("UseExternalRatings", out v)) config.UseExternalRatings = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("MdbListApiKey", out v)) config.MdbListApiKey = v.GetString() ?? string.Empty;
        if (dict.TryGetValue("TmdbApiKey", out v)) config.TmdbApiKey = v.GetString() ?? string.Empty;
        if (dict.TryGetValue("CustomTagEnabled", out v)) config.CustomTagEnabled = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("CustomTagText", out v)) config.CustomTagText = v.GetString() ?? string.Empty;
        if (dict.TryGetValue("CustomTagPosition", out v) && Enum.TryParse<BadgePosition>(v.GetString(), out var customPos))
        {
            config.CustomTagPosition = customPos;
        }

        if (dict.TryGetValue("TagCurvature", out v) && v.TryGetInt32(out var curv))
        {
            config.TagCurvature = Math.Clamp(curv, 0, 100);
        }
        if (dict.TryGetValue("AutoApplyOnLibraryScan", out v)) config.AutoApplyOnLibraryScan = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("ShowHDR", out v)) config.ShowHDR = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("ShowDolbyAtmos", out v)) config.ShowDolbyAtmos = v.ValueKind == JsonValueKind.True;
        if (dict.TryGetValue("ShowDtsX", out v)) config.ShowDtsX = v.ValueKind == JsonValueKind.True;
        return config;
    }
}

/// <summary>
/// Request body for POST Preview (live preview with config override).
/// </summary>
public class PreviewRequest
{
    /// <summary>Optional item ID. If null, a random item from libraries is used.</summary>
    public string? ItemId { get; set; }

    /// <summary>Optional config override (same shape as plugin config). Used for live preview without saving.</summary>
    public Dictionary<string, JsonElement>? Config { get; set; }
}

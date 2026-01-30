using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.PosterTags.Configuration;
using Jellyfin.Plugin.PosterTags.Services;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Configuration;
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
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="PosterTagsApiController"/> class.
    /// </summary>
    public PosterTagsApiController(
        ILibraryManager libraryManager,
        PosterTagService posterTagService,
        IApplicationPaths applicationPaths)
    {
        _libraryManager = libraryManager;
        _posterTagService = posterTagService;
        _applicationPaths = applicationPaths;
    }

    private string GetPreviewCopyPath(Guid itemId, string extension)
    {
        var dir = Path.Combine(_applicationPaths.DataPath, "postertags", "preview");
        return Path.Combine(dir, "preview_" + itemId.ToString("N", CultureInfo.InvariantCulture) + extension);
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

        // Single scroll root so iframe content scrolls and receives clicks (pointer-events, no overflow on html/body)
        var scrollRoot = "<div id=\"postertags-scroll-root\" style=\"flex:1;min-height:0;overflow-y:auto;overflow-x:hidden;-webkit-overflow-scrolling:touch;position:relative;pointer-events:auto;z-index:1;\">" + html + "</div>";
        var doc = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>html,body{height:100%;margin:0;padding:0;overflow:hidden;pointer-events:auto;}body{display:flex;flex-direction:column;}</style></head><body>" + scrollRoot + "</body></html>";
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

        var copyPath = GetPreviewCopyPath(item.Id, Path.GetExtension(item.GetImagePath(ImageType.Primary, 0) ?? ".jpg"));
        var sourcePath = System.IO.File.Exists(copyPath) ? copyPath : null;
        var bytes = await _posterTagService.GetPreviewImageAsync(item, config, sourcePath, cancellationToken).ConfigureAwait(false);
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
        if (request?.Config != null)
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

        var copyPath = GetPreviewCopyPath(item.Id, Path.GetExtension(item.GetImagePath(ImageType.Primary, 0) ?? ".jpg"));
        var sourcePath = System.IO.File.Exists(copyPath) ? copyPath : null;
        var bytes = await _posterTagService.GetPreviewImageAsync(item, config, sourcePath, cancellationToken).ConfigureAwait(false);
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

        // Prefer user-facing libraries (UserView); if none, show all root children so something is always visible
        var userViews = children
            .Where(c => c is UserView && !string.IsNullOrEmpty(c.Name))
            .Select(c => new { Id = c!.Id.ToString("N", CultureInfo.InvariantCulture), Name = c.Name })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList<object>();

        if (userViews.Count > 0)
        {
            return Ok(userViews);
        }

        var fallback = children
            .Where(c => c != null && !string.IsNullOrEmpty(c.Name))
            .Select(c => new { Id = c!.Id.ToString("N", CultureInfo.InvariantCulture), Name = c.Name })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList<object>();

        return Ok(fallback);
    }

    /// <summary>
    /// GET a random item from selected libraries that has a poster. Copies the artwork to a temp file for preview (original unchanged).
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

        var primaryPath = item.GetImagePath(ImageType.Primary, 0);
        var ext = string.IsNullOrEmpty(primaryPath) ? ".jpg" : Path.GetExtension(primaryPath);
        if (string.IsNullOrEmpty(ext))
        {
            ext = ".jpg";
        }

        var copyPath = GetPreviewCopyPath(item.Id, ext);
        _posterTagService.EnsurePreviewCopy(item, copyPath);

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

    private static bool TryGetConfigValue(Dictionary<string, JsonElement> dict, string key, out JsonElement value)
    {
        if (dict.TryGetValue(key, out value))
        {
            return true;
        }

        var camel = char.ToLowerInvariant(key[0]) + key[1..];
        return dict.TryGetValue(camel, out value);
    }

    private static bool GetConfigBool(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString();
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static PluginConfiguration ParseConfigFromDictionary(Dictionary<string, JsonElement> dict)
    {
        var config = new PluginConfiguration();
        if (TryGetConfigValue(dict, "SelectedLibraryIds", out var libs) && libs.ValueKind == JsonValueKind.Array)
        {
            config.SelectedLibraryIds = libs.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToArray();
        }

        if (TryGetConfigValue(dict, "Show4K", out var v)) config.Show4K = GetConfigBool(v);
        if (TryGetConfigValue(dict, "ShowHD", out v)) config.ShowHD = GetConfigBool(v);
        if (TryGetConfigValue(dict, "ShowQuality", out v)) config.ShowQuality = GetConfigBool(v);
        if (TryGetConfigValue(dict, "ShowAudioLanguageFlags", out v)) config.ShowAudioLanguageFlags = GetConfigBool(v);
        if (TryGetConfigValue(dict, "ShowImdbRating", out v)) config.ShowImdbRating = GetConfigBool(v);
        if (TryGetConfigValue(dict, "ShowRottenTomatoes", out v)) config.ShowRottenTomatoes = GetConfigBool(v);
        if (TryGetConfigValue(dict, "BadgePosition", out v) && Enum.TryParse<BadgePosition>(v.GetString(), out var badgePos))
        {
            config.BadgePosition = badgePos;
        }

        if (TryGetConfigValue(dict, "UseLetterResolution", out v)) config.UseLetterResolution = GetConfigBool(v);
        if (TryGetConfigValue(dict, "SkipItemsWithNoAudioLanguage", out v)) config.SkipItemsWithNoAudioLanguage = GetConfigBool(v);
        if (TryGetConfigValue(dict, "UseExternalRatings", out v)) config.UseExternalRatings = GetConfigBool(v);
        if (TryGetConfigValue(dict, "MdbListApiKey", out v)) config.MdbListApiKey = v.GetString() ?? string.Empty;
        if (TryGetConfigValue(dict, "TmdbApiKey", out v)) config.TmdbApiKey = v.GetString() ?? string.Empty;
        if (TryGetConfigValue(dict, "CustomTagEnabled", out v)) config.CustomTagEnabled = GetConfigBool(v);
        if (TryGetConfigValue(dict, "CustomTagText", out v)) config.CustomTagText = v.GetString() ?? string.Empty;
        if (TryGetConfigValue(dict, "CustomTagPosition", out v) && Enum.TryParse<BadgePosition>(v.GetString(), out var customPos))
        {
            config.CustomTagPosition = customPos;
        }

        if (TryGetConfigValue(dict, "TagCurvature", out v) && v.TryGetInt32(out var curv))
        {
            config.TagCurvature = Math.Clamp(curv, 0, 100);
        }
        if (TryGetConfigValue(dict, "AutoApplyOnLibraryScan", out v)) config.AutoApplyOnLibraryScan = GetConfigBool(v);
        if (TryGetConfigValue(dict, "ShowHDR", out v)) config.ShowHDR = GetConfigBool(v);
        if (TryGetConfigValue(dict, "ShowDolbyAtmos", out v)) config.ShowDolbyAtmos = GetConfigBool(v);
        if (TryGetConfigValue(dict, "ShowDtsX", out v)) config.ShowDtsX = GetConfigBool(v);
        return config;
    }
}

/// <summary>
/// Request body for POST Preview (live preview with config override).
/// Client must send camelCase keys (itemId, config) for ASP.NET Core default JSON binding.
/// </summary>
public class PreviewRequest
{
    /// <summary>Optional item ID. If null, a random item from libraries is used.</summary>
    [JsonPropertyName("itemId")]
    public string? ItemId { get; set; }

    /// <summary>Optional config override (same shape as plugin config). Used for live preview without saving.</summary>
    [JsonPropertyName("config")]
    public Dictionary<string, JsonElement>? Config { get; set; }
}

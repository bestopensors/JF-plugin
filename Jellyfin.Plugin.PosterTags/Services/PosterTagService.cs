using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Jellyfin.Plugin.PosterTags.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Jellyfin.Plugin.PosterTags.Services;

/// <summary>
/// A single badge to draw at a specific position.
/// </summary>
internal sealed record BadgeItem(string Text, BadgePosition Position);

/// <summary>
/// Service that draws tag badges (4K, HD, flags, IMDB, RT) onto poster images.
/// </summary>
public class PosterTagService
{
    private readonly ILogger<PosterTagService> _logger;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly IFileSystem _fileSystem;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="PosterTagService"/> class.
    /// </summary>
    public PosterTagService(
        ILogger<PosterTagService> logger,
        IMediaSourceManager mediaSourceManager,
        IFileSystem fileSystem)
    {
        _logger = logger;
        _mediaSourceManager = mediaSourceManager;
        _fileSystem = fileSystem;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Jellyfin-PosterTags/1.0");
    }

    /// <summary>
    /// Copies the item's primary poster to a destination path for live preview. Does not modify the original.
    /// Creates the destination directory if needed.
    /// </summary>
    public bool EnsurePreviewCopy(BaseItem item, string destPath)
    {
        if (item == null || string.IsNullOrWhiteSpace(destPath))
        {
            return false;
        }

        var primaryPath = item.GetImagePath(ImageType.Primary, 0);
        if (string.IsNullOrWhiteSpace(primaryPath) || !_fileSystem.FileExists(primaryPath))
        {
            return false;
        }

        try
        {
            var dir = System.IO.Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            System.IO.File.Copy(primaryPath, destPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Poster Tags: failed to copy preview image to {Path}", destPath);
            return false;
        }
    }

    /// <summary>
    /// Returns true if the item has a primary image file that exists on disk (for preview or processing).
    /// </summary>
    public bool HasUsablePrimaryImage(BaseItem item)
    {
        if (item == null)
        {
            return false;
        }

        var path = item.GetImagePath(ImageType.Primary, 0);
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return _fileSystem.FileExists(path);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns true if the item should be skipped (e.g. no known audio when skip is enabled).
    /// </summary>
    public bool ShouldSkipItem(BaseItem item, PluginConfiguration config)
    {
        if (config == null)
        {
            return true;
        }

        if (config.ShowAudioLanguageFlags && config.SkipItemsWithNoAudioLanguage)
        {
            var languages = GetAudioLanguages(item);
            if (languages.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Process a single item: load poster, draw badges, save to metadata path, update item.
    /// </summary>
    public async Task<bool> ProcessItemAsync(
        BaseItem item,
        PluginConfiguration config,
        CancellationToken cancellationToken)
    {
        if (config == null)
        {
            _logger.LogDebug("Poster Tags: config is null, skipping item.");
            return false;
        }

        if (ShouldSkipItem(item, config))
        {
            return false;
        }

        var primaryPath = item?.GetImagePath(ImageType.Primary, 0);
        if (string.IsNullOrWhiteSpace(primaryPath))
        {
            return false;
        }

        try
        {
            if (!_fileSystem.FileExists(primaryPath))
            {
                _logger.LogDebug("Poster Tags: primary image file does not exist: {Path}", primaryPath);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Poster Tags: error checking file existence for {Path}", primaryPath);
            return false;
        }

        Dictionary<string, double>? externalRatings = null;
        if (config.UseExternalRatings && !string.IsNullOrWhiteSpace(config.MdbListApiKey))
        {
            try
            {
                externalRatings = await FetchMdbListRatingsAsync(item!, config, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Poster Tags: could not fetch external ratings for {Name}", item!.Name);
            }
        }

        List<BadgeItem> badgeItems;
        try
        {
            badgeItems = BuildBadges(item!, config, externalRatings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Poster Tags: failed to build badges for {Name}", item!.Name);
            return false;
        }

        var hasCustomTag = config.CustomTagEnabled && !string.IsNullOrWhiteSpace(config.CustomTagText?.Trim());
        if (badgeItems.Count == 0 && !hasCustomTag)
        {
            return false;
        }

        var metadataPath = item!.GetInternalMetadataPath();
        if (string.IsNullOrWhiteSpace(metadataPath))
        {
            _logger.LogDebug("Poster Tags: no metadata path for item {Id}", item.Id);
            return false;
        }

        string? tempPath = null;
        string outputPath = System.IO.Path.Combine(metadataPath, "poster.png");

        try
        {
            using (var image = await Image.LoadAsync<Rgba32>(primaryPath, cancellationToken).ConfigureAwait(false))
            {
                DrawBadges(image, badgeItems, config);

                try
                {
                    Directory.CreateDirectory(metadataPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Poster Tags: could not create metadata directory {Path}", metadataPath);
                    return false;
                }

                tempPath = System.IO.Path.Combine(metadataPath, "poster_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp.png");

                await image.SaveAsPngAsync(tempPath, cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(tempPath) || !_fileSystem.FileExists(tempPath))
            {
                _logger.LogWarning("Poster Tags: temp file was not written for {Name}", item.Name);
                return false;
            }

            try
            {
                if (_fileSystem.FileExists(outputPath))
                {
                    _fileSystem.DeleteFile(outputPath);
                }

                File.Move(tempPath, outputPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Poster Tags: could not move temp to output for {Name}", item.Name);
                try
                {
                    if (!string.IsNullOrEmpty(tempPath) && _fileSystem.FileExists(tempPath))
                    {
                        _fileSystem.DeleteFile(tempPath);
                    }
                }
                catch
                {
                    // ignore cleanup failure
                }

                return false;
            }

            var primaryInfo = item.GetImageInfo(ImageType.Primary, 0);
            if (primaryInfo != null)
            {
                try
                {
                    primaryInfo.Path = outputPath;
                    primaryInfo.DateModified = _fileSystem.GetLastWriteTimeUtc(outputPath);
                    primaryInfo.Width = 0;
                    primaryInfo.Height = 0;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Poster Tags: could not update image info for {Name}", item.Name);
                    return false;
                }
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Poster Tags: failed to add poster tags for {Name} ({Path})", item.Name, primaryPath);
            try
            {
                if (!string.IsNullOrEmpty(tempPath) && _fileSystem.FileExists(tempPath))
                {
                    _fileSystem.DeleteFile(tempPath);
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }
    }

    /// <summary>
    /// Generates a preview image with badges drawn (same logic as ProcessItemAsync but returns PNG bytes without saving).
    /// Used for live preview in plugin settings. Fetches external ratings if config.UseExternalRatings is set.
    /// When sourceImagePath is set and exists, loads from that path (preview copy) instead of the item's primary image.
    /// </summary>
    public async Task<byte[]?> GetPreviewImageAsync(
        BaseItem item,
        PluginConfiguration config,
        string? sourceImagePath,
        CancellationToken cancellationToken)
    {
        if (config == null || item == null)
        {
            return null;
        }

        var primaryPath = sourceImagePath;
        if (string.IsNullOrWhiteSpace(primaryPath) || !_fileSystem.FileExists(primaryPath))
        {
            primaryPath = item.GetImagePath(ImageType.Primary, 0);
        }

        if (string.IsNullOrWhiteSpace(primaryPath))
        {
            return null;
        }

        try
        {
            if (!_fileSystem.FileExists(primaryPath))
            {
                return null;
            }
        }
        catch
        {
            return null;
        }

        Dictionary<string, double>? externalRatings = null;
        if (config.UseExternalRatings && !string.IsNullOrWhiteSpace(config.MdbListApiKey))
        {
            try
            {
                externalRatings = await FetchMdbListRatingsAsync(item, config, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // use fallback ratings
            }
        }

        List<BadgeItem> badgeItems;
        try
        {
            badgeItems = BuildBadges(item, config, externalRatings);
        }
        catch
        {
            return null;
        }

        var hasCustomTag = config.CustomTagEnabled && !string.IsNullOrWhiteSpace(config.CustomTagText?.Trim());
        if (badgeItems.Count == 0 && !hasCustomTag)
        {
            // Still return the poster image so preview always shows something; user can enable badges and refresh.
            try
            {
                using var image = await Image.LoadAsync<Rgba32>(primaryPath, cancellationToken).ConfigureAwait(false);
                using var ms = new MemoryStream();
                await image.SaveAsPngAsync(ms, cancellationToken).ConfigureAwait(false);
                return ms.ToArray();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Poster Tags: failed to load poster for preview {Name}", item.Name);
                return null;
            }
        }

        try
        {
            using var image = await Image.LoadAsync<Rgba32>(primaryPath, cancellationToken).ConfigureAwait(false);
            DrawBadges(image, badgeItems, config);
            using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms, cancellationToken).ConfigureAwait(false);
            return ms.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Poster Tags: failed to generate preview for {Name}", item.Name);
            return null;
        }
    }

    /// <summary>
    /// Fetch IMDb, Rotten Tomatoes, TMDB, Letterboxd ratings from MDBList API (https://api.mdblist.com).
    /// </summary>
    private async Task<Dictionary<string, double>?> FetchMdbListRatingsAsync(BaseItem item, PluginConfiguration config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.MdbListApiKey))
        {
            return null;
        }

        string? tmdbId = null;
        string type;

        if (item is Movie)
        {
            type = "movie";
            tmdbId = item.ProviderIds?.GetValueOrDefault("Tmdb");
        }
        else if (item is Series)
        {
            type = "show";
            tmdbId = item.ProviderIds?.GetValueOrDefault("Tmdb");
        }
        else if (item is Episode episode && episode.Series != null)
        {
            type = "show";
            tmdbId = episode.Series.ProviderIds?.GetValueOrDefault("Tmdb");
        }
        else
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(tmdbId))
        {
            return null;
        }

        var url = $"https://api.mdblist.com/tmdb/{type}/{tmdbId}?apikey={Uri.EscapeDataString(config.MdbListApiKey.Trim())}";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Poster Tags: MDBList API returned {Status} for {Name}", response.StatusCode, item.Name);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            if (!root.TryGetProperty("ratings", out var ratingsEl) || ratingsEl.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in ratingsEl.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var source = el.TryGetProperty("source", out var s) ? s.GetString()?.Trim() : null;
                if (string.IsNullOrEmpty(source))
                {
                    continue;
                }

                if (!el.TryGetProperty("value", out var v))
                {
                    continue;
                }

                double value;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out value) && value > 0)
                {
                    var key = source.Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant();
                    if (source.Contains("IMDb", StringComparison.OrdinalIgnoreCase))
                    {
                        result["imdb"] = value;
                    }
                    else if (source.Contains("Rotten Tomatoes", StringComparison.OrdinalIgnoreCase) && !source.Contains("Audience", StringComparison.OrdinalIgnoreCase))
                    {
                        result["rotten_tomatoes"] = value;
                    }
                    else if (source.Contains("Movie Database", StringComparison.OrdinalIgnoreCase) || source.Equals("TMDb", StringComparison.OrdinalIgnoreCase))
                    {
                        result["tmdb"] = value;
                    }
                    else if (source.Contains("Letterboxd", StringComparison.OrdinalIgnoreCase))
                    {
                        result["letterboxd"] = value;
                    }
                }
            }

            return result.Count > 0 ? result : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Poster Tags: MDBList request failed for {Name}", item.Name);
            return null;
        }
    }

    private static string GetResolutionString(int height, PluginConfiguration config)
    {
        string? letter = null;
        string? number = null;
        if (height >= 2160 && config.Show4K)
        {
            letter = "UHD";
            number = "4K";
        }
        else if (height >= 1080 && config.ShowHD)
        {
            letter = "FHD";
            number = "1080p";
        }
        else if (height >= 720 && config.ShowHD)
        {
            letter = "HD";
            number = "720p";
        }
        else if (height > 0)
        {
            letter = "SD";
            number = FormatResolutionNumber(height);
        }

        if (string.IsNullOrEmpty(letter) && string.IsNullOrEmpty(number))
        {
            return string.Empty;
        }

        return config.ResolutionFormat switch
        {
            ResolutionFormatOption.Letters => letter ?? number ?? string.Empty,
            ResolutionFormatOption.Numbers => number ?? letter ?? string.Empty,
            ResolutionFormatOption.Both => string.IsNullOrEmpty(number) || string.IsNullOrEmpty(letter)
                ? (number ?? letter ?? string.Empty)
                : number + " " + letter,
            _ => config.UseLetterResolution ? (letter ?? number ?? string.Empty) : (number ?? letter ?? string.Empty)
        };
    }

    private List<BadgeItem> BuildBadges(BaseItem item, PluginConfiguration config, Dictionary<string, double>? externalRatings = null)
    {
        var badges = new List<BadgeItem>();

        if (item is Video video && config.ShowQuality)
        {
            var height = video.Height;
            if (height <= 0)
            {
                try
                {
                    var streams = _mediaSourceManager.GetMediaStreams(item.Id);
                    var videoStream = streams?.FirstOrDefault(s => s.Type == MediaStreamType.Video && s.Height > 0);
                    if (videoStream != null && videoStream.Height.HasValue)
                    {
                        height = videoStream.Height.Value;
                    }
                }
                catch
                {
                    // use 0, no resolution badge
                }
            }

            var res = height > 0 ? GetResolutionString(height, config) : null;
            if (!string.IsNullOrEmpty(res))
            {
                badges.Add(new BadgeItem(res, config.ResolutionPosition));
            }
        }

        if (config.ShowAudioLanguageFlags)
        {
            var flagEmojis = GetAudioLanguageFlagEmojis(item);
            if (flagEmojis.Count > 0)
            {
                badges.Add(new BadgeItem(string.Join(" ", flagEmojis.Take(4)), config.AudioFlagsPosition));
            }
        }

        if (config.ShowImdbRating)
        {
            if (externalRatings != null && externalRatings.TryGetValue("imdb", out var imdbVal) && imdbVal > 0)
            {
                badges.Add(new BadgeItem($"IMDB {imdbVal:0.0}", config.ImdbPosition));
            }
            else if (item.CommunityRating.HasValue && item.CommunityRating.Value > 0)
            {
                badges.Add(new BadgeItem($"IMDB {item.CommunityRating.Value:0.0}", config.ImdbPosition));
            }
        }

        if (config.ShowRottenTomatoes)
        {
            if (externalRatings != null && externalRatings.TryGetValue("rotten_tomatoes", out var rtVal) && rtVal > 0)
            {
                var pct = rtVal <= 1 ? (int)Math.Round(rtVal * 100) : (int)Math.Round(rtVal);
                pct = Math.Clamp(pct, 0, 100);
                badges.Add(new BadgeItem($"RT {pct}%", config.RottenTomatoesPosition));
            }
            else if (item.CriticRating.HasValue && item.CriticRating.Value > 0)
            {
                var pct = (int)Math.Round(item.CriticRating.Value * 10);
                badges.Add(new BadgeItem($"RT {pct}%", config.RottenTomatoesPosition));
            }
        }

        if (config.ShowHDR)
        {
            var hdrBadge = GetHdrBadge(item);
            if (!string.IsNullOrEmpty(hdrBadge))
            {
                badges.Add(new BadgeItem(hdrBadge, config.HdrPosition));
            }
        }

        if (config.ShowDolbyAtmos || config.ShowDtsX)
        {
            var audioBadges = GetPremiumAudioBadges(item, config);
            foreach (var ab in audioBadges)
            {
                badges.Add(new BadgeItem(ab, config.AudioPosition));
            }
        }

        return badges;
    }

    /// <summary>
    /// Gets HDR badge text from video streams (HDR10, HDR10+, Dolby Vision, HLG).
    /// </summary>
    private string? GetHdrBadge(BaseItem item)
    {
        try
        {
            var streams = _mediaSourceManager.GetMediaStreams(item.Id);
            if (streams == null)
            {
                return null;
            }

            string? best = null;
            foreach (var stream in streams)
            {
                if (stream.Type != MediaStreamType.Video)
                {
                    continue;
                }

                var rangeType = stream.VideoRangeType.ToString();
                var doviTitle = stream.VideoDoViTitle;
                if (!string.IsNullOrEmpty(doviTitle))
                {
                    if (doviTitle.Contains("Dolby Vision", StringComparison.OrdinalIgnoreCase))
                    {
                        best = "Dolby Vision";
                        break;
                    }
                }

                if (rangeType.Contains("DOVI", StringComparison.OrdinalIgnoreCase) || rangeType.Contains("Dolby", StringComparison.OrdinalIgnoreCase))
                {
                    best = "Dolby Vision";
                    break;
                }

                if (rangeType.Contains("HDR10Plus", StringComparison.OrdinalIgnoreCase))
                {
                    best = "HDR10+";
                }
                else if (rangeType.Contains("HDR10", StringComparison.OrdinalIgnoreCase) && best == null)
                {
                    best = "HDR10";
                }
                else if (rangeType.Contains("HLG", StringComparison.OrdinalIgnoreCase) && best == null)
                {
                    best = "HLG";
                }
                else if (rangeType.Contains("HDR", StringComparison.OrdinalIgnoreCase) && best == null)
                {
                    best = "HDR";
                }
            }

            return best;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Poster Tags: could not get HDR badge for {Id}", item.Id);
            return null;
        }
    }

    /// <summary>
    /// Gets premium audio badges (Dolby Atmos, DTS:X) from audio streams.
    /// </summary>
    private List<string> GetPremiumAudioBadges(BaseItem item, PluginConfiguration config)
    {
        var list = new List<string>();
        try
        {
            var streams = _mediaSourceManager.GetMediaStreams(item.Id);
            if (streams == null)
            {
                return list;
            }

            var hasAtmos = false;
            var hasDtsX = false;
            foreach (var stream in streams)
            {
                if (stream.Type != MediaStreamType.Audio)
                {
                    continue;
                }

                var profile = stream.Profile ?? string.Empty;
                if (config.ShowDolbyAtmos && profile.Contains("Dolby Atmos", StringComparison.OrdinalIgnoreCase))
                {
                    hasAtmos = true;
                }

                if (config.ShowDtsX && profile.Contains("DTS:X", StringComparison.OrdinalIgnoreCase))
                {
                    hasDtsX = true;
                }

                if (hasAtmos && hasDtsX)
                {
                    break;
                }
            }

            if (hasAtmos)
            {
                list.Add("Dolby Atmos");
            }

            if (hasDtsX)
            {
                list.Add("DTS:X");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Poster Tags: could not get premium audio badges for {Id}", item.Id);
        }

        return list;
    }

    private static string FormatResolutionNumber(int height)
    {
        if (height >= 2160)
        {
            return "2160p";
        }

        if (height >= 1080)
        {
            return "1080p";
        }

        if (height >= 720)
        {
            return "720p";
        }

        if (height >= 576)
        {
            return "576p";
        }

        if (height >= 480)
        {
            return "480p";
        }

        return height > 0 ? height + "p" : string.Empty;
    }

    private List<string> GetAudioLanguages(BaseItem item)
    {
        var list = new List<string>();
        try
        {
            var streams = _mediaSourceManager.GetMediaStreams(item.Id);
            if (streams == null)
            {
                return list;
            }

            foreach (var stream in streams)
            {
                if (stream.Type != MediaStreamType.Audio)
                {
                    continue;
                }

                var lang = stream.Language?.Trim();
                if (string.IsNullOrWhiteSpace(lang))
                {
                    continue;
                }

                if (lang.Length >= 2 && !list.Contains(lang, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(lang);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Poster Tags: could not get media streams for {Id}", item.Id);
        }

        return list;
    }

    /// <summary>
    /// Get flag emoji strings for audio languages (e.g. 🇺🇸 🇬🇧).
    /// </summary>
    private List<string> GetAudioLanguageFlagEmojis(BaseItem item)
    {
        var languages = GetAudioLanguages(item);
        var flags = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var lang in languages.Take(4))
        {
            var flag = LanguageToFlagEmoji(lang);
            if (!string.IsNullOrEmpty(flag) && seen.Add(flag))
            {
                flags.Add(flag);
            }
        }

        return flags;
    }

    /// <summary>
    /// Map ISO 639 language code to Unicode flag emoji (regional indicators).
    /// </summary>
    private static string? LanguageToFlagEmoji(string language)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Length < 2)
        {
            return null;
        }

        var key = language.Length >= 3 ? language[..3].ToLowerInvariant() : language.ToLowerInvariant();
        string? countryCode = key switch
        {
            "eng" or "en" => "US",
            "spa" or "es" => "ES",
            "fra" or "fr" => "FR",
            "deu" or "ger" or "de" => "DE",
            "ita" or "it" => "IT",
            "por" or "pt" => "PT",
            "jpn" or "ja" => "JP",
            "kor" or "ko" => "KR",
            "zho" or "chi" or "zh" => "CN",
            "rus" or "ru" => "RU",
            "ara" or "ar" => "SA",
            "hin" or "hi" => "IN",
            "nld" or "dut" or "nl" => "NL",
            "pol" or "pl" => "PL",
            "tur" or "tr" => "TR",
            "vie" or "vi" => "VN",
            "tha" or "th" => "TH",
            "ind" or "id" => "ID",
            "ces" or "cze" or "cs" => "CZ",
            "swe" or "sv" => "SE",
            "dan" or "da" => "DK",
            "nor" or "no" => "NO",
            "fin" or "fi" => "FI",
            "ell" or "gre" or "el" => "GR",
            "heb" or "he" => "IL",
            _ => language.Length >= 2 ? language[..2].ToUpperInvariant() : null
        };

        if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2)
        {
            return null;
        }

        return CountryCodeToFlagEmoji(countryCode);
    }

    /// <summary>
    /// Convert two-letter ISO country code to flag emoji (e.g. US -> 🇺🇸).
    /// </summary>
    private static string CountryCodeToFlagEmoji(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 2)
        {
            return string.Empty;
        }

        code = code.ToUpperInvariant();
        var first = 0x1F1E6 + (code[0] - 'A');
        var second = 0x1F1E6 + (code[1] - 'A');
        return char.ConvertFromUtf32(first) + char.ConvertFromUtf32(second);
    }

    private static Font GetBadgeFont(int fontSize)
    {
        var names = new[] { "Arial", "Segoe UI", "Helvetica", "Liberation Sans", "DejaVu Sans", "sans-serif" };
        foreach (var name in names)
        {
            try
            {
                return SystemFonts.CreateFont(name, fontSize, FontStyle.Bold);
            }
            catch
            {
                // try next
            }
        }

        var families = SystemFonts.Collection.Families.ToArray();
        if (families.Length > 0)
        {
            try
            {
                return SystemFonts.CreateFont(families[0].Name, fontSize, FontStyle.Bold);
            }
            catch
            {
                // fall through
            }
        }

        return SystemFonts.CreateFont("Arial", fontSize, FontStyle.Bold);
    }

    private void DrawBadges(Image<Rgba32> image, List<BadgeItem> badgeItems, PluginConfiguration config)
    {
        if (config == null)
        {
            return;
        }

        var fontSize = Math.Clamp(config.TagSize, 12, 28);
        var padding = Math.Max(4, fontSize / 2);
        var lineHeight = fontSize + 10;
        var curvature = Math.Clamp(config.TagCurvature, 0, 100);
        var semiBlack = new Color(new Rgba32(0, 0, 0, 180));

        try
        {
            var font = GetBadgeFont(fontSize);

            // Draw each badge at its configured position
            if (badgeItems != null)
            {
                foreach (var item in badgeItems)
                {
                    if (string.IsNullOrEmpty(item.Text))
                    {
                        continue;
                    }

                    var size = TextMeasurer.MeasureSize(item.Text, new TextOptions(font));
                    var boxWidth = (int)Math.Ceiling(size.Width) + padding * 2;
                    var boxHeight = lineHeight + padding * 2;
                    GetPosition(item.Position, image.Width, image.Height, boxWidth, boxHeight, padding, out var x, out var y);
                    var rect = new SixLabors.ImageSharp.RectangleF(x, y, boxWidth, boxHeight);
                    DrawTagBox(image, rect, semiBlack, font, new List<string> { item.Text }, padding, lineHeight, curvature);
                }
            }

            // Draw custom tag if enabled
            if (config.CustomTagEnabled && !string.IsNullOrWhiteSpace(config.CustomTagText))
            {
                var customText = config.CustomTagText.Trim();
                var size = TextMeasurer.MeasureSize(customText, new TextOptions(font));
                var boxWidth = (int)Math.Ceiling(size.Width) + padding * 2;
                var boxHeight = lineHeight + padding * 2;
                GetPosition(config.CustomTagPosition, image.Width, image.Height, boxWidth, boxHeight, padding, out var cx, out var cy);
                var rect = new SixLabors.ImageSharp.RectangleF(cx, cy, boxWidth, boxHeight);
                DrawTagBox(image, rect, semiBlack, font, new List<string> { customText }, padding, lineHeight, curvature);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Poster Tags: could not draw badge text; Badges: {Count}", badgeItems?.Count ?? 0);
        }
    }

    private static void GetPosition(BadgePosition position, int imageWidth, int imageHeight, int boxWidth, int boxHeight, int padding, out int x, out int y)
    {
        switch (position)
        {
            case BadgePosition.TopLeft:
                x = padding;
                y = padding;
                break;
            case BadgePosition.TopRight:
                x = imageWidth - boxWidth - padding;
                y = padding;
                break;
            case BadgePosition.TopCenter:
                x = Math.Max(padding, (imageWidth - boxWidth) / 2);
                y = padding;
                break;
            case BadgePosition.BottomLeft:
                x = padding;
                y = imageHeight - boxHeight - padding;
                break;
            case BadgePosition.BottomRight:
                x = imageWidth - boxWidth - padding;
                y = imageHeight - boxHeight - padding;
                break;
            case BadgePosition.BottomCenter:
                x = Math.Max(padding, (imageWidth - boxWidth) / 2);
                y = imageHeight - boxHeight - padding;
                break;
            default:
                x = padding;
                y = imageHeight - boxHeight - padding;
                break;
        }

        x = Math.Max(0, Math.Min(x, imageWidth - boxWidth));
        y = Math.Max(0, Math.Min(y, imageHeight - boxHeight));
    }

    private void DrawTagBox(
        Image<Rgba32> image,
        SixLabors.ImageSharp.RectangleF rect,
        Color fillColor,
        Font font,
        List<string> lines,
        int padding,
        int lineHeight,
        int curvaturePercent)
    {
        var radius = curvaturePercent <= 0 ? 0f : (curvaturePercent / 100f) * Math.Min(rect.Width, rect.Height) / 2f;
        radius = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

        image.Mutate(ctx =>
        {
            if (radius <= 0)
            {
                ctx.Fill(fillColor, rect);
            }
            else
            {
                var path = BuildRoundedRectPath(rect, radius);
                ctx.Fill(fillColor, path);
            }

            var textY = rect.Y + padding;
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    textY += lineHeight;
                    continue;
                }

                ctx.DrawText(line, font, Color.White, new PointF(rect.X + padding, textY));
                textY += lineHeight;
            }
        });
    }

    /// <summary>
    /// Builds a rounded rectangle path. Curvature 0 = sharp corners, full curvature = pill (half-circle ends).
    /// </summary>
    private static IPath BuildRoundedRectPath(SixLabors.ImageSharp.RectangleF rect, float cornerRadius)
    {
        var left = rect.Left;
        var top = rect.Top;
        var right = rect.Right;
        var bottom = rect.Bottom;
        var r = Math.Min(cornerRadius, Math.Min(rect.Width, rect.Height) / 2f);
        if (r <= 0)
        {
            var pb = new PathBuilder();
            pb.AddLine(new PointF(left, top), new PointF(right, top));
            pb.AddLine(new PointF(right, top), new PointF(right, bottom));
            pb.AddLine(new PointF(right, bottom), new PointF(left, bottom));
            pb.AddLine(new PointF(left, bottom), new PointF(left, top));
            return pb.CloseFigure().Build();
        }

        const int arcSteps = 8;
        var points = new List<PointF>();
        // Top edge: (left+r, top) -> (right-r, top)
        points.Add(new PointF(left + r, top));
        points.Add(new PointF(right - r, top));
        // Top-right arc: center (right-r, top+r), from 270° to 360°
        AddArcPoints(points, right - r, top + r, r, 270f, 90f, arcSteps);
        // Right edge
        points.Add(new PointF(right, bottom - r));
        // Bottom-right arc: center (right-r, bottom-r), from 0° to 90°
        AddArcPoints(points, right - r, bottom - r, r, 0f, 90f, arcSteps);
        // Bottom edge
        points.Add(new PointF(left + r, bottom));
        // Bottom-left arc: center (left+r, bottom-r), from 90° to 180°
        AddArcPoints(points, left + r, bottom - r, r, 90f, 90f, arcSteps);
        // Left edge
        points.Add(new PointF(left, top + r));
        // Top-left arc: center (left+r, top+r), from 180° to 270°
        AddArcPoints(points, left + r, top + r, r, 180f, 90f, arcSteps);

        var builder = new PathBuilder();
        for (var i = 0; i < points.Count - 1; i++)
        {
            builder.AddLine(points[i], points[i + 1]);
        }

        builder.AddLine(points[points.Count - 1], points[0]);
        return builder.CloseFigure().Build();
    }

    private static void AddArcPoints(List<PointF> points, float cx, float cy, float radius, float startAngleDeg, float sweepDeg, int steps)
    {
        for (var i = 1; i <= steps; i++)
        {
            var angleDeg = startAngleDeg + (sweepDeg * i / steps);
            var rad = angleDeg * MathF.PI / 180f;
            points.Add(new PointF(cx + radius * MathF.Cos(rad), cy + radius * MathF.Sin(rad)));
        }
    }
}

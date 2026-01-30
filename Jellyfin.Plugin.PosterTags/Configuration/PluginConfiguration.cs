using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PosterTags.Configuration;

/// <summary>
/// Plugin configuration for poster tag overlays.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        SelectedLibraryIds = Array.Empty<string>();
        Show4K = true;
        ShowHD = true;
        ShowQuality = true;
        ShowAudioLanguageFlags = true;
        ShowImdbRating = true;
        ShowRottenTomatoes = true;
        BadgePosition = BadgePosition.BottomLeft;
        UseLetterResolution = true;
        SkipItemsWithNoAudioLanguage = true;
        UseExternalRatings = false;
        MdbListApiKey = string.Empty;
        TmdbApiKey = string.Empty;
        CustomTagEnabled = false;
        CustomTagText = string.Empty;
        CustomTagPosition = BadgePosition.TopLeft;
        TagCurvature = 0;
        TagSize = 18;
        ResolutionFormat = ResolutionFormatOption.Letters;
        ResolutionPosition = BadgePosition.BottomLeft;
        ImdbPosition = BadgePosition.BottomLeft;
        RottenTomatoesPosition = BadgePosition.BottomLeft;
        HdrPosition = BadgePosition.BottomLeft;
        AudioPosition = BadgePosition.BottomLeft;
        AudioFlagsPosition = BadgePosition.BottomLeft;
        AutoApplyOnLibraryScan = true;
        ShowHDR = true;
        ShowDolbyAtmos = true;
        ShowDtsX = true;
    }

    /// <summary>
    /// Gets or sets the library IDs selected for poster tagging (empty = all libraries when running task).
    /// </summary>
    public string[] SelectedLibraryIds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show 4K/UHD badge.
    /// </summary>
    public bool Show4K { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show HD badge.
    /// </summary>
    public bool ShowHD { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show resolution badge.
    /// </summary>
    public bool ShowQuality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show audio language flags (emoji).
    /// </summary>
    public bool ShowAudioLanguageFlags { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show IMDB-style rating.
    /// </summary>
    public bool ShowImdbRating { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show Rotten Tomatoes (critic) rating.
    /// </summary>
    public bool ShowRottenTomatoes { get; set; }

    /// <summary>
    /// Gets or sets the position of badges on the poster.
    /// </summary>
    public BadgePosition BadgePosition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use letter resolution (SD, HD, FHD, UHD) instead of numbered (480p, 720p, 1080p, 2160p).
    /// </summary>
    public bool UseLetterResolution { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to skip items that have no known audio language (when audio flags are enabled).
    /// </summary>
    public bool SkipItemsWithNoAudioLanguage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to fetch IMDb/Rotten Tomatoes ratings from external APIs (MDBList) when API keys are set.
    /// </summary>
    public bool UseExternalRatings { get; set; }

    /// <summary>
    /// Gets or sets the MDBList API key (https://mdblist.com) for fetching IMDb, Rotten Tomatoes, TMDB, Letterboxd ratings.
    /// </summary>
    public string MdbListApiKey { get; set; }

    /// <summary>
    /// Gets or sets the TMDB API key (optional, for episode/season ratings from The Movie Database).
    /// </summary>
    public string TmdbApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show a custom text tag on the poster.
    /// </summary>
    public bool CustomTagEnabled { get; set; }

    /// <summary>
    /// Gets or sets the custom tag text (e.g. "NEW", "FAVORITE").
    /// </summary>
    public string CustomTagText { get; set; }

    /// <summary>
    /// Gets or sets the position of the custom tag on the poster.
    /// </summary>
    public BadgePosition CustomTagPosition { get; set; }

    /// <summary>
    /// Gets or sets the tag edge curvature (0 = rectangle, 100 = pill). Applies to main badges and custom tag.
    /// </summary>
    public int TagCurvature { get; set; }

    /// <summary>
    /// Gets or sets the font size for tags (12–28). Applies to all badges and custom tag.
    /// </summary>
    public int TagSize { get; set; }

    /// <summary>
    /// Gets or sets resolution display format: Letters (SD/HD/FHD/UHD), Numbers (480p/720p/1080p/4K), or Both.
    /// </summary>
    public ResolutionFormatOption ResolutionFormat { get; set; }

    /// <summary>
    /// Gets or sets the position of the resolution badge.
    /// </summary>
    public BadgePosition ResolutionPosition { get; set; }

    /// <summary>
    /// Gets or sets the position of the IMDb rating badge.
    /// </summary>
    public BadgePosition ImdbPosition { get; set; }

    /// <summary>
    /// Gets or sets the position of the Rotten Tomatoes badge.
    /// </summary>
    public BadgePosition RottenTomatoesPosition { get; set; }

    /// <summary>
    /// Gets or sets the position of the HDR badge.
    /// </summary>
    public BadgePosition HdrPosition { get; set; }

    /// <summary>
    /// Gets or sets the position of the audio badges (Dolby Atmos, DTS:X).
    /// </summary>
    public BadgePosition AudioPosition { get; set; }

    /// <summary>
    /// Gets or sets the position of the audio language flags badge.
    /// </summary>
    public BadgePosition AudioFlagsPosition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically apply poster tags when a library scan completes.
    /// </summary>
    public bool AutoApplyOnLibraryScan { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show HDR badge (HDR10, HDR10+, Dolby Vision, HLG).
    /// </summary>
    public bool ShowHDR { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show Dolby Atmos badge.
    /// </summary>
    public bool ShowDolbyAtmos { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to show DTS:X badge.
    /// </summary>
    public bool ShowDtsX { get; set; }
}

/// <summary>
/// Resolution display format: letters only, numbers only, or both.
/// </summary>
public enum ResolutionFormatOption
{
    /// <summary>Letters only (SD, HD, FHD, UHD).</summary>
    Letters = 0,

    /// <summary>Numbers only (480p, 720p, 1080p, 4K).</summary>
    Numbers = 1,

    /// <summary>Both (e.g. 1080p FHD).</summary>
    Both = 2,
}

/// <summary>
/// Position of badge overlays on the poster.
/// </summary>
public enum BadgePosition
{
    /// <summary>Top-left corner.</summary>
    TopLeft,

    /// <summary>Top-right corner.</summary>
    TopRight,

    /// <summary>Top center.</summary>
    TopCenter,

    /// <summary>Bottom-left corner.</summary>
    BottomLeft,

    /// <summary>Bottom-right corner.</summary>
    BottomRight,

    /// <summary>Bottom center.</summary>
    BottomCenter,
}

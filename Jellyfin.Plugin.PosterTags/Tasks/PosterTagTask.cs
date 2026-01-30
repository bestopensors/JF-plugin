using System.Globalization;
using Jellyfin.Plugin.PosterTags.Configuration;
using Jellyfin.Plugin.PosterTags.Services;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PosterTags.Tasks;

/// <summary>
/// Scheduled task that adds poster tags (4K, HD, flags, IMDB, RT) to items in selected libraries.
/// </summary>
public class PosterTagTask : IScheduledTask
{
    private readonly ILogger<PosterTagTask> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly PosterTagService _posterTagService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PosterTagTask"/> class.
    /// </summary>
    public PosterTagTask(
        ILogger<PosterTagTask> logger,
        ILibraryManager libraryManager,
        PosterTagService posterTagService)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _posterTagService = posterTagService;
    }

    /// <inheritdoc />
    public string Name => "Add Poster Tags";

    /// <inheritdoc />
    public string Key => "PosterTagsAddTags";

    /// <inheritdoc />
    public string Description => "Add quality, 4K, HD, audio language, IMDB and Rotten Tomatoes badges to posters in selected libraries.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin?.Configuration == null)
        {
            _logger.LogWarning("Poster Tags plugin or configuration not available.");
            return;
        }

        var config = plugin.Configuration;
        var libraryIds = config.SelectedLibraryIds;
        if (libraryIds == null || libraryIds.Length == 0)
        {
            var root = _libraryManager.RootFolder;
            if (root == null)
            {
                return;
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
            _logger.LogInformation("No libraries selected for poster tagging.");
            return;
        }

        var query = new InternalItemsQuery
        {
            AncestorIds = guids,
            IsVirtualItem = false,
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode, BaseItemKind.Video, BaseItemKind.MusicVideo }
        };

        var items = _libraryManager.GetItemList(query)
            .Where(i => i != null && i.HasImage(ImageType.Primary, 0))
            .Where(i => !_posterTagService.ShouldSkipItem(i, config))
            .ToList();
        var total = items.Count;
        var processed = 0;
        var updated = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (item == null || config == null)
                {
                    processed++;
                    progress.Report(total > 0 ? (double)processed / total * 100 : 0);
                    continue;
                }

                var changed = await _posterTagService.ProcessItemAsync(item, config, cancellationToken).ConfigureAwait(false);
                if (changed)
                {
                    try
                    {
                        var parent = item.GetParent();
                        await _libraryManager.UpdateItemAsync(item, parent, ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);
                        updated++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Poster Tags: failed to update item in library for {Name}.", item.Name);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Poster Tags: failed to process {Name}.", item?.Name ?? "unknown");
            }

            processed++;
            progress.Report(total > 0 ? (double)processed / total * 100 : 0);
        }

        _logger.LogInformation("Poster tags task completed. Processed {Processed} items, updated {Updated}.", processed, updated);
    }
}

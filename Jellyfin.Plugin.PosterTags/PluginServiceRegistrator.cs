using Jellyfin.Plugin.PosterTags.Services;
using Jellyfin.Plugin.PosterTags.Tasks;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.PosterTags;

/// <summary>
/// Registers plugin services with the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, MediaBrowser.Controller.IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<PosterTagService>();
        serviceCollection.AddSingleton<PosterTagTask>();
        serviceCollection.AddSingleton<PosterTagLibraryPostScanTask>();
    }
}

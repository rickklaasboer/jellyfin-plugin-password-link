using Jellyfin.Plugin.PasswordLink.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PasswordLink;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<PasswordLinkStore>();

        // Factory instead of plain registration: TimeProvider and the configuration accessor
        // must not be registered globally in Jellyfin's container.
        serviceCollection.AddSingleton(sp => new PasswordLinkService(
            sp.GetRequiredService<PasswordLinkStore>(),
            sp.GetRequiredService<IUserManager>(),
            sp.GetRequiredService<ISessionManager>(),
            TimeProvider.System,
            () => Plugin.Instance?.Configuration
                ?? throw new InvalidOperationException("Password Link plugin is not initialized."),
            sp.GetRequiredService<ILogger<PasswordLinkService>>()));
    }
}

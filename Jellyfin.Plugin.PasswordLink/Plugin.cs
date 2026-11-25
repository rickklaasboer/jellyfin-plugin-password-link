using Jellyfin.Plugin.PasswordLink.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.PasswordLink;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Password Link";

    public override Guid Id => Guid.Parse("2ae0d2b9-67aa-486d-a157-3d0464f6be86");

    public override string Description => "Send users a one-time link to set their own password.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;
        return
        [
            new PluginPageInfo
            {
                Name = "PasswordLink",
                DisplayName = "Password Link",
                EmbeddedResourcePath = $"{ns}.Configuration.configPage.html",
                EnableInMainMenu = true,
            },
            new PluginPageInfo
            {
                Name = "PasswordLinkJs",
                EmbeddedResourcePath = $"{ns}.Configuration.configPage.js",
            },
        ];
    }
}

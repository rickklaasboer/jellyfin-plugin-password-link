using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PasswordLink.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public int DefaultExpiryDays { get; set; } = 7;

    public int MinPasswordLength { get; set; } = 8;
}

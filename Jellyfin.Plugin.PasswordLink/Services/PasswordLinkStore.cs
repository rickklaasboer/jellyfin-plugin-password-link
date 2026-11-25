using System.Text.Json;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.PasswordLink.Services;

public class PasswordLinkStore
{
    private readonly string _path;

    public PasswordLinkStore(IApplicationPaths applicationPaths)
        : this(Path.Combine(applicationPaths.DataPath, "password-link", "links.json"))
    {
    }

    internal PasswordLinkStore(string path)
    {
        _path = path;
    }

    public virtual List<PasswordLinkRecord> Load()
    {
        if (!File.Exists(_path)) return [];

        return JsonSerializer.Deserialize<List<PasswordLinkRecord>>(File.ReadAllText(_path))
            ?? throw new InvalidDataException($"Password link store {_path} contains no list.");
    }

    public virtual void Save(IEnumerable<PasswordLinkRecord> links)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        // Write to a temp file first so a crash mid-write never leaves a truncated store.
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(links));
        File.Move(tempPath, _path, overwrite: true);
    }
}

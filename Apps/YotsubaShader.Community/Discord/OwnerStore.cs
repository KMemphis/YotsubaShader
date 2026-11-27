using System.Text.Json;

namespace YotsubaShader.Community.Server.Discord;

/// <summary>
/// Remembers which signed-in user wrote each message the Hub posted. Discord only shows a webhook's display name, so
/// this is the only reliable way to know who may edit or delete a message. Kept in a small JSON file.
/// </summary>
public sealed class OwnerStore
{
    private readonly string _path;
    private readonly Dictionary<ulong, string> _owners;

    public OwnerStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "owners.json");
        try { _owners = File.Exists(_path) ? JsonSerializer.Deserialize<Dictionary<ulong, string>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { _owners = []; }
    }

    public string? OwnerOf(ulong messageId) { lock (_owners) return _owners.GetValueOrDefault(messageId); }

    public void Set(ulong messageId, string userId)
    {
        lock (_owners)
        {
            _owners[messageId] = userId;
            Save();
        }
    }

    public void Remove(ulong messageId)
    {
        lock (_owners) if (_owners.Remove(messageId)) Save();
    }

    private void Save()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_owners));
        File.Move(tmp, _path, overwrite: true);
    }
}

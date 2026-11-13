using System.Text.Json;

namespace SpaceBattle.Lib;

public record ServerSnapshot(int Version, WorldSnapshot[] Games);

public class SnapshotStore(string path)
{
    public async Task SaveAsync(WorldSnapshot[] games, CancellationToken token = default)
    {
        foreach (var game in games) GameWorld.Validate(game);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new ServerSnapshot(1, games)), token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public async Task<WorldSnapshot[]> LoadAsync(CancellationToken token = default)
    {
        if (!File.Exists(path)) return Array.Empty<WorldSnapshot>();
        var snapshot = JsonSerializer.Deserialize<ServerSnapshot>(await File.ReadAllTextAsync(path, token));
        if (snapshot == null || snapshot.Version != 1 || snapshot.Games == null) throw new InvalidDataException("Unsupported server snapshot.");
        foreach (var game in snapshot.Games) GameWorld.Validate(game);
        if (snapshot.Games.Select(item => item.GameId).Distinct().Count() != snapshot.Games.Length)
            throw new InvalidDataException("Duplicate game IDs.");
        return snapshot.Games;
    }
}

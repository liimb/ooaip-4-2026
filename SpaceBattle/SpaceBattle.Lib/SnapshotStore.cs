using System.Text.Json;

namespace SpaceBattle.Lib;

public record ServerSnapshot(int Version, WorldSnapshot[] Games);

public class SnapshotStore(string path)
{
    public async Task SaveAsync(WorldSnapshot[] games, CancellationToken token = default)
    {
        foreach (var game in games) GameWorld.Validate(game);
        await WriteDocumentAsync(new ServerSnapshot(1, games), token);
    }

    internal async Task WriteDocumentAsync<T>(T document, CancellationToken token)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, document, cancellationToken: token);
                await stream.FlushAsync(token);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal async Task<T?> ReadDocumentAsync<T>(CancellationToken token)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, FileOptions.Asynchronous);
            var document = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: token);
            if (document is null) throw new InvalidDataException("Stored document must not be null.");
            return document;
        }
        catch (FileNotFoundException) { return default; }
        catch (DirectoryNotFoundException) { return default; }
    }

    internal async Task<FileStream> LockAsync(CancellationToken token)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return new FileStream(fullPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous); }
            catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33 or 11)
            { await Task.Delay(10, token); }
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

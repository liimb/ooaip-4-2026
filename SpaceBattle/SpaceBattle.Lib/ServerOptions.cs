using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceBattle.Lib;

public class ServerOptions
{
    public WorkerOptions Workers { get; init; } = new();
    public GameOptions Games { get; init; } = new();
    public EndpointOptions Endpoint { get; init; } = new();
    public int MaxGames { get; init; } = 1024;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Workers);
        ArgumentNullException.ThrowIfNull(Games);
        ArgumentNullException.ThrowIfNull(Endpoint);
        Workers.Validate();
        Games.Validate();
        Endpoint.Validate();
        if (MaxGames <= 0) throw new ArgumentOutOfRangeException(nameof(MaxGames));
    }

    public static ServerOptions FromJson(string json)
    {
        var options = JsonSerializer.Deserialize<ServerOptions>(json, new JsonSerializerOptions
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new ArgumentException("Server configuration must be an object.", nameof(json));
        options.Validate();
        return options;
    }
}

public class EndpointOptions
{
    public string Url { get; init; } = "http://127.0.0.1:5080";
    public int MaxRequestBytes { get; init; } = 16384;

    public void Validate()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != "http" ||
            uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
            throw new ArgumentException("Endpoint URL must be an absolute HTTP origin.", nameof(Url));
        if (MaxRequestBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaxRequestBytes));
    }
}

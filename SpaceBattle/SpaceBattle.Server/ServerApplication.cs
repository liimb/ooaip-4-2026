using SpaceBattle.Lib;
using GameEndpoint = SpaceBattle.Lib.Endpoint;

namespace SpaceBattle.Server;

public class ServerApplication
{
    public static WebApplication Create(ServerOptions options, GameEndpoint endpoint)
    {
        options.Validate();
        ArgumentNullException.ThrowIfNull(endpoint);
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(options.Endpoint.Url);
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.Limits.MaxRequestBodySize = options.Endpoint.MaxRequestBytes;
            server.Limits.MaxConcurrentConnections = 128;
        });
        var app = builder.Build();
        app.MapPost("/commands", (GameMessage message) =>
        {
            var result = endpoint.Accept(message);
            var status = result switch
            {
                DispatchResult.Accepted => StatusCodes.Status202Accepted,
                DispatchResult.UnknownGame => StatusCodes.Status404NotFound,
                DispatchResult.Overloaded => StatusCodes.Status429TooManyRequests,
                DispatchResult.Stopped => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status400BadRequest
            };
            return Results.Json(new { Status = result.ToString() }, statusCode: status);
        });
        return app;
    }
}

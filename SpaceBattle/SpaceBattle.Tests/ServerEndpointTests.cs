using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using App;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Lib;
using SpaceBattle.Server;

namespace SpaceBattle.Tests;

public class ServerEndpointTests
{
    [Fact]
    public async Task HttpEndpointRoutesCommandsAndReturnsExplicitRejections()
    {
        var options = new ServerOptions
        {
            Workers = new WorkerOptions { WorkerCount = 1, QueueCapacity = 1 },
            Endpoint = new EndpointOptions { Url = "http://127.0.0.1:0", MaxRequestBytes = 1024 }
        };
        var diagnostics = new List<GameDiagnostic>();
        var server = new GameServer(options, diagnostics.Add);
        var count = 0;
        var game = server.CreateGame(Guid.NewGuid());
        var endpoint = new Endpoint(server.Dispatcher, new Dictionary<string, Func<JsonElement, ICommand>>
        {
            ["Count"] = _ => new TestAction(() => count++)
        });
        var app = ServerApplication.Create(options, endpoint);
        try
        {
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(10) };
            using var accepted = await client.PostAsJsonAsync("/commands", new { GameId = game.Id, Command = "Count", Arguments = new { } });
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            Assert.Equal(0, count);
            using var full = await client.PostAsJsonAsync("/commands", new { GameId = game.Id, Command = "Count", Arguments = new { } });
            Assert.Equal(HttpStatusCode.TooManyRequests, full.StatusCode);
            using var unknown = await client.PostAsJsonAsync("/commands", new { GameId = Guid.NewGuid(), Command = "Count", Arguments = new { } });
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
            using var invalid = await client.PostAsJsonAsync("/commands", new { GameId = game.Id, Command = "Delete", Arguments = new { } });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            using var invalidJson = new StringContent("{broken", Encoding.UTF8, "application/json");
            using var malformed = await client.PostAsync("/commands", invalidJson);
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
            using var large = await client.PostAsJsonAsync("/commands", new { GameId = game.Id, Command = "Count", Arguments = new { Data = new string('x', 2000) } });
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
            server.Stop();
            Assert.Equal(1, count);
            Assert.DoesNotContain(diagnostics, item => item.Error != null);
            using var stopped = await client.PostAsJsonAsync("/commands", new { GameId = game.Id, Command = "Count", Arguments = new { } });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, stopped.StatusCode);
        }
        finally
        {
            try { await app.StopAsync(); }
            finally
            {
                await app.DisposeAsync();
                server.Stop();
            }
        }
    }

    [Fact]
    public void GeneratedMoveCommandRunsThroughEndpointInGameScope()
    {
        var server = new GameServer(new ServerOptions());
        var data = new Dictionary<string, object>
        {
            ["Position"] = new Vector(new[] { 0, 0 }),
            ["Velocity"] = new Vector(new[] { 3, 2 })
        };
        try
        {
            var game = server.CreateGame(Guid.NewGuid(), scope =>
            {
                AutoRegistrar.RegisterAll(typeof(MoveCommand).Assembly);
                scope.Register("Demo.Move", _ => Ioc.Resolve<ICommand>("Commands.Move", data));
            });
            var endpoint = new Endpoint(server.Dispatcher, new Dictionary<string, Func<JsonElement, ICommand>>
            {
                ["Move"] = _ => Ioc.Resolve<ICommand>("Demo.Move")
            });
            using var args = JsonDocument.Parse("{}");
            Assert.Equal(DispatchResult.Accepted, endpoint.Accept(new GameMessage(game.Id, "Move", args.RootElement)));
            server.Stop();
            Assert.Equal(new Vector(new[] { 3, 2 }), data["Position"]);
        }
        finally { server.Stop(); }
    }

    private class TestAction(Action action) : ICommand
    {
        public void Execute() => action();
    }
}

using System.Text.Json;
using App;
using SpaceBattle.Lib;
using SpaceBattle.Server;
using GameEndpoint = SpaceBattle.Lib.Endpoint;

var options = ServerOptions.FromJson(File.ReadAllText(
    args.Length == 0 ? Path.Combine(AppContext.BaseDirectory, "server.json") : args[0]));
var server = new GameServer(options, diagnostic => Console.Error.WriteLine(diagnostic));
try
{
    var gameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    server.CreateGame(gameId, scope =>
    {
        AutoRegistrar.RegisterAll(typeof(MoveCommand).Assembly);
        var ship = new Dictionary<string, object>
        {
            ["Position"] = new Vector(new[] { 0, 0 }),
            ["Velocity"] = new Vector(new[] { 1, 0 })
        };
        scope.Register("Demo.Move", _ => Ioc.Resolve<ICommand>("Commands.Move", ship));
    });
    var endpoint = new GameEndpoint(server.Dispatcher, new Dictionary<string, Func<JsonElement, ICommand>>
    {
        ["Move"] = _ => Ioc.Resolve<ICommand>("Demo.Move")
    });
    var app = ServerApplication.Create(options, endpoint);
    try
    {
        server.Start();
        Console.WriteLine($"Game: {gameId}");
        await app.RunAsync();
    }
    finally
    {
        await app.DisposeAsync();
    }
}
finally
{
    server.Stop();
}

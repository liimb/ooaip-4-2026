using SpaceBattle.Lib;
using SpaceBattle.Server;

var options = ServerOptions.FromJson(File.ReadAllText(
    args.Length == 0 ? Path.Combine(AppContext.BaseDirectory, "server.json") : args[0]));
var gameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var shipId = Guid.Parse("22222222-2222-2222-2222-222222222222");
var initial = new WorldSnapshot(1, gameId, 0, false, new[] { new ObjectState(shipId, "Ship", 0, 0) });
var manager = new ServerManager(options, new[] { initial });
var shutdown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.TrySetResult(); };
try
{
    await manager.StartAsync();
    Console.WriteLine($"Game: {gameId}; ship: {shipId}");
    await shutdown.Task;
}
finally
{
    await manager.SoftStopAsync();
}

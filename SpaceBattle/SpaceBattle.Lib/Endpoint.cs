using System.Text.Json;
using App;

namespace SpaceBattle.Lib;

public record GameMessage(Guid GameId, string Command, JsonElement Arguments);

public class Endpoint
{
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, Func<JsonElement, ICommand>> _commands;

    public Endpoint(Dispatcher dispatcher, IReadOnlyDictionary<string, Func<JsonElement, ICommand>> commands)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(commands);
        _dispatcher = dispatcher;
        _commands = new Dictionary<string, Func<JsonElement, ICommand>>(commands, StringComparer.Ordinal);
        if (_commands.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null))
            throw new ArgumentException("Command names and factories are required.", nameof(commands));
    }

    public DispatchResult Accept(GameMessage message)
    {
        if (message == null || message.GameId == Guid.Empty || string.IsNullOrWhiteSpace(message.Command) ||
            message.Arguments.ValueKind != JsonValueKind.Object || !_commands.TryGetValue(message.Command, out var factory))
            return DispatchResult.InvalidMessage;
        return _dispatcher.Dispatch(message.GameId, new MessageCommand(factory, message.Arguments.Clone()));
    }

    private class MessageCommand(Func<JsonElement, ICommand> factory, JsonElement arguments) : ICommand
    {
        public void Execute() => factory(arguments).Execute();
    }
}

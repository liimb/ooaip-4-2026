namespace SpaceBattle.Lib;

public record GameDiagnostic(Guid GameId, string Kind, TimeSpan Duration, Exception? Error = null);

public enum GameState
{
    Created,
    Waiting,
    Running,
    Inactive,
    Completed
}

public enum DispatchResult
{
    Accepted,
    InvalidMessage,
    UnknownGame,
    Overloaded,
    Stopped
}

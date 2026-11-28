namespace SpaceBattle.Lib;

public class Matchmaker
{
    private readonly object _gate = new();
    private readonly GameServer _server;
    private readonly MatchmakingOptions _options;
    private readonly TimeProvider _clock;
    private readonly Dictionary<Guid, SearchTicket> _searches = new();
    private readonly Dictionary<Guid, MatchSession> _players = new();
    private readonly Dictionary<Guid, MatchSession> _matches = new();
    private readonly Dictionary<Guid, Guid> _tickets = new();
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private bool _stopped;
    private int _nextStep;
    private long _nextSearch;

    internal Matchmaker(GameServer server, MatchmakingOptions options, TimeProvider clock)
    {
        _server = server;
        _options = options;
        _clock = clock;
    }

    internal SearchTicket Join(Guid playerId, int rating, MatchKind kind)
    {
        if (playerId == Guid.Empty || rating < _options.MinRating || rating > _options.MaxRating || !Enum.IsDefined(kind))
            throw new ArgumentException("Invalid player, rating or match kind.");
        lock (_gate)
        {
            if (_stopped) throw new InvalidOperationException("Matchmaking is stopped.");
            if (kind == MatchKind.Duel && !_options.DuelEnabled || kind == MatchKind.Training && !_options.TrainingEnabled)
                throw new InvalidOperationException("Match kind is disabled.");
            ReleaseFinishedPlayers();
            if (_searches.ContainsKey(playerId) || _players.ContainsKey(playerId))
                throw new InvalidOperationException("Player is already searching or playing.");
            if (_searches.Count >= _options.QueueCapacity) throw new InvalidOperationException("Search queue is full.");
            var ticket = new SearchTicket(playerId, rating, kind, _clock.GetTimestamp(), _nextSearch++);
            _searches.Add(playerId, ticket);
            return ticket;
        }
    }

    internal bool Cancel(Guid playerId, Guid searchId)
    {
        lock (_gate)
        {
            if (!_searches.TryGetValue(playerId, out var ticket) || ticket.Id != searchId) return false;
            _searches.Remove(playerId);
            ticket.Cancel();
            return true;
        }
    }

    internal SearchStatus Status(Guid playerId)
    {
        lock (_gate)
        {
            ReleaseFinishedPlayers();
            if (_searches.TryGetValue(playerId, out var ticket))
                return new(SearchState.Searching, ticket.Id, Error: ticket.Error);
            return _players.TryGetValue(playerId, out var match)
                ? new(SearchState.InGame, Match: match.Notification) : new(SearchState.NotSearching);
        }
    }

    internal Task<MatchFound> WaitAsync(Guid playerId, Guid searchId, CancellationToken token)
    {
        lock (_gate)
        {
            if (_searches.TryGetValue(playerId, out var ticket) && ticket.Id == searchId) return ticket.WaitAsync(token);
            if (_players.TryGetValue(playerId, out var match) && _tickets.TryGetValue(searchId, out var owner) && owner == playerId)
                return Task.FromResult(match.Notification);
            throw new KeyNotFoundException("Search was cancelled or does not exist.");
        }
    }

    internal Game Restore(WorldSnapshot snapshot)
    {
        GameWorld.Validate(snapshot);
        var definition = snapshot.Match!;
        lock (_gate)
        {
            if (_stopped) throw new InvalidOperationException("Matchmaking is stopped.");
            var participants = new[] { definition.First, definition.Second };
            var humans = participants.Where(player => !player.IsBot).ToArray();
            ReleaseFinishedPlayers();
            if (participants.Any(player => player.Rating < _options.MinRating || player.Rating > _options.MaxRating) ||
                !snapshot.Finished && humans.Any(player => _searches.ContainsKey(player.Id) || _players.ContainsKey(player.Id)))
                throw new InvalidOperationException("Restored player is invalid or already searching or playing.");
            var match = _server.RestoreMatch(snapshot);
            _matches.Add(definition.Id, match);
            if (!snapshot.Finished)
                foreach (var player in humans) _players.Add(player.Id, match);
            return match.Game;
        }
    }

    internal MatchOutcome? Outcome(Guid matchId)
    {
        lock (_gate) return _matches.TryGetValue(matchId, out var match) ? match.Outcome : null;
    }

    public void Process()
    {
        lock (_gate)
        {
            if (_stopped) return;
            ReleaseFinishedPlayers();
            var now = _clock.GetTimestamp();
            var waiting = _searches.Values.OrderBy(ticket => ticket.Sequence).ToArray();
            var attempted = new HashSet<Guid>();
            foreach (var first in waiting)
            {
                if (!_searches.ContainsKey(first.PlayerId) || attempted.Contains(first.PlayerId)) continue;
                SearchTicket? second = null;
                if (first.Kind == MatchKind.Duel)
                    second = waiting.Where(candidate => candidate.PlayerId != first.PlayerId && candidate.Kind == first.Kind &&
                            _searches.ContainsKey(candidate.PlayerId) && !attempted.Contains(candidate.PlayerId) && Compatible(first, candidate, now))
                        .OrderBy(candidate => Math.Abs((long)candidate.Rating - first.Rating))
                        .ThenBy(candidate => candidate.Sequence).FirstOrDefault();
                var bot = first.Kind == MatchKind.Training || _options.AllowDuelBots &&
                    _clock.GetElapsedTime(first.Started, now) >= TimeSpan.FromMilliseconds(_options.BotWaitMilliseconds);
                if (second == null && !bot) continue;
                attempted.Add(first.PlayerId);
                if (second != null) attempted.Add(second.PlayerId);
                try
                {
                    var definition = new MatchDefinition(Guid.NewGuid(), first.Kind,
                        new MatchPlayer(first.PlayerId, first.Rating, Guid.NewGuid()),
                        new MatchPlayer(second?.PlayerId ?? Guid.NewGuid(), second?.Rating ?? first.Rating, Guid.NewGuid(), second == null));
                    var match = _server.CreateMatch(definition);
                    _matches.Add(definition.Id, match);
                    Admit(first, match);
                    if (second != null) Admit(second, match);
                }
                catch (Exception error)
                {
                    first.Error = error.Message;
                    if (second != null) second.Error = error.Message;
                }
            }
            var active = _matches.Values.Where(match => match.Outcome == null).ToArray();
            var start = active.Length == 0 ? 0 : _nextStep % active.Length;
            _nextStep = active.Length == 0 ? 0 : (start + 1) % active.Length;
            for (var offset = 0; offset < active.Length; offset++)
            {
                var match = active[(start + offset) % active.Length];
                if (!match.ReserveStep()) continue;
                if (_server.Dispatcher.Dispatch(match.Definition.Id, match.StepCommand()) != DispatchResult.Accepted)
                    match.ReleaseStep();
            }
        }
    }

    private bool Compatible(SearchTicket first, SearchTicket second, long now)
    {
        var distance = Math.Abs((long)first.Rating - second.Rating);
        return distance <= _options.Range(_clock.GetElapsedTime(first.Started, now)) &&
               distance <= _options.Range(_clock.GetElapsedTime(second.Started, now));
    }

    private void Admit(SearchTicket ticket, MatchSession match)
    {
        _searches.Remove(ticket.PlayerId);
        _players.Add(ticket.PlayerId, match);
        _tickets.Add(ticket.Id, ticket.PlayerId);
        ticket.Complete(match.Notification);
    }

    private void ReleaseFinishedPlayers()
    {
        foreach (var player in _players.Where(pair => pair.Value.Outcome != null).Select(pair => pair.Key).ToArray())
        {
            _players.Remove(player);
            foreach (var ticket in _tickets.Where(pair => pair.Value == player).Select(pair => pair.Key).ToArray()) _tickets.Remove(ticket);
        }
    }

    internal void Start()
    {
        lock (_gate)
        {
            if (!_stopped) _loop ??= RunAsync();
        }
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.PollIntervalMilliseconds), _clock);
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token)) Process();
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    internal void Stop()
    {
        lock (_gate)
        {
            _stopped = true;
            _stop.Cancel();
            foreach (var ticket in _searches.Values) ticket.Cancel();
            _searches.Clear();
        }
    }

    public async Task StopAsync()
    {
        Stop();
        if (_loop != null) await _loop;
    }
}

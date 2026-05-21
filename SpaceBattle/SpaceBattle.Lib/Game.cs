using App;

namespace SpaceBattle.Lib;

public class Game : IGameState
{
    private readonly Guid _gameId;
    private readonly ICommandReceiver _commandReceiver;
    private readonly List<ICommand> _commandQueue = new();
    private bool _isRunning = true;

    public Game(Guid gameId, ICommandReceiver commandReceiver)
    {
        _gameId = gameId;
        _commandReceiver = commandReceiver ?? throw new ArgumentNullException(nameof(commandReceiver));
    }

    public void AddCommand(ICommand command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        var sendCommand = new SendCommand(command, _commandReceiver);
        sendCommand.Execute();
    }

    public void Update()
    {
        foreach (var cmd in _commandQueue)
        {
            try
            {
                cmd.Execute();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command failed: {ex.Message}");
            }
        }
        _commandQueue.Clear();

        UpdateTorpedoesMovement();
        CheckCollisions();
    }

    private void UpdateTorpedoesMovement()
    {
        var torpedoesIds = Ioc.Resolve<IEnumerable<Guid>>("Game.GetTorpedoes", _gameId);

        foreach (var torpedoId in torpedoesIds)
        {
            var moveCommand = new TorpedoMovementCommand(torpedoId);
            moveCommand.Execute();
        }
    }

    private void CheckCollisions()
    {
        var collisions = Ioc.Resolve<IEnumerable<object>>("Game.CheckCollisions", _gameId);
        foreach (var collision in collisions)
        {
            var handleCollision = Ioc.Resolve<ICommand>("Game.HandleCollision", collision);
            handleCollision.Execute();
        }
    }

    public void Shoot(IShootable shooter)
    {
        if (shooter == null) throw new ArgumentNullException(nameof(shooter));

        var shooterName = shooter.ToString() ?? "Unknown";
        var authCommand = new CheckAuthorizationCommand("Player", shooterName, "Shoot");
        authCommand.Execute();

        var shootCommand = new ShootCommand(shooter);
        shootCommand.Execute();
    }

    public object GetState()
    {
        return Ioc.Resolve<object>("Game.State.Get", _gameId);
    }

    public void Stop() => _isRunning = false;
    public bool IsRunning => _isRunning;
}

using System.Text.Json;
using App;

namespace SpaceBattle.Lib;

public record ObjectState(Guid Id, string Kind, int X, int Y, int Direction = 0, int Speed = 1,
    int Fuel = 100, int Rockets = 5, bool Engine = false, int Rotation = 0, int Lifetime = 100,
    Guid? Owner = null);
public record WorldSnapshot(int Version, Guid GameId, long Tick, bool Finished, ObjectState[] Objects);
public record WorldOrder(string Operation, Guid ObjectId = default, Guid TargetId = default,
    int Amount = 0, ObjectState? Object = null);

public class GameWorld
{
    private readonly Dictionary<Guid, Body> _objects;
    private readonly GameplayOptions _rules;
    private readonly Action _assertContext;
    private readonly IDictionary<(string, string), CollisionTree> _collisions;
    private long _tick;
    private bool _finished;
    public Guid Id { get; }

    public GameWorld(WorldSnapshot snapshot, GameplayOptions rules, Action assertContext)
    {
        Validate(snapshot);
        rules.Validate();
        Id = snapshot.GameId;
        _rules = rules;
        _assertContext = assertContext;
        _tick = snapshot.Tick;
        _finished = snapshot.Finished;
        _objects = snapshot.Objects.ToDictionary(item => item.Id, item => new Body(item));
        var offsets = from x in Enumerable.Range(-1, 3)
                      from y in Enumerable.Range(-1, 3)
                      select (x, y, 0, 0);
        _collisions = new Dictionary<(string, string), CollisionTree> { [("cell", "cell")] = new CollisionTree(offsets) };
    }

    public WorldSnapshot Snapshot()
    {
        _assertContext();
        return new WorldSnapshot(1, Id, _tick, _finished, _objects.Values.Select(item => item.Export()).ToArray());
    }

    public ICommand Command(WorldOrder order) => new WorldCommand(this, order);

    private void Apply(WorldOrder order)
    {
        _assertContext();
        if (_finished) throw new InvalidOperationException("Game is finished.");
        if (order.Operation == "Finish") { _finished = true; return; }
        if (order.Operation == "Tick") { Tick(); return; }
        if (order.Operation == "Spawn")
        {
            var item = order.Object ?? throw new ArgumentException("Object is required.");
            Validate(new WorldSnapshot(1, Id, 0, false, new[] { item }));
            if (item.Fuel > _rules.MaxFuel) throw new ArgumentException("Fuel exceeds capacity.");
            if (_objects.Count >= _rules.MaxObjects) throw new InvalidOperationException("Object limit reached.");
            if (!_objects.TryAdd(item.Id, new Body(item))) throw new InvalidOperationException("Duplicate object.");
            return;
        }
        var body = _objects[order.ObjectId];
        if (body.Data.Kind is not ("Ship" or "Tanker")) throw new InvalidOperationException("A ship is required.");
        switch (order.Operation)
        {
            case "EngineOn": RequireFuel(body, _rules.MoveFuel); body.Engine = true; break;
            case "EngineOff": body.Engine = false; break;
            case "Clockwise": RequireFuel(body, _rules.RotateFuel); body.Rotation = 1; break;
            case "Counterclockwise": RequireFuel(body, _rules.RotateFuel); body.Rotation = -1; break;
            case "StopRotation": body.Rotation = 0; break;
            case "Refuel":
                if (order.Amount <= 0 || order.Amount > _rules.MaxFuel - body.Fuel) throw new ArgumentOutOfRangeException(nameof(order.Amount));
                body.Fuel += order.Amount;
                break;
            case "TransferFuel":
                var target = _objects[order.TargetId];
                if (body.Data.Kind != "Tanker" || ReferenceEquals(body, target) || target.Data.Kind is not ("Ship" or "Tanker") ||
                    order.Amount <= 0 || order.Amount > _rules.MaxFuel - target.Fuel || Distance(body, target) > _rules.RefuelDistance)
                    throw new InvalidOperationException("Invalid fuel transfer.");
                RequireFuel(body, order.Amount);
                body.Fuel -= order.Amount;
                target.Fuel += order.Amount;
                break;
            case "Shoot":
                new ShootCommand(body).Execute();
                break;
            default: throw new ArgumentException("Unknown gameplay operation.");
        }
    }

    internal ICommand Shooting(IShootable shooter) => new OperationCommand(() =>
    {
        _assertContext();
        if (_finished || shooter is not Body body || !_objects.TryGetValue(body.Data.Id, out var current) || !ReferenceEquals(body, current))
            throw new InvalidOperationException("Shooter does not belong to the active game.");
        RequireFuel(body, _rules.ShotFuel);
        if (body.Rockets == 0 || _objects.Count >= _rules.MaxObjects) throw new InvalidOperationException("Cannot launch rocket.");
        var rocket = new ObjectState(Guid.NewGuid(), "Rocket", body.Position.GetCoordinates()[0], body.Position.GetCoordinates()[1],
            body.Angle.Steps, _rules.RocketSpeed, 0, 0, true, 0, _rules.RocketLifetime, body.Data.Id);
        _objects.Add(rocket.Id, new Body(rocket));
        body.Fuel -= _rules.ShotFuel;
        body.Rockets--;
    });

    internal IMovingObject MovingObject(Guid id)
    {
        _assertContext();
        return _objects[id];
    }

    private class OperationCommand(Action action) : ICommand
    {
        public void Execute() => action();
    }
    private void Tick()
    {
        _tick++;
        var removed = new HashSet<Guid>();
        foreach (var body in _objects.Values)
        {
            var ship = body.Data.Kind is "Ship" or "Tanker";
            var cost = ship ? (body.Engine ? (long)_rules.MoveFuel : 0) + (body.Rotation != 0 ? _rules.RotateFuel : 0) : 0;
            if (body.Fuel < cost) { body.Engine = false; body.Rotation = 0; continue; }
            if (body.Rotation != 0) new RotateCommand(body).Execute();
            if (body.Engine)
            {
                if (body.Data.Kind == "Rocket") new TorpedoMovementCommand(body.Data.Id).Execute();
                else new MoveCommand(body).Execute();
            }
            body.Fuel -= (int)cost;
            if (ship && body.Fuel == 0) { body.Engine = false; body.Rotation = 0; }
            if (body.Data.Kind == "Rocket" && --body.Lifetime <= 0) removed.Add(body.Data.Id);
        }
        var objects = _objects.Values.ToArray();
        for (var i = 0; i < objects.Length; i++)
            for (var j = i + 1; j < objects.Length; j++)
            {
                var a = objects[i]; var b = objects[j];
                if (removed.Contains(a.Data.Id) || removed.Contains(b.Data.Id) || a.Data.Owner == b.Data.Id || b.Data.Owner == a.Data.Id) continue;
                var check = new CheckCollisionCommand(a, b, _collisions);
                check.Execute();
                if (!check.HasCollision) continue;
                if (a.Data.Kind == "Bonus" && b.Data.Kind is "Ship" or "Tanker") { b.Fuel = (int)Math.Min(_rules.MaxFuel, (long)b.Fuel + _rules.BonusFuel); removed.Add(a.Data.Id); }
                else if (b.Data.Kind == "Bonus" && a.Data.Kind is "Ship" or "Tanker") { a.Fuel = (int)Math.Min(_rules.MaxFuel, (long)a.Fuel + _rules.BonusFuel); removed.Add(b.Data.Id); }
                else if (a.Data.Kind != "Bonus" && b.Data.Kind != "Bonus") { removed.Add(a.Data.Id); removed.Add(b.Data.Id); }
            }
        foreach (var id in removed) _objects.Remove(id);
        if (!_objects.Values.Any(item => item.Data.Kind is "Ship" or "Tanker")) _finished = true;
    }

    private static double Distance(Body a, Body b)
    {
        var p = a.Position.GetCoordinates(); var q = b.Position.GetCoordinates();
        return Math.Sqrt(Math.Pow((double)p[0] - q[0], 2) + Math.Pow((double)p[1] - q[1], 2));
    }
    private static void RequireFuel(Body body, int amount)
    {
        if (body.Fuel < amount) throw new InvalidOperationException("Insufficient fuel.");
    }
    public static void Validate(WorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Version != 1 || snapshot.GameId == Guid.Empty || snapshot.Tick < 0 || snapshot.Objects == null)
            throw new ArgumentException("Invalid snapshot or unsupported version.");
        if (snapshot.Objects.Any(item => item == null || item.Id == Guid.Empty || item.Kind is not ("Ship" or "Tanker" or "Rocket" or "Meteor" or "Bonus") ||
            item.Fuel < 0 || item.Rockets < 0 || item.Speed < 0 || item.Speed > 1000 || item.Lifetime <= 0 || item.Rotation < -1 || item.Rotation > 1 || item.Direction < 0 || item.Direction > 7) ||
            snapshot.Objects.Select(item => item.Id).Distinct().Count() != snapshot.Objects.Length)
            throw new ArgumentException("Invalid game objects in snapshot.");
    }

    private class WorldCommand(GameWorld world, WorldOrder order) : ICommand
    {
        public void Execute() => world.Apply(order);
    }
    private class Body : IMovingObject, IRotatingObject, ICollisionObject, IShootable
    {
        public ObjectState Data { get; }
        public Vector Position { get; set; }
        public Angle Angle { get; set; }
        public Angle AngularVelocity => new(Rotation);
        public int Rotation { get; set; }
        public bool Engine { get; set; }
        public int Fuel { get; set; }
        public int Rockets { get; set; }
        public int Lifetime { get; set; }
        public Vector Velocity => new(new[] { (int)Math.Round(Math.Cos(Angle.ToRadians()) * Data.Speed), (int)Math.Round(Math.Sin(Angle.ToRadians()) * Data.Speed) });
        public string Form => "cell";
        IVector ICollisionObject.Position => Position;
        IVector ICollisionObject.Velocity => new Vector(new[] { 0, 0 });
        public Body(ObjectState data)
        {
            Data = data; Position = new Vector(new[] { data.X, data.Y }); Angle = new Angle(data.Direction);
            Rotation = data.Rotation; Engine = data.Engine; Fuel = data.Fuel; Rockets = data.Rockets; Lifetime = data.Lifetime;
        }
        public ObjectState Export() => Data with
        {
            X = Position.GetCoordinates()[0],
            Y = Position.GetCoordinates()[1],
            Direction = Angle.Steps,
            Rotation = Rotation,
            Engine = Engine,
            Fuel = Fuel,
            Rockets = Rockets,
            Lifetime = Lifetime
        };
    }
}

public class GameplayOptions
{
    public int MoveFuel { get; init; } = 1;
    public int RotateFuel { get; init; } = 1;
    public int ShotFuel { get; init; } = 2;
    public int MaxFuel { get; init; } = 1000;
    public int BonusFuel { get; init; } = 20;
    public int RefuelDistance { get; init; } = 5;
    public int RocketSpeed { get; init; } = 3;
    public int RocketLifetime { get; init; } = 100;
    public int MaxObjects { get; init; } = 1000;
    public void Validate()
    {
        if (MoveFuel <= 0 || RotateFuel <= 0 || ShotFuel <= 0 || MaxFuel <= 0 || BonusFuel <= 0 || RefuelDistance <= 0 ||
            RocketSpeed <= 0 || RocketSpeed > 1000 || RocketLifetime <= 0 || MaxObjects <= 0)
            throw new ArgumentException("Gameplay settings must be positive and speed must not exceed 1000.");
    }
}



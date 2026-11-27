using App;
using App.Scopes;

namespace SpaceBattle.Lib;

public class GameScope
{
    private readonly object _gate = new();
    private readonly object _scope;
    private readonly List<Action> _cleanup = new();
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private int _owner;
    private int _depth;
    private bool _closed;
    public GameScope()
    {
        new InitCommand().Execute();
        _scope = Ioc.Resolve<object>("IoC.Scope.Create");
    }

    public Activation Activate()
    {
        lock (_gate)
        {
            if (_closed)
                throw new InvalidOperationException("The scope is closed.");
            var thread = Environment.CurrentManagedThreadId;
            if (_depth != 0 && _owner != thread)
                throw new InvalidOperationException("The scope is already active on another thread.");
            var previous = Ioc.Resolve<object>("IoC.Scope.Current");
            var restore = Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previous);
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _scope).Execute();
            _owner = thread;
            return new Activation(this, restore, ++_depth);
        }
    }

    public void Register(string name, Func<object[], object> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);
        var activation = Activate();
        try
        {
            if (!_names.Add(name))
                throw new InvalidOperationException($"Dependency '{name}' is already registered in this scope.");
            try
            {
                Ioc.Resolve<ICommand>("IoC.Register", name, factory).Execute();
            }
            catch
            {
                _names.Remove(name);
                throw;
            }
        }
        finally
        {
            activation.Restore();
        }
    }

    public void RegisterOwned<T>(string name, Func<object[], T> factory, Action<T> cleanup)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(cleanup);
        Register(name, args =>
        {
            lock (_gate)
            {
                if (_closed)
                    throw new InvalidOperationException("The scope is closed.");
                var value = factory(args);
                _cleanup.Add(() => cleanup(value));
                return value;
            }
        });
    }

    public void Close()
    {
        Action[] actions;
        lock (_gate)
        {
            if (_closed)
                return;
            if (_depth != 0)
                throw new InvalidOperationException("Cannot close an active scope.");
            _closed = true;
            actions = _cleanup.AsEnumerable().Reverse().ToArray();
            _cleanup.Clear();
        }

        var errors = new List<Exception>();
        foreach (var action in actions)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        if (errors.Count != 0)
            throw new AggregateException(errors);
    }

    public class Activation
    {
        private readonly GameScope owner;
        private readonly ICommand restore;
        private readonly int depth;
        private bool _restored;
        internal Activation(GameScope owner, ICommand restore, int depth)
        {
            this.owner = owner;
            this.restore = restore;
            this.depth = depth;
        }

        public void Restore()
        {
            lock (owner._gate)
            {
                if (_restored)
                    return;
                if (owner._owner != Environment.CurrentManagedThreadId || owner._depth != depth || !ReferenceEquals(Ioc.Resolve<object>("IoC.Scope.Current"), owner._scope))
                    throw new InvalidOperationException("Scope activations must be restored on their owning thread in reverse order.");
                restore.Execute();
                owner._depth--;
                _restored = true;
            }
        }
    }
}

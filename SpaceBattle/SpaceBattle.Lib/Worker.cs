using System.Diagnostics;
using System.Threading.Channels;
using App;
using App.Scopes;

namespace SpaceBattle.Lib;

public class Worker : ICommandReceiver
{
    private readonly Channel<ICommand> _inbox;
    private readonly Thread _thread;
    private readonly object _lifecycle = new();
    private readonly Action<ICommand, Exception> _onError;
    private bool _started;
    public Worker(int id, int capacity, Action<ICommand, Exception>? onError = null)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        Id = id;
        _onError = onError ?? ((_, error) => Trace.TraceError(error.ToString()));
        _inbox = Channel.CreateBounded<ICommand>(new BoundedChannelOptions(capacity) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = $"SpaceBattle.Worker.{id}"
        };
    }

    public int Id { get; }
    public int PendingCount => _inbox.Reader.Count;

    public bool TrySend(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _inbox.Writer.TryWrite(command);
    }

    public void Receive(ICommand command)
    {
        if (!TrySend(command))
            throw new InvalidOperationException("Worker inbox is full or completed.");
    }

    public void Start()
    {
        lock (_lifecycle)
        {
            if (_started)
                return;
            _thread.Start();
            _started = true;
        }
    }

    public void Complete() => _inbox.Writer.TryComplete();
    public bool Join(TimeSpan timeout)
    {
        if (Thread.CurrentThread == _thread)
            throw new InvalidOperationException("A worker cannot join itself.");
        lock (_lifecycle)
        {
            if (!_started)
                throw new InvalidOperationException("Start the worker before joining it.");
        }

        return _thread.Join(timeout);
    }

    public void Stop()
    {
        if (Thread.CurrentThread == _thread)
            throw new InvalidOperationException("Stop the worker from its controlling thread.");
        Complete();
        Start();
        _thread.Join();
    }

    internal bool IsCurrentThread => Thread.CurrentThread == _thread;

    private void Run()
    {
        new InitCommand().Execute();
        var initialScope = Ioc.Resolve<object>("IoC.Scope.Current");
        var restore = Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", initialScope);
        while (_inbox.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (_inbox.Reader.TryRead(out var command))
            {
                try
                {
                    command.Execute();
                }
                catch (Exception error)
                {
                    try
                    {
                        _onError(command, error);
                    }
                    catch (Exception handlerError)
                    {
                        Trace.TraceError(handlerError.ToString());
                    }
                }
                finally
                {
                    restore.Execute();
                }
            }
        }
    }
}

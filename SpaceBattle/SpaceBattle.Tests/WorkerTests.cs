using System.Collections.Concurrent;
using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class WorkerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    [Fact]
    public async Task FullInboxRejectsImmediatelyAndDrainsInOrder()
    {
        var results = new List<int>();
        var worker = new Worker(0, 2);
        try
        {
            Assert.True(worker.TrySend(new TestAction(() => results.Add(1))));
            Assert.True(worker.TrySend(new TestAction(() => results.Add(2))));
            var rejected = Task.Run(() => worker.TrySend(new TestAction(() => results.Add(3))));
            Assert.False(await rejected.WaitAsync(Timeout));
            Assert.Equal(2, worker.PendingCount);
            Assert.Empty(results);
            worker.Complete();
            Assert.False(worker.TrySend(new TestAction(() => results.Add(4))));
            worker.Start();
            Assert.True(worker.Join(Timeout));
            Assert.Equal(new[] { 1, 2 }, results);
        }
        finally
        {
            worker.Stop();
        }
    }

    [Fact]
    public void ConcurrentProducersExecuteAcceptedMessagesExactlyOnce()
    {
        var accepted = new ConcurrentBag<int>();
        var executed = new ConcurrentBag<int>();
        var worker = new Worker(0, 32);
        try
        {
            worker.Start();
            Parallel.For(0, 4000, id =>
            {
                if (worker.TrySend(new TestAction(() => executed.Add(id))))
                    accepted.Add(id);
            });
            worker.Complete();
            Assert.True(worker.Join(Timeout));
            Assert.NotEmpty(accepted);
            Assert.Equal(accepted.Order(), executed.Order());
            Assert.Equal(executed.Count, executed.Distinct().Count());
        }
        finally
        {
            worker.Stop();
        }
    }

    [Fact]
    public void PoolUsesFixedThreadsAndRunsWorkersInParallel()
    {
        using var barrier = new Barrier(2);
        var threads = new ConcurrentDictionary<int, byte>();
        var errors = new ConcurrentBag<Exception>();
        var pool = new WorkerPool(new WorkerOptions { WorkerCount = 2, QueueCapacity = 101 }, (_, error) => errors.Add(error));
        try
        {
            var count = 0;
            for (var worker = 0; worker < 2; worker++)
            {
                Assert.True(pool.TrySend(worker, new TestAction(() => Assert.True(barrier.SignalAndWait(Timeout)))));
                for (var i = 0; i < 100; i++)
                    Assert.True(pool.TrySend(worker, new TestAction(() =>
                    {
                        threads.TryAdd(Environment.CurrentManagedThreadId, 0);
                        Interlocked.Increment(ref count);
                    })));
            }

            pool.Start();
            pool.Complete();
            foreach (var worker in pool.Workers)
                Assert.True(worker.Join(Timeout));
            Assert.Empty(errors);
            Assert.Equal(200, count);
            Assert.Equal(2, threads.Count);
            Assert.DoesNotContain(Environment.CurrentManagedThreadId, threads.Keys);
        }
        finally
        {
            pool.Stop();
        }
    }

    [Fact]
    public void CommandAndErrorHandlerFailuresDoNotStopWorker()
    {
        var handled = 0;
        var executed = 0;
        var worker = new Worker(0, 2, (_, _) =>
        {
            Interlocked.Increment(ref handled);
            throw new InvalidOperationException("handler failure");
        });
        try
        {
            worker.TrySend(new TestAction(() => throw new InvalidOperationException("command failure")));
            worker.TrySend(new TestAction(() => executed++));
            worker.Complete();
            worker.Start();
            Assert.True(worker.Join(Timeout));
            Assert.Equal(1, handled);
            Assert.Equal(1, executed);
        }
        finally
        {
            worker.Stop();
        }
    }

    [Fact]
    public void SendCommandUsesWorkerAsAsynchronousReceiver()
    {
        var count = 0;
        var worker = new Worker(0, 1);
        try
        {
            var send = new SendCommand(new TestAction(() => count++), worker);
            send.Execute();
            Assert.Equal(0, count);
            Assert.Throws<InvalidOperationException>(send.Execute);
            worker.Stop();
            Assert.Equal(1, count);
        }
        finally
        {
            worker.Stop();
        }
    }

    [Fact]
    public void CompletionRacingWithProducersDoesNotLoseAcceptedWork()
    {
        var worker = new Worker(0, 64);
        try
        {
            var accepted = 0;
            var executed = 0;
            worker.Start();
            Parallel.For(0, 2000, id =>
            {
                if (id == 1000)
                    worker.Complete();
                if (worker.TrySend(new TestAction(() => Interlocked.Increment(ref executed))))
                    Interlocked.Increment(ref accepted);
            });
            worker.Complete();
            Assert.True(worker.Join(Timeout));
            Assert.Equal(accepted, executed);
        }
        finally
        {
            worker.Stop();
        }
    }

    [Fact]
    public void IdleWorkerWakesOnNewWorkAndCompletion()
    {
        using var ran = new ManualResetEventSlim();
        var worker = new Worker(0, 1);
        try
        {
            worker.Start();
            Assert.True(worker.TrySend(new TestAction(ran.Set)));
            Assert.True(ran.Wait(Timeout));
            worker.Complete();
            Assert.True(worker.Join(Timeout));
        }
        finally
        {
            worker.Stop();
        }
    }

    [Fact]
    public void WorkerCannotStopItselfButContinuesAfterRejection()
    {
        var worker = new Worker(0, 2);
        try
        {
            Exception? error = null;
            worker.TrySend(new TestAction(() => error = Record.Exception(worker.Stop)));
            worker.Complete();
            worker.Start();
            Assert.True(worker.Join(Timeout));
            Assert.IsType<InvalidOperationException>(error);
        }
        finally
        {
            worker.Stop();
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public void InvalidConfigurationIsRejected(int workers, int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerPool(new WorkerOptions { WorkerCount = workers, QueueCapacity = capacity }));
    }

    [Fact]
    public void JsonConfigurationIsValidated()
    {
        var options = WorkerOptions.FromJson("{\"WorkerCount\":3,\"QueueCapacity\":12}");
        Assert.Equal(3, options.WorkerCount);
        Assert.Equal(12, options.QueueCapacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkerOptions.FromJson("{\"QueueCapacity\":0}"));
        Assert.Throws<ArgumentException>(() => WorkerOptions.FromJson("null"));
    }

    private class TestAction(Action action) : ICommand
    {
        public void Execute() => action();
    }
}

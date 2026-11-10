using System.Collections.Concurrent;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameScopeTests
{
    [Fact]
    public void TransientFactoriesReceiveParametersAndCreateNewInstances()
    {
        var scope = new GameScope();
        try
        {
            scope.Register("Test.Parameterized", args => new Box((int)args[0]));
            var activation = scope.Activate();
            try
            {
                var first = Ioc.Resolve<Box>("Test.Parameterized", 42);
                var second = Ioc.Resolve<Box>("Test.Parameterized", 7);
                Assert.Equal(42, first.Value);
                Assert.Equal(7, second.Value);
                Assert.NotSame(first, second);
            }
            finally
            {
                activation.Restore();
            }
        }
        finally
        {
            scope.Close();
        }
    }

    [Fact]
    public void OwnedFactoriesCreateNewInstancesAndCloseEveryResultOnce()
    {
        var scope = new GameScope();
        var created = 0;
        scope.RegisterOwned("Test.Resource", _ =>
        {
            created++;
            return new Resource();
        }, resource => resource.Close());
        Resource first;
        Resource second;
        Assert.Equal(0, created);
        {
            var activation1 = scope.Activate();
            try
            {
                first = Ioc.Resolve<Resource>("Test.Resource");
                second = Ioc.Resolve<Resource>("Test.Resource");
                Assert.NotSame(first, second);
                Assert.Equal(0, first.Closures);
                Assert.Equal(0, second.Closures);
            }
            finally
            {
                activation1.Restore();
            }
        }

        Assert.Equal(2, created);
        scope.Close();
        scope.Close();
        Assert.Equal(1, first.Closures);
        Assert.Equal(1, second.Closures);
        Assert.Throws<InvalidOperationException>(() => scope.Activate());
    }

    [Fact]
    public void NestedActivationAndExceptionsRestorePreviousScope()
    {
        new InitCommand().Execute();
        var original = Ioc.Resolve<object>("IoC.Scope.Current");
        var first = new GameScope();
        try
        {
            var second = new GameScope();
            try
            {
                {
                    var activation2 = first.Activate();
                    try
                    {
                        var active = Ioc.Resolve<object>("IoC.Scope.Current");
                        Assert.NotSame(original, active);
                        var command = new ScopedCommand(second, new TestAction(() => throw new Exception("test")));
                        Assert.Throws<Exception>(command.Execute);
                        Assert.Same(active, Ioc.Resolve<object>("IoC.Scope.Current"));
                    }
                    finally
                    {
                        activation2.Restore();
                    }
                }

                Assert.Same(original, Ioc.Resolve<object>("IoC.Scope.Current"));
            }
            finally
            {
                second.Close();
            }
        }
        finally
        {
            first.Close();
        }
    }

    [Fact]
    public void OwnedFactoriesReceiveParametersOnEveryResolution()
    {
        var scope = new GameScope();
        try
        {
            scope.RegisterOwned("Test.Parameterized", args => new Box((int)args[0]), _ => { });
            var active = scope.Activate();
            try
            {
                var first = Ioc.Resolve<Box>("Test.Parameterized", 3);
                var second = Ioc.Resolve<Box>("Test.Parameterized", 5);
                Assert.Equal(3, first.Value);
                Assert.Equal(5, second.Value);
                Assert.NotSame(first, second);
            }
            finally
            {
                active.Restore();
            }
        }
        finally
        {
            scope.Close();
        }
    }

    [Fact]
    public void CallerOwnedResourcesAreNotClosedByScope()
    {
        var scope = new GameScope();
        scope.Register("Test.Resource", _ => new Resource());
        Resource resource;
        {
            var activation3 = scope.Activate();
            try
            {
                resource = Ioc.Resolve<Resource>("Test.Resource");
            }
            finally
            {
                activation3.Restore();
            }
        }

        scope.Close();
        Assert.Equal(0, resource.Closures);
        resource.Close();
        Assert.Equal(1, resource.Closures);
    }

    [Fact]
    public void DifferentScopesAreIsolatedOnParallelWorkers()
    {
        var first = new GameScope();
        try
        {
            var second = new GameScope();
            try
            {
                first.Register("Test.Identity", _ => new Box(1));
                second.Register("Test.Identity", _ => new Box(2));
                using var barrier = new Barrier(2);
                var errors = new ConcurrentBag<Exception>();
                var pool = new WorkerPool(new WorkerOptions(), (_, error) => errors.Add(error));
                try
                {
                    foreach (var pair in new[]
                    {
                        (0, first, 1),
                        (1, second, 2)
                    }

                    )
                    {
                        pool.TrySend(pair.Item1, new ScopedCommand(pair.Item2, new TestAction(() =>
                        {
                            var before = Ioc.Resolve<Box>("Test.Identity");
                            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
                            Assert.Equal(pair.Item3, before.Value);
                            var after = Ioc.Resolve<Box>("Test.Identity");
                            Assert.Equal(pair.Item3, after.Value);
                            Assert.NotSame(before, after);
                        })));
                    }

                    pool.Start();
                    pool.Complete();
                    foreach (var worker in pool.Workers)
                        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
                    Assert.Empty(errors);
                }
                finally
                {
                    pool.Stop();
                }
            }
            finally
            {
                second.Close();
            }
        }
        finally
        {
            first.Close();
        }
    }

    [Fact]
    public void ConcurrentActivationOfSameScopeIsRejected()
    {
        var scope = new GameScope();
        try
        {
            var active = scope.Activate();
            try
            {
                Exception? error = null;
                var thread = new Thread(() => error = Record.Exception(() =>
                {
                    var other = scope.Activate();
                    try
                    {
                    }
                    finally
                    {
                        other.Restore();
                    }
                }));
                thread.Start();
                Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
                Assert.IsType<InvalidOperationException>(error);
                Assert.Throws<InvalidOperationException>(scope.Close);
            }
            finally
            {
                active.Restore();
            }
        }
        finally
        {
            scope.Close();
        }
    }

    [Fact]
    public void ScopeCanBeReactivatedOnAnotherThreadAfterRelease()
    {
        var scope = new GameScope();
        try
        {
            scope.Register("Test.Identity", _ => new Box(1));
            Box? first;
            {
                var activation4 = scope.Activate();
                try
                {
                    first = Ioc.Resolve<Box>("Test.Identity");
                }
                finally
                {
                    activation4.Restore();
                }
            }

            Box? second = null;
            Exception? error = null;
            var thread = new Thread(() => error = Record.Exception(() =>
            {
                var active = scope.Activate();
                try
                {
                    second = Ioc.Resolve<Box>("Test.Identity");
                }
                finally
                {
                    active.Restore();
                }
            }));
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
            Assert.Null(error);
            Assert.Equal(first, second);
            Assert.NotSame(first, second);
        }
        finally
        {
            scope.Close();
        }
    }

    [Fact]
    public void CleanupContinuesAfterResourceFailure()
    {
        var scope = new GameScope();
        Resource resource;
        scope.RegisterOwned("Test.Resource", _ => new Resource(), item => item.Close());
        scope.RegisterOwned("Test.FailingResource", _ => new FailingResource(), item => item.Close());
        {
            var activation5 = scope.Activate();
            try
            {
                resource = Ioc.Resolve<Resource>("Test.Resource");
                Ioc.Resolve<object>("Test.FailingResource");
            }
            finally
            {
                activation5.Restore();
            }
        }

        Assert.Throws<AggregateException>(scope.Close);
        Assert.Equal(1, resource.Closures);
    }

    [Fact]
    public void RegistrationsAndTheirResultsAreIndependent()
    {
        var scope = new GameScope();
        try
        {
            scope.Register("Test.First", _ => new Box(1));
            scope.Register("Test.Second", _ => new Box(1));
            Assert.Throws<InvalidOperationException>(() => scope.Register("Test.First", _ => new Box(2)));
            var active = scope.Activate();
            try
            {
                Assert.NotSame(Ioc.Resolve<Box>("Test.First"), Ioc.Resolve<Box>("Test.Second"));
            }
            finally
            {
                active.Restore();
            }
        }
        finally
        {
            scope.Close();
        }
    }

    [Fact]
    public void CloseRunsCleanupInReverseOrderAndOnlyOnce()
    {
        var scope = new GameScope();
        var cleaned = new List<int>();
        scope.RegisterOwned("Test.Order", args => new Box((int)args[0]), box => cleaned.Add(box.Value));
        try
        {
            var activation = scope.Activate();
            try
            {
                Ioc.Resolve<Box>("Test.Order", 1);
                Ioc.Resolve<Box>("Test.Order", 2);
                Ioc.Resolve<Box>("Test.Order", 3);
            }
            finally
            {
                activation.Restore();
            }
        }
        finally
        {
            scope.Close();
        }
        scope.Close();
        Assert.Equal(new[] { 3, 2, 1 }, cleaned);
    }

    [Fact]
    public void RestoreIsIdempotentAndPreservesPreviousContext()
    {
        var scope = new GameScope();
        var previous = Ioc.Resolve<object>("IoC.Scope.Current");
        try
        {
            var activation = scope.Activate();
            try
            {
                Assert.NotSame(previous, Ioc.Resolve<object>("IoC.Scope.Current"));
            }
            finally
            {
                activation.Restore();
            }
            activation.Restore();
            Assert.Same(previous, Ioc.Resolve<object>("IoC.Scope.Current"));
        }
        finally
        {
            scope.Close();
        }
    }

    private record Box(int Value);
    [Fact]
    public void ActivationRejectsOutOfOrderRestorationAndCanRecover()
    {
        var first = new GameScope();
        try
        {
            var second = new GameScope();
            try
            {
                var outer = first.Activate();
                var inner = second.Activate();
                Assert.Throws<InvalidOperationException>(outer.Restore);
                inner.Restore();
                outer.Restore();
            }
            finally
            {
                second.Close();
            }
        }
        finally
        {
            first.Close();
        }
    }

    [Fact]
    public void WorkerRestoresScopeAfterFailingScopedCommand()
    {
        var scope = new GameScope();
        try
        {
            var errors = new ConcurrentBag<Exception>();
            var worker = new Worker(0, 3, (_, error) => errors.Add(error));
            try
            {
                object? before = null;
                object? after = null;
                worker.TrySend(new TestAction(() => before = Ioc.Resolve<object>("IoC.Scope.Current")));
                worker.TrySend(new ScopedCommand(scope, new TestAction(() => throw new InvalidOperationException("expected"))));
                worker.TrySend(new TestAction(() => after = Ioc.Resolve<object>("IoC.Scope.Current")));
                worker.Complete();
                worker.Start();
                Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
                Assert.Single(errors);
                Assert.NotNull(before);
                Assert.Same(before, after);
                var reactivated = scope.Activate();
                try
                {
                }
                finally
                {
                    reactivated.Restore();
                }
            }
            finally
            {
                worker.Stop();
            }
        }
        finally
        {
            scope.Close();
        }
    }

    private class Resource
    {
        public int Closures { get; private set; }

        public void Close() => Closures++;
    }

    private class FailingResource
    {
        public void Close() => throw new InvalidOperationException("cleanup");
    }

    private class TestAction(Action action) : ICommand
    {
        public void Execute() => action();
    }
}

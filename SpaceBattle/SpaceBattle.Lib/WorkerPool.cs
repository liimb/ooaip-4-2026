using App;

namespace SpaceBattle.Lib;

public class WorkerPool
{
    public IReadOnlyList<Worker> Workers { get; }

    public WorkerPool(WorkerOptions options, Action<ICommand, Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        Workers = Array.AsReadOnly(Enumerable.Range(0, options.WorkerCount).Select(id => new Worker(id, options.QueueCapacity, onError, options.IncomingBatchSize)).ToArray());
    }

    public void Start()
    {
        foreach (var worker in Workers)
            worker.Start();
    }

    public bool TrySend(int workerId, ICommand command) => Workers[workerId].TrySend(command);
    public void Complete()
    {
        foreach (var worker in Workers)
            worker.Complete();
    }

    public void Stop()
    {
        if (Workers.Any(worker => worker.IsCurrentThread))
            throw new InvalidOperationException("Stop the pool from its controlling thread.");
        Complete();
        Start();
        foreach (var worker in Workers)
            worker.Stop();
    }
}

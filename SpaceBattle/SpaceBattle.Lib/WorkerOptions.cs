using System.Text.Json;

namespace SpaceBattle.Lib;

public class WorkerOptions
{
    public int WorkerCount { get; init; } = 2;
    public int QueueCapacity { get; init; } = 256;
    public int IncomingBatchSize { get; init; } = 32;

    public void Validate()
    {
        if (WorkerCount <= 0) throw new ArgumentOutOfRangeException(nameof(WorkerCount));
        if (QueueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(QueueCapacity));
        if (IncomingBatchSize <= 0) throw new ArgumentOutOfRangeException(nameof(IncomingBatchSize));
    }

    public static WorkerOptions FromJson(string json)
    {
        var options = JsonSerializer.Deserialize<WorkerOptions>(json)
            ?? throw new ArgumentException("Worker configuration must be an object.", nameof(json));
        options.Validate();
        return options;
    }
}

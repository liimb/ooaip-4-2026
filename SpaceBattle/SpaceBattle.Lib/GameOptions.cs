namespace SpaceBattle.Lib;

public class GameOptions
{
    public int QueueCapacity { get; init; } = 256;
    public int QuantumMilliseconds { get; init; } = 10;
    public int MaxCommandsPerQuantum { get; init; } = 64;
    public int SlowCommandMilliseconds { get; init; } = 100;

    public void Validate()
    {
        if (QueueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(QueueCapacity));
        if (QuantumMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(QuantumMilliseconds));
        if (MaxCommandsPerQuantum <= 0) throw new ArgumentOutOfRangeException(nameof(MaxCommandsPerQuantum));
        if (SlowCommandMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(SlowCommandMilliseconds));
    }
}

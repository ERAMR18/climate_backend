namespace Climate.Contracts.Realtime;

public static class RealtimeEventNames
{
    public const string SensorReadingUpdated = nameof(SensorReadingUpdated);
    public const string AlertGenerated = nameof(AlertGenerated);
    public const string SensorStatusChanged = nameof(SensorStatusChanged);
    public const string SystemReset = nameof(SystemReset);
    public static readonly IReadOnlyCollection<string> All = [SensorReadingUpdated,AlertGenerated,SensorStatusChanged,SystemReset];
}

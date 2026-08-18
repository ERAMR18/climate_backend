using Climate.Monitoring.Application.Abstractions;

namespace Climate.Monitoring.Infrastructure.Simulation;

internal sealed class SimulationControl(bool initiallyRunning) : ISimulationControl
{
    private int _isRunning = initiallyRunning ? 1 : 0;
    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;
    public void Start() => Interlocked.Exchange(ref _isRunning, 1);
    public void StopSimulation() => Interlocked.Exchange(ref _isRunning, 0);
}

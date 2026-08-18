namespace Climate.Monitoring.Application.Abstractions;

public interface ISimulationControl
{
    bool IsRunning { get; }
    void Start();
    void StopSimulation();
}

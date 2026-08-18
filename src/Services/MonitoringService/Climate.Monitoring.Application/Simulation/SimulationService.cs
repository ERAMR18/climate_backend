using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Application.Readings;

namespace Climate.Monitoring.Application.Simulation;

public sealed class SimulationService(
    ISimulationControl control,
    IMonitoringRepository repository) : ISimulationService
{
    public SimulationStatusResponse GetStatus() => new(control.IsRunning);

    public SimulationStatusResponse Start()
    {
        control.Start();
        return GetStatus();
    }

    public SimulationStatusResponse StopSimulation()
    {
        control.StopSimulation();
        return GetStatus();
    }

    public async Task<SimulationStatusResponse> ResetAsync(CancellationToken cancellationToken)
    {
        control.StopSimulation();
        await repository.DeleteAllAsync(cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return GetStatus();
    }
}

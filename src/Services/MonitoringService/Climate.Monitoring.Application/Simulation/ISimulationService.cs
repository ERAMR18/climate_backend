using Climate.Monitoring.Application.Readings;

namespace Climate.Monitoring.Application.Simulation;

public interface ISimulationService
{
    SimulationStatusResponse GetStatus();
    SimulationStatusResponse Start();
    SimulationStatusResponse StopSimulation();
    Task<SimulationStatusResponse> ResetAsync(CancellationToken cancellationToken);
}

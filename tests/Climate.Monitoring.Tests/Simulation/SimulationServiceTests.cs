using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Application.Simulation;
using Moq;

namespace Climate.Monitoring.Tests.Simulation;

public sealed class SimulationServiceTests
{
    [Fact]
    public void StartChangesSimulationState()
    {
        var control = new Mock<ISimulationControl>();
        control.SetupGet(value => value.IsRunning).Returns(true);
        var service = new SimulationService(control.Object, Mock.Of<IMonitoringRepository>());

        var status = service.Start();

        control.Verify(value => value.Start(), Times.Once);
        Assert.True(status.IsRunning);
    }

    [Fact]
    public void StopChangesSimulationState()
    {
        var control = new Mock<ISimulationControl>();
        control.SetupGet(value => value.IsRunning).Returns(false);
        var service = new SimulationService(control.Object, Mock.Of<IMonitoringRepository>());

        var status = service.StopSimulation();

        control.Verify(value => value.StopSimulation(), Times.Once);
        Assert.False(status.IsRunning);
    }

    [Fact]
    public async Task ResetStopsSimulationAndDeletesOnlyReadings()
    {
        var control = new Mock<ISimulationControl>();
        var repository = new Mock<IMonitoringRepository>();
        var service = new SimulationService(control.Object, repository.Object);

        var status = await service.ResetAsync(CancellationToken.None);

        control.Verify(value => value.StopSimulation(), Times.Once);
        repository.Verify(value => value.DeleteAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(status.IsRunning);
    }
}

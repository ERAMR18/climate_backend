namespace Climate.Contracts.Audit;

public static class AuditActions
{
    public const string Login = nameof(Login);
    public const string CreateSensor = nameof(CreateSensor);
    public const string UpdateSensor = nameof(UpdateSensor);
    public const string ActivateSensor = nameof(ActivateSensor);
    public const string DeactivateSensor = nameof(DeactivateSensor);
    public const string StartSimulation = nameof(StartSimulation);
    public const string StopSimulation = nameof(StopSimulation);
    public const string ResetSystem = nameof(ResetSystem);
    public const string UpdateUser = nameof(UpdateUser);
}

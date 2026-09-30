namespace Climate.Contracts.Audit;

public static class AuditActions
{
    public static string ForOperation(string controller, string operation, string method) =>
        (controller.Replace("-", "", StringComparison.Ordinal).ToUpperInvariant(), operation.ToUpperInvariant()) switch
        {
            ("ALERTRULES", "CREATE") => CreateAlertRule,
            ("ALERTRULES", "UPDATE") => UpdateAlertRule,
            ("ALERTRULES", "ACTIVATE") => ActivateAlertRule,
            ("ALERTRULES", "DEACTIVATE") => DeactivateAlertRule,
            ("ALERTS", "ATTEND") => AttendAlert,
            ("ALERTS", "CLOSE") => CloseAlert,
            _ => method == "POST" ? Create : method == "DELETE" ? Delete : Update
        };

    public const string Logout = nameof(Logout);
    public const string Create = nameof(Create);
    public const string Update = nameof(Update);
    public const string Delete = nameof(Delete);
    public const string Activate = nameof(Activate);
    public const string Deactivate = nameof(Deactivate);
    public const string CreateAlertRule = nameof(CreateAlertRule);
    public const string UpdateAlertRule = nameof(UpdateAlertRule);
    public const string ActivateAlertRule = nameof(ActivateAlertRule);
    public const string DeactivateAlertRule = nameof(DeactivateAlertRule);
    public const string AttendAlert = nameof(AttendAlert);
    public const string CloseAlert = nameof(CloseAlert);
    public const string ResetSimulation = nameof(ResetSimulation);
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

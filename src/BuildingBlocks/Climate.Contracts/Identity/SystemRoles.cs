namespace Climate.Contracts.Identity;

public static class SystemRoles
{
    public const string Administrator = nameof(Administrator);
    public const string Operator = nameof(Operator);
    public const string Viewer = nameof(Viewer);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Administrator, Operator, Viewer],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsDefined(string? role) => role is not null && All.Contains(role);
}

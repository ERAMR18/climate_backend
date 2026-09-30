using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Climate.Contracts.Audit;

public sealed class AuditTransactionFilter<TContext>(TContext db, AuditWriter writer) : IAsyncActionFilter where TContext : DbContext
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string method = context.HttpContext.Request.Method;
        if (method is not ("POST" or "PUT" or "PATCH" or "DELETE")) { await next(); return; }
        var token = context.HttpContext.RequestAborted;
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var executed = await next();
        int status = (executed.Result as IStatusCodeActionResult)?.StatusCode ?? 200;
        if (executed.Exception is not null || status >= 400) return;

        if (!db.ChangeTracker.Entries<AuditOutboxMessage>().Any())
        {
            string entity = context.RouteData.Values["controller"]?.ToString() ?? "Operation";
            string action = AuditActions.ForOperation(entity, context.RouteData.Values["action"]?.ToString() ?? "", method);
            string? id = (context.RouteData.Values["id"] ?? context.RouteData.Values["sensorId"])?.ToString();
            if (id is null && executed.Result is ObjectResult result)
                id = result.Value?.GetType().GetProperty("Id")?.GetValue(result.Value)?.ToString();
            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
                await writer.RecordAsync(context.HttpContext.User, action, entity, id, $"{action} {entity}.", context.HttpContext.Connection.RemoteIpAddress?.ToString(), token);
            else await writer.RecordSystemAsync(action, entity, id, $"{action} {entity}.", token);
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }
}

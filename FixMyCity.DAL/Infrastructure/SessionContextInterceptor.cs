using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace FixMyCity.DAL.Infrastructure;

/// <summary>
/// EF Core interceptor that injects SESSION_CONTEXT on every connection open.
/// Required for ComplaintRLS row-level security policy to pass.
/// Set CurrentUserId / CurrentUserRole / CurrentDeptId before any DB call.
/// </summary>
public static class DbSessionContext
{
    [ThreadStatic] public static int? CurrentUserId;
    [ThreadStatic] public static string? CurrentUserRole;  // "SuperAdmin","Solver","Citizen","PWG"
    [ThreadStatic] public static int? CurrentDeptId;
}

public class SessionContextInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection,
                                          ConnectionEndEventData eventData)
    {
        SetSessionContext(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection,
                                                      ConnectionEndEventData eventData,
                                                      CancellationToken cancellationToken = default)
    {
        SetSessionContext(connection);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void SetSessionContext(DbConnection connection)
    {
        // SuperAdmin bypasses RLS — used for admin endpoints and Swagger testing
        string role = DbSessionContext.CurrentUserRole ?? "SuperAdmin";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            EXEC sp_set_session_context N'UserRole', @role, @read_only = 0;
            EXEC sp_set_session_context N'UserId',   @uid,  @read_only = 0;
            EXEC sp_set_session_context N'DeptId',   @dept, @read_only = 0;
            """;

        var pRole = cmd.CreateParameter();
        pRole.ParameterName = "@role";
        pRole.Value = role;
        cmd.Parameters.Add(pRole);

        var pUid = cmd.CreateParameter();
        pUid.ParameterName = "@uid";
        pUid.Value = (object?)DbSessionContext.CurrentUserId ?? DBNull.Value;
        cmd.Parameters.Add(pUid);

        var pDept = cmd.CreateParameter();
        pDept.ParameterName = "@dept";
        pDept.Value = (object?)DbSessionContext.CurrentDeptId ?? DBNull.Value;
        cmd.Parameters.Add(pDept);

        cmd.ExecuteNonQuery();
    }
}
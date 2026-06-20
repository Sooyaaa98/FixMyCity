// FixMyCity.API/Middleware/JwtSessionContextMiddleware.cs
// Reads JWT claims from HttpContext.User and writes them to DbSessionContext
// (the [ThreadStatic] fields used by SessionContextInterceptor for RLS).
// Must run AFTER app.UseAuthentication() so User.Claims is populated.

using FixMyCity.DAL.Infrastructure;
using System.Security.Claims;

namespace FixMyCity.API.Middleware
{
    public class JwtSessionContextMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? context.User.FindFirst("sub")?.Value;

                DbSessionContext.CurrentUserId   = int.TryParse(sub, out var uid) ? uid : null;
                DbSessionContext.CurrentUserRole  = context.User.FindFirst(ClaimTypes.Role)?.Value;

                var deptClaim = context.User.FindFirst("deptId")?.Value;
                DbSessionContext.CurrentDeptId    = int.TryParse(deptClaim, out var dept) ? dept : null;
            }
            else
            {
                // Unauthenticated requests — RLS defaults to SuperAdmin bypass in interceptor
                // (safe: Swagger and health endpoints need no RLS; all real endpoints are [Authorize])
                DbSessionContext.CurrentUserId   = null;
                DbSessionContext.CurrentUserRole  = "SuperAdmin";
                DbSessionContext.CurrentDeptId    = null;
            }

            await next(context);
        }
    }
}

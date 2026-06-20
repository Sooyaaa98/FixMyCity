// FixMyCity.API/Middleware/SecurityHeadersMiddleware.cs
// Adds security headers on every response:
//   - CSP (prevents XSS)
//   - HSTS (HTTPS only)
//   - X-Frame-Options (clickjacking)
//   - X-Content-Type-Options (MIME sniffing)
//   - Referrer-Policy
//   - Permissions-Policy
//   - Cache-Control on API responses

namespace FixMyCity.API.Middleware
{
    public class SecurityHeadersMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            // Content Security Policy — allow only same-origin and trusted CDNs
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline'; " +    // Angular needs inline scripts
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: blob:; " +
                "connect-src 'self' http://localhost:8001; " + // AI service
                "frame-ancestors 'none';";

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"]         = "DENY";
            headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"]      = "geolocation=(), microphone=(), camera=()";

            // HSTS — only in production (30 days, include subdomains)
            if (!context.Request.IsHttps is false)
                headers["Strict-Transport-Security"] = "max-age=2592000; includeSubDomains";

            // No-cache on all API responses (prevents stale auth data)
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                headers["Pragma"]        = "no-cache";
            }

            // Remove server identity headers
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");

            await next(context);
        }
    }
}

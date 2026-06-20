// FixMyCity.API/Program.cs
// Complete replacement.
//
// New NuGet packages required (add to FixMyCity.API.csproj):
//   <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.*" />
//   <PackageReference Include="QuestPDF"                                       Version="2024.3.*" />
//   <PackageReference Include="Microsoft.Extensions.Http.Polly"                Version="8.*" />
//
// New NuGet packages required (add to FixMyCity.DAL.csproj):
//   (no new packages needed in DAL)

using FixMyCity.API.Middleware;
using FixMyCity.API.Services;
using FixMyCity.DAL.Infrastructure;
using FixMyCity.DAL.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using QuestPDF.Infrastructure;
using System.Text;
using System.Threading.RateLimiting;

// ── QuestPDF community license ────────────────────────────────────────────────
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ── 1. DbContext + RLS interceptor ────────────────────────────────────────────
// QueryTrackingBehavior.NoTracking is the default for read-heavy API workloads:
//  - It disables EF Core "navigation fixup", which would otherwise share a single
//    Department/User/Category instance across every Complaint in a result set
//    and thus populate Department.Complaints back-reference lists. Those lists
//    create deep object graphs that defeat ReferenceHandler.IgnoreCycles by
//    blowing past JsonSerializerOptions.MaxDepth — every list endpoint then
//    returns 500 with "A possible object cycle was detected".
//  - Tracking is opt-in via .AsTracking() on the handful of queries that
//    mutate-then-SaveChanges (currently only GamificationRepository.MarkOneRead).
builder.Services.AddDbContext<FixMyCityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
           .AddInterceptors(new SessionContextInterceptor()));

// ── 2. JWT Authentication ─────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret  = jwtSection["Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required in appsettings.json");

builder.Services.AddAuthentication(opts =>
{
    opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opts.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opts =>
{
    opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer           = true,
        ValidIssuer              = jwtSection["Issuer"],
        ValidateAudience         = true,
        ValidAudience            = jwtSection["Audience"],
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero, // no tolerance — 15 min means 15 min
    };

    opts.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            if (ctx.Exception is SecurityTokenExpiredException)
                ctx.Response.Headers.Append("Token-Expired", "true");
            return Task.CompletedTask;
        },
        OnChallenge = ctx =>
        {
            ctx.HandleResponse();
            ctx.Response.StatusCode  = 401;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync(
                "{\"success\":false,\"message\":\"Authentication required. Please log in.\"}");
        },
        OnForbidden = ctx =>
        {
            ctx.Response.StatusCode  = 403;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync(
                "{\"success\":false,\"message\":\"You do not have permission to access this resource.\"}");
        },
    };
});

builder.Services.AddAuthorization();

// ── 3. JWT Service ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IJwtService, JwtService>();

// ── 4. QuestPDF Service ───────────────────────────────────────────────────────
builder.Services.AddSingleton<IQuestPdfService, QuestPdfService>();

// ── 5. Polly retry policy (kept; reused by Razorpay client below) ────────────
// Originally introduced for MLServiceClient (Phase-8 removed). Razorpay still
// benefits from the same exponential-backoff policy for transient 5xx.
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            3,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // 2s, 4s, 8s
            onRetry: (outcome, delay, attempt, _) =>
                Console.WriteLine(
                    $"[Http] Retry {attempt} after {delay.TotalSeconds}s — {outcome.Exception?.Message}"));

// ── 5b. Razorpay client (Phase 5) ─────────────────────────────────────────────
// Typed HttpClient + service. The base address is Razorpay's API endpoint; auth
// header is computed once inside the service constructor from the configured
// Razorpay:KeyId / Razorpay:KeySecret. Polly retries shield us from transient
// 5xx on the Orders API.
builder.Services.AddHttpClient<IRazorpayService, RazorpayService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddPolicyHandler(GetRetryPolicy());

// ── 5c. AI migration services (Phase 1.2 — scaffold only) ────────────────────
// AiService wraps Gemini + OpenAI moderation directly. Scoped because it
// holds FixMyCityDbContext; background callers MUST resolve via
// IServiceScopeFactory.CreateScope() — see risk_analysis.md R6.
// CloudinaryService is singleton (thread-safe SDK client, no per-request state).
// Both are registered now so Phase-2 and Phase-3 can wire them into controllers
// without further Program.cs changes. IHttpClientFactory is required by
// AiService; the typed HttpClient registrations above already pull it in,
// but AddHttpClient() is the canonical way to make it explicit.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<CloudinaryService>();
builder.Services.AddScoped<AiService>();

// ── 6. Background services ────────────────────────────────────────────────────
builder.Services.AddHostedService<AIPendingQueueProcessor>();
builder.Services.AddHostedService<AutoEscalationService>();
builder.Services.AddHostedService<WeeklyDigestService>();   // US65 — weekly digest

// ── 7. Rate limiting ──────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = 429;

    // Login endpoint: 10 attempts per minute per IP
    opts.AddFixedWindowLimiter("login", o =>
    {
        o.PermitLimit       = 10;
        o.Window            = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit        = 0;
    });

    // Global API limiter: 300 requests/minute per IP (prevents scraping)
    opts.AddFixedWindowLimiter("global", o =>
    {
        o.PermitLimit       = 300;
        o.Window            = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit        = 5;
    });

    opts.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode  = 429;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"success\":false,\"message\":\"Too many requests. Please try again shortly.\"}", ct);
    };
});

// ── 8. Controllers + JSON + model validation ──────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        // Defense in depth: even with NoTracking + IgnoreCycles, a deep
        // entity graph (Complaint → Department → Locality → … → Complaint chain
        // across many records) can approach the 64-frame default. 128 leaves
        // ample headroom without risking real runaway recursion.
        opts.JsonSerializerOptions.MaxDepth = 128;
    })
    .ConfigureApiBehaviorOptions(opts =>
    {
        opts.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToArray();
            return new JsonResult(new { success = false, errors }) { StatusCode = 400 };
        };
    });

// ── 9. Swagger with JWT support ───────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "FixMyCity API — Sprint 2 + AI + JWT",
        Version     = "v1",
        Description = "Civic complaint management platform.",
    });

    // Add JWT bearer input to Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT access token (without 'Bearer ' prefix).",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── 10. CORS ──────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("AllowAll", policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
        if (origins is { Length: > 0 })
            policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
        else
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

// ── Security headers (first — applied to every response) ─────────────────────
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
    {
        ctx.Response.StatusCode  = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(
            "{\"success\":false,\"message\":\"An unexpected error occurred.\"}");
    }));
    app.UseHsts();
}

app.UseCors("AllowAll");

// In Development the API may bind both http://localhost:5065 and
// https://localhost:7030 (see Properties/launchSettings.json "https" profile).
// UseHttpsRedirection would 307-redirect requests to http:5065 → https:7030,
// which is a cross-origin scheme change. Browsers strip the
// Authorization: Bearer <jwt> header on that follow-up request, so the
// HTTPS endpoint sees the request as anonymous and returns 401. The Angular
// AuthInterceptor then treats the 401 as a revoked session and bounces the
// user back to /login — looking exactly like a broken login.
// Keep HTTPS redirection for production (behind a reverse proxy or HSTS).
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// ── Rate limiting ─────────────────────────────────────────────────────────────
app.UseRateLimiter();

// ── Authentication + Authorization ───────────────────────────────────────────
app.UseAuthentication();

// ── Populate DbSessionContext from JWT claims (must be after UseAuthentication) ──
app.UseMiddleware<JwtSessionContextMiddleware>();

app.UseAuthorization();

// Phase 8: AIServiceKeyMiddleware retired — all write-back callback endpoints
// were deleted with the Python service. /api/ML/* is now standard JWT-only.

app.MapControllers();
app.Run();

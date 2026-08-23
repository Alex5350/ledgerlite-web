using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LedgerLite.Api.Extensions;
using LedgerLite.Api.Features.Accounts;
using LedgerLite.Api.Features.Auth;
using LedgerLite.Api.Features.Budgets;
using LedgerLite.Api.Features.FiscalPeriods;
using LedgerLite.Api.Features.JournalEntries;
using LedgerLite.Api.Features.Reports;
using LedgerLite.Application;
using LedgerLite.Infrastructure;
using LedgerLite.Infrastructure.Authentication;
using LedgerLite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ----- Serilog (console, structured) -----
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

// ----- Layers -----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ----- JSON -----
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ----- ProblemDetails for unhandled exceptions -----
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
    };
});

// ----- CORS -----
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000", "http://localhost:5173"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// ----- Rate limiting -----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = static async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Type = "https://tools.ietf.org/html/rfc6585#section-4"
            },
            cancellationToken);
    };

    // Strict limiter for login attempts: 5 per minute per IP.
    options.AddPolicy("auth-login", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Generous global limiter for everything else: 200 per minute per IP.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// ----- Authentication / authorization -----
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false; // keep raw "sub"/"email" claims
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwt.Value.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
    });

builder.Services.AddAuthorization();

// ----- OpenAPI (document + bearer scheme, UI in Development) -----
builder.Services.AddBearerDocumentation();

// ----- Health checks -----
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LedgerLiteDbContext>("sqlite-database");

var app = builder.Build();

// ----- Middleware (order matters) -----
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ----- OpenAPI document in every environment; Scalar UI in Development -----
app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(); // /scalar/v1

    // Apply migrations and seed demo data in Development only.
    await app.Services.InitializeAsync();
}

app.MapHealthChecks("/health").ExcludeFromDescription();

// ----- Endpoint groups -----
app.MapGroup("/api/auth")
    .WithTags("Auth")
    .MapAuthEndpoints();

app.MapGroup("/api/periods")
    .WithTags("Fiscal Periods")
    .RequireAuthorization()
    .MapFiscalPeriodEndpoints();

app.MapGroup("/api/accounts")
    .WithTags("Accounts")
    .RequireAuthorization()
    .MapAccountEndpoints();

app.MapGroup("/api/journal-entries")
    .WithTags("Journal Entries")
    .RequireAuthorization()
    .MapJournalEntryEndpoints();

app.MapGroup("/api/reports")
    .WithTags("Reports")
    .RequireAuthorization()
    .MapReportEndpoints();

app.MapGroup("/api/budgets")
    .WithTags("Budgets")
    .RequireAuthorization()
    .MapBudgetEndpoints();

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;

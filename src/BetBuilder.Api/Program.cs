using System.Text.Json;
using System.Text.Json.Serialization;
using BetBuilder.Api.GrpcServices;
using BetBuilder.Api.Health;
using BetBuilder.Api.Hubs;
using BetBuilder.Api.Middleware;
using BetBuilder.Application.Bingo;
using BetBuilder.Application.Interfaces;
using BetBuilder.Application.Pricing;
using BetBuilder.Application.Resulting;
using BetBuilder.Application.Validation;
using BetBuilder.Infrastructure.Csv;
using BetBuilder.Infrastructure.Data;
using BetBuilder.Infrastructure.Hosting;
using BetBuilder.Infrastructure.Rules;
using BetBuilder.Infrastructure.Snapshots;
using BetBuilder.Infrastructure.Simulation;
using BetBuilder.Infrastructure.State;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DataSettings>(builder.Configuration.GetSection(DataSettings.SectionName));
builder.Services.Configure<PricingSettings>(builder.Configuration.GetSection(PricingSettings.SectionName));

// PostgreSQL connection — three sources, in priority order:
//   1. DATABASE_URL          → Railway-style postgres://user:pass@host:port/db
//   2. DB_HOST (+ DB_*)      → AWS ECS pattern (host/port/db/user as env vars,
//                              password injected from Secrets Manager)
//   3. ConnectionStrings:Default → local dev fallback
//
// DB_SCHEMA is appended as Search Path on every variant so unqualified table
// names in raw SQL (and EF's HasDefaultSchema in BetBuilderDbContext) all
// resolve to the same Postgres schema. Defaults to "public" when unset.
var schema = Environment.GetEnvironmentVariable("DB_SCHEMA")?.Trim();
var searchPathSuffix = !string.IsNullOrEmpty(schema) && !string.Equals(schema, "public", StringComparison.OrdinalIgnoreCase)
    ? $";Search Path={schema}"
    : string.Empty;

string buildConnectionString()
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')}" +
               $";Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true" +
               searchPathSuffix;
    }

    var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
    if (!string.IsNullOrEmpty(dbHost))
    {
        var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
        var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? string.Empty;
        return $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass}" +
               ";SSL Mode=Require;Trust Server Certificate=true" +
               searchPathSuffix;
    }

    return builder.Configuration.GetConnectionString("Default")
           ?? "Host=localhost;Port=5432;Database=betbuilder;Username=betbuilder;Password=betbuilder" + searchPathSuffix;
}

builder.Services.AddDbContext<BetBuilderDbContext>(opts => opts.UseNpgsql(buildConnectionString()));

builder.Services.AddSingleton<IActiveSnapshotStore, ActiveSnapshotStore>();
builder.Services.AddSingleton<ISnapshotSource, LocalSnapshotSource>();
builder.Services.AddSingleton<IPricingSnapshotFactory, PricingSnapshotFactory>();
builder.Services.AddSingleton<ISelectionRuleFactory, SelectionRuleFactory>();

builder.Services.AddSingleton<IComboValidator, ComboValidator>();
builder.Services.AddSingleton<IJointProbabilityCalculator, JointProbabilityCalculator>();
builder.Services.AddSingleton<IMarginService, MarginService>();
builder.Services.AddSingleton<IComboPricingService, ComboPricingService>();

builder.Services.AddSingleton<IFightSimulationService, FightSimulationService>();
builder.Services.AddSingleton<StatsFeedService>();
builder.Services.AddSingleton<IStatsFeedService>(sp => sp.GetRequiredService<StatsFeedService>());
builder.Services.AddSingleton<IStatsFeedAccessor>(sp => sp.GetRequiredService<StatsFeedService>());
builder.Services.AddSingleton<IFightBroadcaster, FightHubBroadcaster>();
builder.Services.AddSingleton<ILegOutcomeResolver, DefaultLegOutcomeResolver>();
builder.Services.AddSingleton<IBingoCardGenerator, BingoCardGenerator>();
builder.Services.AddSingleton<IBingoCardCache, BingoCardCache>();

builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IFightResultingService, FightResultingService>();

builder.Services.AddHostedService<DatabaseMigrationService>();
builder.Services.AddHostedService<SnapshotLoaderService>();

builder.Services.AddGrpc(opts =>
{
    opts.MaxReceiveMessageSize = 10 * 1024 * 1024; // 10 MB for large matrices
});

builder.Services.AddSignalR(opts =>
{
    opts.EnableDetailedErrors = true;
    opts.MaximumReceiveMessageSize = 1 * 1024 * 1024; // 1 MB
});

builder.Services.AddControllers(opts =>
    {
        // Outcome matrix uploads can be 200KB+ as CSV text in JSON body
        opts.MaxModelBindingCollectionSize = int.MaxValue;
    })
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.WebHost.ConfigureKestrel(opts =>
{
    opts.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    opts.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http1AndHttp2);
});

var corsOrigins = builder.Configuration.GetSection("CorsOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
    {
        if (corsOrigins.Length > 0)
        {
            var wildcardPatterns = corsOrigins.Where(o => o.Contains('*')).ToArray();
            var exactOrigins = corsOrigins.Where(o => !o.Contains('*')).ToArray();

            if (wildcardPatterns.Length > 0)
            {
                policy.SetIsOriginAllowed(origin =>
                {
                    if (exactOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                        return true;

                    foreach (var pattern in wildcardPatterns)
                    {
                        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                            .Replace("\\*", ".*") + "$";
                        if (System.Text.RegularExpressions.Regex.IsMatch(
                            origin, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            return true;
                    }
                    return false;
                });
            }
            else
            {
                policy.WithOrigins(exactOrigins);
            }

            policy.AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin();
        }

        policy.AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddHealthChecks()
    .AddCheck<SnapshotHealthCheck>("snapshot_loaded");

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandler>();
app.UseCors();

var defaultFileOptions = new DefaultFilesOptions();
defaultFileOptions.DefaultFileNames.Clear();
defaultFileOptions.DefaultFileNames.Add("workbench.html");
app.UseDefaultFiles(defaultFileOptions);
app.UseStaticFiles();
app.MapControllers();
app.MapGrpcService<SnapshotIngestService>();
app.MapHub<FightHub>(FightHub.Path);
app.MapHealthChecks("/health");

app.Run();

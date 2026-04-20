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

// PostgreSQL -- Railway provides DATABASE_URL, or fall back to connection string in config
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var connStr = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')}" +
                  $";Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
    builder.Services.AddDbContext<BetBuilderDbContext>(opts => opts.UseNpgsql(connStr));
}
else
{
    var connStr = builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Port=5432;Database=betbuilder;Username=betbuilder;Password=betbuilder";
    builder.Services.AddDbContext<BetBuilderDbContext>(opts => opts.UseNpgsql(connStr));
}

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

builder.Services.AddCors(opts =>
{
    // Reflect any origin so SignalR (which requires AllowCredentials for sticky sessions)
    // still works when embedded in a cross-origin iframe. Same-origin iframe embeds are
    // unaffected. Tighten this to a configured allow-list before going to production.
    opts.AddDefaultPolicy(policy => policy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
});

builder.Services.AddHealthChecks()
    .AddCheck<SnapshotHealthCheck>("snapshot_loaded");

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandler>();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapGrpcService<SnapshotIngestService>();
app.MapHub<FightHub>(FightHub.Path);
app.MapHealthChecks("/health");

app.Run();

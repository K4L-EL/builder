using System.Text.Json;
using System.Text.Json.Serialization;
using BetBuilder.Api.GrpcServices;
using BetBuilder.Api.Health;
using BetBuilder.Api.Middleware;
using BetBuilder.Application.Interfaces;
using BetBuilder.Application.Pricing;
using BetBuilder.Application.Validation;
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

builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ITicketService, TicketService>();

builder.Services.AddHostedService<DatabaseMigrationService>();
builder.Services.AddHostedService<SnapshotLoaderService>();

builder.Services.AddGrpc(opts =>
{
    opts.MaxReceiveMessageSize = 10 * 1024 * 1024; // 10 MB for large matrices
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
    opts.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
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
app.MapHealthChecks("/health");

app.Run();

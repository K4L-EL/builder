using System.Text.Json;
using System.Text.Json.Serialization;
using BetBuilder.Api.Health;
using BetBuilder.Api.Middleware;
using BetBuilder.Application.Interfaces;
using BetBuilder.Application.Pricing;
using BetBuilder.Application.Validation;
using BetBuilder.Infrastructure.Hosting;
using BetBuilder.Infrastructure.Rules;
using BetBuilder.Infrastructure.Snapshots;
using BetBuilder.Infrastructure.State;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DataSettings>(builder.Configuration.GetSection(DataSettings.SectionName));
builder.Services.Configure<PricingSettings>(builder.Configuration.GetSection(PricingSettings.SectionName));

builder.Services.AddSingleton<IActiveSnapshotStore, ActiveSnapshotStore>();
builder.Services.AddSingleton<ISnapshotSource, LocalSnapshotSource>();
builder.Services.AddSingleton<IPricingSnapshotFactory, PricingSnapshotFactory>();
builder.Services.AddSingleton<ISelectionRuleFactory, SelectionRuleFactory>();

builder.Services.AddSingleton<IComboValidator, ComboValidator>();
builder.Services.AddSingleton<IJointProbabilityCalculator, JointProbabilityCalculator>();
builder.Services.AddSingleton<IMarginService, MarginService>();
builder.Services.AddSingleton<IComboPricingService, ComboPricingService>();

builder.Services.AddHostedService<SnapshotLoaderService>();

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
});

builder.Services.AddHealthChecks()
    .AddCheck<SnapshotHealthCheck>("snapshot_loaded");

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandler>();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

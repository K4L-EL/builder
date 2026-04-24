using BetBuilder.Infrastructure.Simulation;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/v1/simulation")]
public class SimulationController : ControllerBase
{
    private readonly IFightSimulationService _simulation;

    public SimulationController(IFightSimulationService simulation)
    {
        _simulation = simulation;
    }

    [HttpPost("start")]
    public IActionResult Start([FromBody] SimulationStartRequest? request)
    {
        var speed = request?.Speed ?? 1.0;
        if (speed <= 0 || speed > 100) speed = 1.0;

        try
        {
            var status = _simulation.Start(speed);
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        var status = _simulation.Stop();
        return Ok(status);
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = _simulation.GetStatus();
        return Ok(status);
    }

    [HttpGet("debug")]
    public IActionResult Debug([FromServices] IConfiguration config)
    {
        var configuredDir = config["Simulation:MockDataDirectory"];
        var fallbackDir = Path.Combine(AppContext.BaseDirectory, "mock-data");
        var resolvedDir = configuredDir ?? fallbackDir;
        var exists = Directory.Exists(resolvedDir);
        var files = exists ? Directory.GetFiles(resolvedDir, "*.csv").Length : 0;
        var envVar = Environment.GetEnvironmentVariable("Simulation__MockDataDirectory");

        return Ok(new
        {
            configValue = configuredDir,
            envVar,
            fallbackDir,
            resolvedDir,
            directoryExists = exists,
            csvFileCount = files,
            baseDirectory = AppContext.BaseDirectory,
            workingDirectory = Directory.GetCurrentDirectory()
        });
    }
}

public sealed class SimulationStartRequest
{
    public double Speed { get; set; } = 1.0;
}

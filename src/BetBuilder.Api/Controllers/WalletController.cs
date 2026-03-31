using System.ComponentModel.DataAnnotations;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/v1/wallet")]
public sealed class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet("{userId}/balance")]
    public async Task<IActionResult> GetBalance(string userId)
    {
        var wallet = await _walletService.GetOrCreateWallet(userId);
        return Ok(new
        {
            userId = wallet.UserId,
            balance = wallet.Balance,
            held = wallet.Held,
            available = wallet.Available
        });
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] WalletTransactionRequest request)
    {
        try
        {
            var wallet = await _walletService.Deposit(request.UserId, request.Amount);
            return Ok(new
            {
                userId = wallet.UserId,
                balance = wallet.Balance,
                held = wallet.Held,
                available = wallet.Available
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = ex.Message });
        }
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WalletTransactionRequest request)
    {
        try
        {
            var wallet = await _walletService.Withdraw(request.UserId, request.Amount);
            return Ok(new
            {
                userId = wallet.UserId,
                balance = wallet.Balance,
                held = wallet.Held,
                available = wallet.Available
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Insufficient funds", Detail = ex.Message });
        }
    }
}

public sealed class WalletTransactionRequest
{
    [Required] public string UserId { get; set; } = default!;
    [Required] public decimal Amount { get; set; }
}

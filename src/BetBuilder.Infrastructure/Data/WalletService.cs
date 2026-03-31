using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;
using Microsoft.EntityFrameworkCore;

namespace BetBuilder.Infrastructure.Data;

public sealed class WalletService : IWalletService
{
    private readonly BetBuilderDbContext _db;

    public WalletService(BetBuilderDbContext db)
    {
        _db = db;
    }

    public async Task<Wallet> GetOrCreateWallet(string userId)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet != null) return wallet;

        wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Balance = 0m,
            Held = 0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Wallets.Add(wallet);
        await _db.SaveChangesAsync();
        return wallet;
    }

    public async Task<Wallet> Deposit(string userId, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive.");

        var wallet = await GetOrCreateWallet(userId);
        wallet.Balance += amount;
        wallet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return wallet;
    }

    public async Task<Wallet> Withdraw(string userId, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive.");

        var wallet = await GetOrCreateWallet(userId);
        if (wallet.Available < amount)
            throw new InvalidOperationException($"Insufficient available balance. Available: {wallet.Available:F2}, requested: {amount:F2}");

        wallet.Balance -= amount;
        wallet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return wallet;
    }

    public async Task<Wallet> HoldStake(string userId, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive.");

        var wallet = await GetOrCreateWallet(userId);
        if (wallet.Available < amount)
            throw new InvalidOperationException($"Insufficient available balance. Available: {wallet.Available:F2}, requested: {amount:F2}");

        wallet.Held += amount;
        wallet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return wallet;
    }

    public async Task<Wallet> ReleaseHold(string userId, decimal amount)
    {
        var wallet = await GetOrCreateWallet(userId);
        wallet.Held = Math.Max(0, wallet.Held - amount);
        wallet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return wallet;
    }

    public async Task<Wallet> SettleWin(string userId, decimal heldStake, decimal payout)
    {
        var wallet = await GetOrCreateWallet(userId);
        wallet.Held = Math.Max(0, wallet.Held - heldStake);
        wallet.Balance = wallet.Balance - heldStake + payout;
        wallet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return wallet;
    }

    public async Task<Wallet> SettleLoss(string userId, decimal heldStake)
    {
        var wallet = await GetOrCreateWallet(userId);
        wallet.Held = Math.Max(0, wallet.Held - heldStake);
        wallet.Balance -= heldStake;
        wallet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return wallet;
    }
}

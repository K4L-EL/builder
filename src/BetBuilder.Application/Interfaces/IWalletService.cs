using BetBuilder.Domain;

namespace BetBuilder.Application.Interfaces;

public interface IWalletService
{
    Task<Wallet> GetOrCreateWallet(string userId);
    Task<Wallet> Deposit(string userId, decimal amount);
    Task<Wallet> Withdraw(string userId, decimal amount);
    Task<Wallet> HoldStake(string userId, decimal amount);
    Task<Wallet> ReleaseHold(string userId, decimal amount);
    Task<Wallet> SettleWin(string userId, decimal heldStake, decimal payout);
    Task<Wallet> SettleLoss(string userId, decimal heldStake);
}

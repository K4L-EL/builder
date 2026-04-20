using BetBuilder.Domain;
using Microsoft.EntityFrameworkCore;

namespace BetBuilder.Infrastructure.Data;

public sealed class BetBuilderDbContext : DbContext
{
    public BetBuilderDbContext(DbContextOptions<BetBuilderDbContext> options) : base(options) { }

    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Wallet>(e =>
        {
            e.ToTable("wallets");
            e.HasKey(w => w.Id);
            e.Property(w => w.Id).HasColumnName("id");
            e.Property(w => w.UserId).HasColumnName("user_id").HasMaxLength(256);
            e.Property(w => w.Balance).HasColumnName("balance").HasPrecision(18, 2);
            e.Property(w => w.Held).HasColumnName("held").HasPrecision(18, 2);
            e.Property(w => w.CreatedAt).HasColumnName("created_at");
            e.Property(w => w.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(w => w.UserId).IsUnique();
            e.Ignore(w => w.Available);
        });

        modelBuilder.Entity<Ticket>(e =>
        {
            e.ToTable("tickets");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasColumnName("id");
            e.Property(t => t.UserId).HasColumnName("user_id").HasMaxLength(256);
            e.Property(t => t.SnapshotId).HasColumnName("snapshot_id").HasMaxLength(128);
            e.Property(t => t.EventId).HasColumnName("event_id").HasMaxLength(128).HasDefaultValue("default");
            e.Property(t => t.LegsJson).HasColumnName("legs").HasColumnType("text");
            e.Property(t => t.Stake).HasColumnName("stake").HasPrecision(18, 2);
            e.Property(t => t.Odds).HasColumnName("odds").HasPrecision(18, 4);
            e.Property(t => t.PotentialPayout).HasColumnName("potential_payout").HasPrecision(18, 2);
            e.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
            e.Property(t => t.Payout).HasColumnName("payout").HasPrecision(18, 2);
            e.Property(t => t.PlacedAt).HasColumnName("placed_at");
            e.Property(t => t.SettledAt).HasColumnName("settled_at");
            e.HasIndex(t => t.UserId);
            e.HasIndex(t => t.EventId);
        });
    }
}

using Microsoft.EntityFrameworkCore;
using SustainablePS.Core.Models;

namespace SustainablePS.Core.Data;

/// <summary>
/// EF Core database context for the SustainablePS marketplace.
/// Uses SQLite with a relative path so the database file travels with the project
/// and works on any computer without changing the connection string.
/// </summary>
public sealed class MarketplaceDbContext : DbContext
{
    /// <summary>
    /// Relative path to the SQLite database file.
    /// Resolved at runtime against AppContext.BaseDirectory so the DB
    /// sits next to the executable on every machine.
    /// </summary>
    public const string RelativeDbPath = "sustainableps.db";

    /// <inheritdoc />
    public MarketplaceDbContext() { }

    /// <inheritdoc />
    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : base(options) { }

    /// <summary>All registered user accounts (customers and merchants).</summary>
    public DbSet<UserAccount> Users { get; set; } = null!;

    /// <summary>All products in the catalog (active and soft-deleted).</summary>
    public DbSet<Product> Products { get; set; } = null!;

    /// <summary>All orders placed by customers.</summary>
    public DbSet<Order> Orders { get; set; } = null!;

    /// <summary>Line items belonging to orders.</summary>
    public DbSet<OrderItem> OrderItems { get; set; } = null!;

    /// <summary>In-app notifications for customers and merchants.</summary>
    public DbSet<Notification> Notifications { get; set; } = null!;

    /// <summary>Shopping cart entries — one row per customer+product combination.</summary>
    public DbSet<CartEntry> CartEntries { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Resolve the absolute path from the executable directory.
            // Using a relative path (not a hardcoded absolute one) means
            // the database works on any machine without changing the connection string.
            var fullPath = Path.Combine(AppContext.BaseDirectory, RelativeDbPath);
            optionsBuilder.UseSqlite($"Data Source={fullPath}");
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── UserAccount ────────────────────────────────────────────────────
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            // Persist enum as human-readable string
            entity.Property(u => u.Role).HasConversion<string>();
        });

        // ── Product ────────────────────────────────────────────────────────
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            // SQLite treats DECIMAL as TEXT; specify explicitly for clarity
            entity.Property(p => p.Price).HasColumnType("TEXT");
        });

        // ── Order ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Status).HasConversion<string>();
            entity.Property(o => o.PaymentStatus).HasConversion<string>();
            // Ignore computed properties — they are derived from Items in memory
            entity.Ignore(o => o.TotalPrice);
            entity.Ignore(o => o.TotalCarbonKg);
            entity.HasMany(o => o.Items)
                  .WithOne()
                  .HasForeignKey(i => i.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── OrderItem ──────────────────────────────────────────────────────
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.UnitPrice).HasColumnType("TEXT");
            // Ignore computed line totals
            entity.Ignore(i => i.LineTotal);
            entity.Ignore(i => i.LineCarbonKg);
        });

        // ── Notification ───────────────────────────────────────────────────
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Type).HasConversion<string>();
        });

        // ── CartEntry ──────────────────────────────────────────────────────
        modelBuilder.Entity<CartEntry>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.CustomerId, c.ProductId }).IsUnique();
        });
    }
}

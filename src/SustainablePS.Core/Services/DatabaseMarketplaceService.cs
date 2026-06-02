using Microsoft.EntityFrameworkCore;
using SustainablePS.Core.Data;
using SustainablePS.Core.Models;
using SustainablePS.Core.Security;

namespace SustainablePS.Core.Services;

/// <summary>
/// Production implementation of the marketplace backed by SQLite via EF Core.
/// The database file uses a relative path (resolved at runtime from the executable
/// directory) so it works on any machine without changing the connection string.
///
/// This service owns the <see cref="MarketplaceDbContext"/> lifetime and applies
/// EnsureCreated on startup to auto-provision the schema and seed demo data.
/// </summary>
public sealed class DatabaseMarketplaceService
{
    // ──────────────────────────────────────────────────────────────────────
    // Demo credentials (same as original MarketplaceService for compatibility)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Shared demo password for seeded accounts.</summary>
    public const string DemoPassword = "demo123";

    /// <summary>Email address of the seeded demo customer account.</summary>
    public const string DemoCustomerEmail = "customer@sustainable.test";

    /// <summary>Email address of the seeded demo merchant account.</summary>
    public const string DemoMerchantEmail = "merchant@sustainable.test";

    // ──────────────────────────────────────────────────────────────────────
    // Events — custom delegate types defined in MarketplaceEvents.cs
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Raised after a customer's checkout succeeds and an order is created.</summary>
    public event OrderConfirmedHandler? OrderConfirmed;

    /// <summary>Raised whenever a product's stock quantity changes.</summary>
    public event StockChangedHandler? StockChanged;

    /// <summary>Raised when a merchant advances the status of an order.</summary>
    public event OrderStatusChangedHandler? OrderStatusChanged;

    /// <summary>Raised when a merchant adds a new product to the catalog.</summary>
    public event ProductAddedHandler? ProductAdded;

    // ──────────────────────────────────────────────────────────────────────
    // Private state
    // ──────────────────────────────────────────────────────────────────────

    private readonly IDbContextFactory<MarketplaceDbContext> _dbFactory;
    private readonly CarbonCalculator _carbonCalculator = new();
    private readonly MockPaymentProcessor _paymentProcessor = new();

    // ──────────────────────────────────────────────────────────────────────
    // Construction & schema bootstrap
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Initializes the service, ensures the SQLite schema exists, and seeds demo data
    /// if the Users table is empty.
    /// </summary>
    public DatabaseMarketplaceService(IDbContextFactory<MarketplaceDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        using var db = _dbFactory.CreateDbContext();
        // EnsureCreated creates all tables defined in OnModelCreating when the DB
        // file does not yet exist — no separate migration step required.
        db.Database.EnsureCreated();
        if (!db.Users.Any())
        {
            SeedDemoData(db);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Authentication
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Authenticates a user by email and password.
    /// Throws <see cref="InvalidOperationException"/> when credentials are invalid.
    /// </summary>
    /// <param name="email">The user's email address (case-insensitive).</param>
    /// <param name="password">The plaintext password to verify against the stored hash.</param>
    public UserAccount Login(string email, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        using var db = _dbFactory.CreateDbContext();
        var normalized = NormalizeEmail(email);
        var user = db.Users.SingleOrDefault(u => u.Email == normalized);
        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid email or password.");
        }
        return user;
    }

    /// <summary>
    /// Registers a new user account.
    /// Throws <see cref="InvalidOperationException"/> when the email is already taken.
    /// </summary>
    public UserAccount Register(string fullName, string email, string password, UserRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        using var db = _dbFactory.CreateDbContext();
        var normalized = NormalizeEmail(email);
        if (db.Users.Any(u => u.Email == normalized))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }
        var account = new UserAccount
        {
            FullName = fullName.Trim(),
            Email = normalized,
            PasswordHash = PasswordHasher.Hash(password),
            Role = role
        };
        db.Users.Add(account);
        db.SaveChanges();
        return account;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Product catalog
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all active products ordered by name.
    /// Uses LINQ Where + OrderBy on the EF IQueryable — translated to SQL.
    /// </summary>
    public IReadOnlyList<Product> GetProducts()
    {
        using var db = _dbFactory.CreateDbContext();
        // LINQ query: filter IsActive, order alphabetically
        return db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToList();
    }

    /// <summary>
    /// Adds a new product to the catalog and raises the <see cref="ProductAdded"/> event.
    /// </summary>
    public Product AddProduct(Guid merchantId, ProductDraft draft)
    {
        EnsureRole(merchantId, UserRole.Merchant);
        draft.Validate();
        using var db = _dbFactory.CreateDbContext();
        var product = new Product
        {
            MerchantId = merchantId,
            Name = draft.Name.Trim(),
            Description = draft.Description.Trim(),
            Category = draft.Category.Trim(),
            Price = draft.Price,
            StockQuantity = draft.StockQuantity,
            CarbonKgPerUnit = draft.CarbonKgPerUnit
        };
        db.Products.Add(product);
        db.SaveChanges();
        // Raise ProductAdded event — subscribers (UI layers, loggers) can react
        ProductAdded?.Invoke(merchantId, product.Name, product.CarbonKgPerUnit);
        return product;
    }

    /// <summary>Updates an existing product's catalog details.</summary>
    public void UpdateProduct(Guid merchantId, Guid productId, ProductDraft draft)
    {
        EnsureRole(merchantId, UserRole.Merchant);
        draft.Validate();
        using var db = _dbFactory.CreateDbContext();
        var product = db.Products.Single(p => p.Id == productId && p.MerchantId == merchantId);
        product.Name = draft.Name.Trim();
        product.Description = draft.Description.Trim();
        product.Category = draft.Category.Trim();
        product.Price = draft.Price;
        product.StockQuantity = draft.StockQuantity;
        product.CarbonKgPerUnit = draft.CarbonKgPerUnit;
        db.SaveChanges();
    }

    /// <summary>
    /// Updates the stock quantity of a product and raises the <see cref="StockChanged"/> event.
    /// </summary>
    public void UpdateStock(Guid merchantId, Guid productId, int stockQuantity)
    {
        EnsureRole(merchantId, UserRole.Merchant);
        if (stockQuantity < 0)
        {
            throw new ArgumentException("Stock quantity cannot be negative.", nameof(stockQuantity));
        }
        using var db = _dbFactory.CreateDbContext();
        var product = db.Products.Single(p => p.Id == productId && p.MerchantId == merchantId);
        product.StockQuantity = stockQuantity;
        db.SaveChanges();
        // Notify subscribers that stock has changed
        StockChanged?.Invoke(product.Id, product.Name, product.StockQuantity);
    }

    /// <summary>Soft-deletes a product (sets IsActive to false).</summary>
    public void DeleteProduct(Guid merchantId, Guid productId)
    {
        EnsureRole(merchantId, UserRole.Merchant);
        using var db = _dbFactory.CreateDbContext();
        var product = db.Products.Single(p => p.Id == productId && p.MerchantId == merchantId);
        product.IsActive = false;
        db.SaveChanges();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Shopping cart
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Adds <paramref name="quantity"/> units of a product to the customer's cart.</summary>
    public void AddToCart(Guid customerId, Guid productId, int quantity)
    {
        EnsureRole(customerId, UserRole.Customer);
        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        }
        using var db = _dbFactory.CreateDbContext();
        var product = db.Products.Single(p => p.Id == productId && p.IsActive);
        var entry = db.CartEntries.SingleOrDefault(c => c.CustomerId == customerId && c.ProductId == productId);
        var nextQty = (entry?.Quantity ?? 0) + quantity;
        if (nextQty > product.StockQuantity)
        {
            throw new InvalidOperationException("There is not enough stock for this product.");
        }
        if (entry is null)
        {
            db.CartEntries.Add(new CartEntry
            {
                CustomerId = customerId,
                ProductId = productId,
                Quantity = nextQty
            });
        }
        else
        {
            entry.Quantity = nextQty;
        }
        db.SaveChanges();
    }

    /// <summary>Sets the quantity of a product in the cart; removes the entry when quantity ≤ 0.</summary>
    public void SetCartQuantity(Guid customerId, Guid productId, int quantity)
    {
        EnsureRole(customerId, UserRole.Customer);
        using var db = _dbFactory.CreateDbContext();
        var entry = db.CartEntries.SingleOrDefault(c => c.CustomerId == customerId && c.ProductId == productId);
        if (quantity <= 0)
        {
            if (entry is not null)
            {
                db.CartEntries.Remove(entry);
                db.SaveChanges();
            }
            return;
        }
        var product = db.Products.Single(p => p.Id == productId && p.IsActive);
        if (quantity > product.StockQuantity)
        {
            throw new InvalidOperationException("There is not enough stock for this product.");
        }
        if (entry is null)
        {
            db.CartEntries.Add(new CartEntry { CustomerId = customerId, ProductId = productId, Quantity = quantity });
        }
        else
        {
            entry.Quantity = quantity;
        }
        db.SaveChanges();
    }

    /// <summary>
    /// Returns all cart lines for the given customer.
    /// Uses LINQ Join (via navigation) to pair cart entries with product details.
    /// </summary>
    public IReadOnlyList<CartLine> GetCart(Guid customerId)
    {
        EnsureRole(customerId, UserRole.Customer);
        using var db = _dbFactory.CreateDbContext();
        // LINQ Join: CartEntries -> Products, then construct CartLine in memory.
        var rows = db.CartEntries
            .Where(c => c.CustomerId == customerId)
            .Join(db.Products.Where(p => p.IsActive),
                  c => c.ProductId,
                  p => p.Id,
                  (c, p) => new { Product = p, c.Quantity })
            .OrderBy(row => row.Product.Name)
            .ToList();

        return rows
            .Select(row => new CartLine(row.Product, row.Quantity))
            .ToList();
    }

    /// <summary>Returns the total carbon footprint of the customer's current cart.</summary>
    public double GetCartCarbon(Guid customerId)
    {
        return _carbonCalculator.CalculateCartCarbon(GetCart(customerId));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Checkout
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Processes payment and converts the customer's cart into an order.
    /// On success, raises the <see cref="OrderConfirmed"/> event and sends notifications.
    /// Stock is rolled back atomically if any step fails.
    /// </summary>
    public CheckoutResult Checkout(Guid customerId, PaymentDetails paymentDetails)
    {
        EnsureRole(customerId, UserRole.Customer);
        using var db = _dbFactory.CreateDbContext();

        // Load cart lines
        var lines = GetCart(customerId);
        if (lines.Count == 0)
        {
            return new CheckoutResult(false, "Cart is empty.", null, null);
        }

        // Validate stock availability
        foreach (var line in lines)
        {
            var product = db.Products.Single(p => p.Id == line.Product.Id);
            if (line.Quantity > product.StockQuantity)
            {
                return new CheckoutResult(false, $"{line.Product.Name} is out of stock.", null, null);
            }
        }

        // Attempt payment charge
        var payment = _paymentProcessor.Charge(paymentDetails, lines.Sum(l => l.LineTotal));
        if (!payment.Success)
        {
            return new CheckoutResult(false, payment.Message, null, null);
        }

        // Use EF transaction to guarantee atomicity: stock deduction + order creation + cart clear
        using var tx = db.Database.BeginTransaction();
        try
        {
            // Deduct stock for each purchased product
            foreach (var line in lines)
            {
                var product = db.Products.Single(p => p.Id == line.Product.Id);
                product.StockQuantity -= line.Quantity;
            }

            // Build the order
            var order = new Order
            {
                CustomerId = customerId,
                TransactionId = payment.TransactionId!,
                Items = lines.Select(line => new OrderItem
                {
                    ProductId = line.Product.Id,
                    MerchantId = line.Product.MerchantId,
                    ProductName = line.Product.Name,
                    UnitPrice = line.Product.Price,
                    Quantity = line.Quantity,
                    CarbonKgPerUnit = line.Product.CarbonKgPerUnit
                }).ToList()
            };
            db.Orders.Add(order);

            // Remove all cart entries for this customer
            var cartEntries = db.CartEntries.Where(c => c.CustomerId == customerId).ToList();
            db.CartEntries.RemoveRange(cartEntries);

            // Create notifications
            AddOrderNotifications(db, order);

            db.SaveChanges();
            tx.Commit();

            // Raise OrderConfirmed event outside the transaction
            OrderConfirmed?.Invoke(customerId, order.Id, order.TotalCarbonKg);

            return new CheckoutResult(true, "Order confirmed.", order, payment.TransactionId);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Orders
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all orders for the given customer, newest first.
    /// Uses LINQ Include (eager loading) to fetch order items in a single query.
    /// </summary>
    public IReadOnlyList<Order> GetCustomerOrders(Guid customerId)
    {
        EnsureRole(customerId, UserRole.Customer);
        using var db = _dbFactory.CreateDbContext();
        // LINQ Include: load related Items in one SQL JOIN
        return db.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .AsEnumerable()
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
    }

    /// <summary>Returns all orders that contain at least one item from the given merchant.</summary>
    public IReadOnlyList<Order> GetMerchantOrders(Guid merchantId)
    {
        EnsureRole(merchantId, UserRole.Merchant);
        using var db = _dbFactory.CreateDbContext();
        return db.Orders
            .Include(o => o.Items)
            .Where(o => o.Items.Any(i => i.MerchantId == merchantId))
            .AsEnumerable()
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Advances the fulfillment status of an order and raises the <see cref="OrderStatusChanged"/> event.
    /// </summary>
    public void UpdateOrderStatus(Guid merchantId, Guid orderId, OrderStatus status)
    {
        EnsureRole(merchantId, UserRole.Merchant);
        using var db = _dbFactory.CreateDbContext();
        var order = db.Orders
            .Include(o => o.Items)
            .Single(o => o.Id == orderId && o.Items.Any(i => i.MerchantId == merchantId));
        if (order.Status == status) { return; }
        order.Status = status;
        db.Notifications.Add(new Notification
        {
            UserId = order.CustomerId,
            Type = NotificationType.ShippingUpdate,
            Title = $"Order {status}",
            Message = $"Your order {order.Id.ToString()[..8]} is now {status}."
        });
        db.SaveChanges();
        // Notify UI layers that the order status changed
        OrderStatusChanged?.Invoke(order.Id, status.ToString());
    }

    // ──────────────────────────────────────────────────────────────────────
    // Carbon & analytics
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Builds a monthly carbon summary for the given customer's order history.</summary>
    public CarbonSummary GetCustomerCarbonSummary(Guid customerId)
    {
        return _carbonCalculator.BuildCustomerSummary(GetCustomerOrders(customerId));
    }

    /// <summary>
    /// Returns a LINQ-based analytics snapshot of the catalog and purchase history.
    ///
    /// Demonstrates: GroupBy, Select, Where, Sum, Average, Count, OrderBy, Join, FirstOrDefault.
    /// All LINQ queries below use method syntax and are executed in-memory after
    /// loading the required data sets from SQLite.
    /// </summary>
    public CatalogAnalytics GetCatalogAnalytics()
    {
        using var db = _dbFactory.CreateDbContext();

        // Load all needed data in two DB round-trips
        var activeProducts = db.Products.Where(p => p.IsActive).ToList();
        var paidOrders = db.Orders
            .Include(o => o.Items)
            .Where(o => o.PaymentStatus == PaymentStatus.Paid)
            .ToList();
        var users = db.Users.ToList();

        // ── 1. Category breakdown — GroupBy + Select + Average + Sum ──────
        var categoryStats = activeProducts
            .GroupBy(p => p.Category)
            .Select(g => new CategoryStat(
                Category: g.Key,
                ProductCount: g.Count(),
                AverageCarbonKgPerUnit: Math.Round(g.Average(p => p.CarbonKgPerUnit), 2),
                TotalStock: g.Sum(p => p.StockQuantity)))
            .OrderBy(s => s.Category)
            .ToList();

        // ── 2. Revenue per merchant — SelectMany + GroupBy + Join ─────────
        var revenueByMerchant = paidOrders
            .SelectMany(o => o.Items)
            .GroupBy(item => item.MerchantId)
            .Select(g => new MerchantRevenueStat(
                MerchantId: g.Key,
                MerchantName: users
                    .Where(u => u.Id == g.Key)
                    .Select(u => u.FullName)
                    .FirstOrDefault() ?? "Unknown",
                TotalRevenue: g.Sum(item => item.LineTotal),
                TotalCarbonKg: Math.Round(g.Sum(item => item.LineCarbonKg), 2),
                ItemsSold: g.Sum(item => item.Quantity)))
            .OrderByDescending(s => s.TotalRevenue)
            .ToList();

        // ── 3. Low-stock alerts — Where + OrderBy ─────────────────────────
        const int LowStockThreshold = 5;
        var lowStock = activeProducts
            .Where(p => p.StockQuantity <= LowStockThreshold)
            .OrderBy(p => p.StockQuantity)
            .Select(p => new LowStockAlert(p.Id, p.Name, p.StockQuantity))
            .ToList();

        return new CatalogAnalytics(categoryStats, revenueByMerchant, lowStock);
    }

    /// <summary>Builds an impact report of highest/lowest carbon and most purchased products.</summary>
    public ImpactReport BuildImpactReport()
    {
        using var db = _dbFactory.CreateDbContext();
        var activeProducts = db.Products.Where(p => p.IsActive).ToList();
        var paidItems = db.Orders
            .Include(o => o.Items)
            .Where(o => o.PaymentStatus == PaymentStatus.Paid)
            .SelectMany(o => o.Items)
            .ToList();

        var purchasedStats = paidItems
            .GroupBy(i => new { i.ProductId, i.ProductName, i.CarbonKgPerUnit })
            .Select(g =>
            {
                var category = activeProducts.SingleOrDefault(p => p.Id == g.Key.ProductId)?.Category ?? "Unknown";
                return new ProductImpactStat(
                    g.Key.ProductId, g.Key.ProductName, category,
                    g.Sum(i => i.Quantity), g.Key.CarbonKgPerUnit,
                    Math.Round(g.Sum(i => i.LineCarbonKg), 2));
            }).ToList();

        var catalogStats = activeProducts
            .Select(p => new ProductImpactStat(p.Id, p.Name, p.Category,
                purchasedStats.SingleOrDefault(s => s.ProductId == p.Id)?.UnitsPurchased ?? 0,
                p.CarbonKgPerUnit, p.CarbonKgPerUnit))
            .ToList();

        return new ImpactReport(
            catalogStats.OrderByDescending(s => s.CarbonKgPerUnit).Take(5).ToList(),
            catalogStats.OrderBy(s => s.CarbonKgPerUnit).Take(5).ToList(),
            purchasedStats.OrderByDescending(s => s.UnitsPurchased).Take(5).ToList());
    }

    // ──────────────────────────────────────────────────────────────────────
    // Notifications
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Returns all notifications for the given user, newest first.</summary>
    public IReadOnlyList<Notification> GetNotifications(Guid userId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Notifications
            .Where(n => n.UserId == userId)
            .AsEnumerable()
            .OrderByDescending(n => n.CreatedAt)
            .ToList();
    }

    /// <summary>Marks a single notification as read.</summary>
    public void MarkNotificationRead(Guid userId, Guid notificationId)
    {
        using var db = _dbFactory.CreateDbContext();
        var notification = db.Notifications.SingleOrDefault(n => n.UserId == userId && n.Id == notificationId);
        if (notification is null) { return; }
        notification.IsRead = true;
        db.SaveChanges();
    }

    /// <summary>Marks all notifications for the given user as read.</summary>
    public void MarkAllNotificationsRead(Guid userId)
    {
        using var db = _dbFactory.CreateDbContext();
        // LINQ Where to filter then bulk update
        var unread = db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToList();
        foreach (var n in unread) { n.IsRead = true; }
        db.SaveChanges();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the given user exists and holds the required role.
    /// Throws <see cref="InvalidOperationException"/> when the check fails.
    /// </summary>
    private void EnsureRole(Guid userId, UserRole requiredRole)
    {
        using var db = _dbFactory.CreateDbContext();
        var user = db.Users.SingleOrDefault(u => u.Id == userId);
        if (user?.Role != requiredRole)
        {
            throw new InvalidOperationException($"{requiredRole} access is required.");
        }
    }

    /// <summary>Creates order confirmation and merchant sale notifications in the given DB context.</summary>
    private static void AddOrderNotifications(MarketplaceDbContext db, Order order)
    {
        // Notify the customer their order was confirmed
        db.Notifications.Add(new Notification
        {
            UserId = order.CustomerId,
            Type = NotificationType.OrderConfirmation,
            Title = "Order confirmed",
            Message = $"Your order {order.Id.ToString()[..8]} was confirmed. Total carbon: {order.TotalCarbonKg:F2} kg CO2e."
        });

        // Notify each merchant that items were sold
        foreach (var merchantId in order.Items.Select(i => i.MerchantId).Distinct())
        {
            db.Notifications.Add(new Notification
            {
                UserId = merchantId,
                Type = NotificationType.StockAlert,
                Title = "New order received",
                Message = $"A customer purchased {order.Items.Where(i => i.MerchantId == merchantId).Sum(i => i.Quantity)} item(s)."
            });
        }
    }

    /// <summary>Normalizes an email address to lowercase trimmed form for consistent lookups.</summary>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Seeds the database with demo accounts and products so the application
    /// is immediately usable after the first run.
    /// </summary>
    private static void SeedDemoData(MarketplaceDbContext db)
    {
        // Create demo accounts
        var customer = new UserAccount
        {
            FullName = "Demo Customer", Email = DemoCustomerEmail,
            PasswordHash = PasswordHasher.Hash(DemoPassword), Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.Now.AddDays(-40)
        };
        var merchant = new UserAccount
        {
            FullName = "Green Merchant", Email = DemoMerchantEmail,
            PasswordHash = PasswordHasher.Hash(DemoPassword), Role = UserRole.Merchant,
            CreatedAt = DateTimeOffset.Now.AddDays(-60)
        };
        db.Users.AddRange(customer, merchant);

        // Create demo products with varied carbon footprints
        var brush = new Product { MerchantId = merchant.Id, Name = "Bamboo Toothbrush",
            Description = "Compostable handle with plant-based bristles.", Category = "Personal Care",
            Price = 4.99m, StockQuantity = 42, CarbonKgPerUnit = 0.18 };
        var notebook = new Product { MerchantId = merchant.Id, Name = "Recycled Paper Notebook",
            Description = "A5 notebook made from 100% post-consumer paper.", Category = "Stationery",
            Price = 8.50m, StockQuantity = 25, CarbonKgPerUnit = 0.62 };
        var tote = new Product { MerchantId = merchant.Id, Name = "Organic Cotton Tote",
            Description = "Reusable bag produced with certified organic cotton.", Category = "Bags",
            Price = 14.00m, StockQuantity = 18, CarbonKgPerUnit = 1.10 };
        var solar = new Product { MerchantId = merchant.Id, Name = "Solar Power Bank",
            Description = "Portable charger with integrated solar panel.", Category = "Electronics",
            Price = 39.90m, StockQuantity = 9, CarbonKgPerUnit = 3.40 };
        db.Products.AddRange(brush, notebook, tote, solar);

        // Seed one demo order for purchase history
        var order = new Order
        {
            CustomerId = customer.Id, TransactionId = "TX-DEMO-001",
            CreatedAt = DateTimeOffset.Now.AddMonths(-1),
            Items = [
                new OrderItem { ProductId = brush.Id, MerchantId = merchant.Id,
                    ProductName = brush.Name, UnitPrice = brush.Price,
                    Quantity = 2, CarbonKgPerUnit = brush.CarbonKgPerUnit },
                new OrderItem { ProductId = notebook.Id, MerchantId = merchant.Id,
                    ProductName = notebook.Name, UnitPrice = notebook.Price,
                    Quantity = 1, CarbonKgPerUnit = notebook.CarbonKgPerUnit }
            ]
        };
        db.Orders.Add(order);
        db.SaveChanges();
    }
}

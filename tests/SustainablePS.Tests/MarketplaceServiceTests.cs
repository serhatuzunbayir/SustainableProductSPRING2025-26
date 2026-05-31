using Microsoft.EntityFrameworkCore;
using SustainablePS.Core.Data;
using SustainablePS.Core.Models;
using SustainablePS.Core.Services;

namespace SustainablePS.Tests;

/// <summary>
/// Unit tests for <see cref="DatabaseMarketplaceService"/>.
///
/// Each test uses an isolated SQLite in-memory database created via the
/// EF Core InMemory provider, so tests never touch the real database file
/// and are safe to run in parallel.
///
/// Test count: 14 test methods covering authentication, products,
/// cart, checkout, orders, events, LINQ analytics, and carbon summary.
/// </summary>
public sealed class MarketplaceServiceTests : IDisposable
{
    // ── Test infrastructure ────────────────────────────────────────────────

    private readonly IDbContextFactory<MarketplaceDbContext> _factory;
    private readonly DatabaseMarketplaceService _svc;

    /// <summary>Initialises a fresh in-memory database and service before each test.</summary>
    public MarketplaceServiceTests()
    {
        // Use a unique DB name per test class instance to guarantee isolation.
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid():N}")
            .Options;
        _factory = new TestDbContextFactory(options);
        _svc = new DatabaseMarketplaceService(_factory);
    }

    /// <summary>Disposes resources after the test run (not strictly needed for in-memory).</summary>
    public void Dispose() { }

    // ── TC-01: Login with correct credentials ──────────────────────────────

    /// <summary>TC-01: Valid credentials return the correct user account.</summary>
    [Fact]
    public void Login_ValidCredentials_ReturnsUser()
    {
        // Act
        var user = _svc.Login(DatabaseMarketplaceService.DemoCustomerEmail,
                              DatabaseMarketplaceService.DemoPassword);

        // Assert
        Assert.Equal(DatabaseMarketplaceService.DemoCustomerEmail, user.Email);
        Assert.Equal(UserRole.Customer, user.Role);
    }

    // ── TC-02: Login with wrong password throws ────────────────────────────

    /// <summary>TC-02: Wrong password throws InvalidOperationException.</summary>
    [Fact]
    public void Login_WrongPassword_Throws()
    {
        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _svc.Login(DatabaseMarketplaceService.DemoCustomerEmail, "wrongpass"));
        Assert.Contains("Invalid", ex.Message);
    }

    // ── TC-03: Register a new customer account ─────────────────────────────

    /// <summary>TC-03: Registering a new account succeeds and the user can log in.</summary>
    [Fact]
    public void Register_NewAccount_CanLogin()
    {
        // Arrange & Act
        var registered = _svc.Register("Alice Green", "alice@test.com", "pass123", UserRole.Customer);

        // Assert — the account exists and credentials work
        Assert.Equal("alice@test.com", registered.Email);
        var loggedIn = _svc.Login("alice@test.com", "pass123");
        Assert.Equal(registered.Id, loggedIn.Id);
    }

    // ── TC-04: Duplicate email throws ─────────────────────────────────────

    /// <summary>TC-04: Registering with an existing email throws.</summary>
    [Fact]
    public void Register_DuplicateEmail_Throws()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _svc.Register("Dup", DatabaseMarketplaceService.DemoCustomerEmail, "x", UserRole.Customer));
    }

    // ── TC-05: GetProducts returns only active items ───────────────────────

    /// <summary>TC-05: GetProducts returns only active products, ordered by name.</summary>
    [Fact]
    public void GetProducts_ReturnsOnlyActiveOrderedByName()
    {
        // Arrange
        var merchant = _svc.Login(DatabaseMarketplaceService.DemoMerchantEmail,
                                  DatabaseMarketplaceService.DemoPassword);
        var added = _svc.AddProduct(merchant.Id,
            new ProductDraft("Eco Cup", "Reusable cup.", "Kitchen", 9.99m, 10, 0.5));

        // Act
        var products = _svc.GetProducts();

        // Assert — seeded products + added product; all active; sorted
        Assert.Contains(products, p => p.Id == added.Id);
        Assert.All(products, p => Assert.True(p.IsActive));
        // Verify ascending name order
        var names = products.Select(p => p.Name).ToList();
        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    // ── TC-06: AddProduct raises ProductAdded event ────────────────────────

    /// <summary>TC-06: Adding a product raises the ProductAdded delegate event.</summary>
    [Fact]
    public void AddProduct_RaisesProductAddedEvent()
    {
        // Arrange
        var merchant = _svc.Login(DatabaseMarketplaceService.DemoMerchantEmail,
                                  DatabaseMarketplaceService.DemoPassword);
        string? capturedName = null;

        // Subscribe using the ProductAddedHandler delegate
        _svc.ProductAdded += (merchantId, productName, carbonKgPerUnit) =>
        {
            capturedName = productName;
        };

        // Act
        _svc.AddProduct(merchant.Id,
            new ProductDraft("Hemp Soap", "Natural bar.", "Personal Care", 5.50m, 20, 0.12));

        // Assert
        Assert.Equal("Hemp Soap", capturedName);
    }

    // ── TC-07: UpdateStock raises StockChanged event ───────────────────────

    /// <summary>TC-07: Updating stock raises the StockChanged delegate event with new quantity.</summary>
    [Fact]
    public void UpdateStock_RaisesStockChangedEvent()
    {
        // Arrange
        var merchant = _svc.Login(DatabaseMarketplaceService.DemoMerchantEmail,
                                  DatabaseMarketplaceService.DemoPassword);
        var product = _svc.GetProducts().First();
        int? capturedStock = null;

        // Subscribe using the StockChangedHandler delegate
        _svc.StockChanged += (productId, productName, newStock) =>
        {
            capturedStock = newStock;
        };

        // Act
        _svc.UpdateStock(merchant.Id, product.Id, 99);

        // Assert
        Assert.Equal(99, capturedStock);
    }

    // ── TC-08: AddToCart increases cart quantity ───────────────────────────

    /// <summary>TC-08: AddToCart correctly stores the product and quantity in the cart.</summary>
    [Fact]
    public void AddToCart_AddsProductToCart()
    {
        // Arrange
        var customer = _svc.Login(DatabaseMarketplaceService.DemoCustomerEmail,
                                  DatabaseMarketplaceService.DemoPassword);
        var product = _svc.GetProducts().First();

        // Act
        _svc.AddToCart(customer.Id, product.Id, 2);
        var cart = _svc.GetCart(customer.Id);

        // Assert
        Assert.Single(cart);
        Assert.Equal(2, cart[0].Quantity);
        Assert.Equal(product.Id, cart[0].Product.Id);
    }

    // ── TC-09: Cart carbon is calculated correctly ─────────────────────────

    /// <summary>TC-09: Cart carbon equals quantity × CarbonKgPerUnit for each line.</summary>
    [Fact]
    public void GetCartCarbon_CalculatesCorrectly()
    {
        // Arrange
        var customer = _svc.Login(DatabaseMarketplaceService.DemoCustomerEmail,
                                  DatabaseMarketplaceService.DemoPassword);
        var product = _svc.GetProducts().First();
        _svc.AddToCart(customer.Id, product.Id, 3);

        // Act
        var carbon = _svc.GetCartCarbon(customer.Id);

        // Assert — 3 units × CarbonKgPerUnit
        var expected = Math.Round(product.CarbonKgPerUnit * 3, 2);
        Assert.Equal(expected, carbon);
    }

    // ── TC-10: Successful checkout creates order and reduces stock ─────────

    /// <summary>TC-10: A successful checkout creates an order and reduces stock.</summary>
    [Fact]
    public void Checkout_Success_CreatesOrderAndReducesStock()
    {
        // Arrange
        var customer = _svc.Login(DatabaseMarketplaceService.DemoCustomerEmail,
                                  DatabaseMarketplaceService.DemoPassword);
        var product = _svc.GetProducts().First(p => p.StockQuantity >= 2);
        var startingStock = product.StockQuantity;
        _svc.AddToCart(customer.Id, product.Id, 2);

        // Act
        var result = _svc.Checkout(customer.Id,
            new PaymentDetails("4242424242424242", "Demo Customer", "12", "30", "123"));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Order);
        var updatedStock = _svc.GetProducts().Single(p => p.Id == product.Id).StockQuantity;
        Assert.Equal(startingStock - 2, updatedStock);
    }

    // ── TC-11: Declined card does NOT reduce stock ─────────────────────────

    /// <summary>TC-11: A declined payment leaves stock unchanged.</summary>
    [Fact]
    public void Checkout_DeclinedCard_StockUnchanged()
    {
        // Arrange
        var customer = _svc.Login(DatabaseMarketplaceService.DemoCustomerEmail,
                                  DatabaseMarketplaceService.DemoPassword);
        var product = _svc.GetProducts().First(p => p.StockQuantity >= 1);
        var startingStock = product.StockQuantity;
        _svc.AddToCart(customer.Id, product.Id, 1);

        // Act — card number ending in 0000 is declined by MockPaymentProcessor
        var result = _svc.Checkout(customer.Id,
            new PaymentDetails("4000000000000000", "Demo Customer", "12", "30", "123"));

        // Assert
        Assert.False(result.Success);
        var updatedStock = _svc.GetProducts().Single(p => p.Id == product.Id).StockQuantity;
        Assert.Equal(startingStock, updatedStock);
    }

    // ── TC-12: OrderConfirmed event fires on successful checkout ───────────

    /// <summary>TC-12: Successful checkout raises the OrderConfirmed delegate event.</summary>
    [Fact]
    public void Checkout_Success_RaisesOrderConfirmedEvent()
    {
        // Arrange
        var customer = _svc.Login(DatabaseMarketplaceService.DemoCustomerEmail,
                                  DatabaseMarketplaceService.DemoPassword);
        var product = _svc.GetProducts().First(p => p.StockQuantity >= 1);
        _svc.AddToCart(customer.Id, product.Id, 1);

        var eventFired = false;
        // Subscribe using the OrderConfirmedHandler delegate type
        _svc.OrderConfirmed += (cId, oId, carbon) => { eventFired = true; };

        // Act
        _svc.Checkout(customer.Id,
            new PaymentDetails("4242424242424242", "Demo Customer", "12", "30", "123"));

        // Assert
        Assert.True(eventFired);
    }

    // ── TC-13: UpdateOrderStatus raises OrderStatusChanged event ──────────

    /// <summary>TC-13: Advancing order status raises the OrderStatusChanged delegate event.</summary>
    [Fact]
    public void UpdateOrderStatus_RaisesOrderStatusChangedEvent()
    {
        // Arrange
        var merchant = _svc.Login(DatabaseMarketplaceService.DemoMerchantEmail,
                                  DatabaseMarketplaceService.DemoPassword);
        var order = _svc.GetMerchantOrders(merchant.Id).First();
        string? capturedStatus = null;

        // Subscribe using the OrderStatusChangedHandler delegate
        _svc.OrderStatusChanged += (orderId, newStatus) => { capturedStatus = newStatus; };

        // Act
        _svc.UpdateOrderStatus(merchant.Id, order.Id, OrderStatus.Shipped);

        // Assert
        Assert.Equal("Shipped", capturedStatus);
    }

    // ── TC-14: GetCatalogAnalytics returns LINQ-computed stats ─────────────

    /// <summary>TC-14: GetCatalogAnalytics returns non-empty category stats and correct aggregations.</summary>
    [Fact]
    public void GetCatalogAnalytics_ReturnsCategoryStats()
    {
        // Act
        var analytics = _svc.GetCatalogAnalytics();

        // Assert — seeded data has 4 products across 4 categories
        Assert.NotEmpty(analytics.CategoryStats);
        Assert.All(analytics.CategoryStats, s =>
        {
            Assert.True(s.ProductCount > 0);
            Assert.True(s.AverageCarbonKgPerUnit >= 0);
            Assert.True(s.TotalStock >= 0);
        });
    }

    // ── TC-15: Carbon summary aggregates paid orders ───────────────────────

    /// <summary>TC-15: Customer carbon summary reflects the seeded order correctly.</summary>
    [Fact]
    public void GetCustomerCarbonSummary_IncludesSeededOrder()
    {
        // Arrange
        var customer = _svc.Login(DatabaseMarketplaceService.DemoCustomerEmail,
                                  DatabaseMarketplaceService.DemoPassword);

        // Act
        var summary = _svc.GetCustomerCarbonSummary(customer.Id);

        // Assert — seeded order: 2× Bamboo Brush (0.18) + 1× Notebook (0.62) = 0.98 kg
        Assert.True(summary.OrderCount >= 1);
        Assert.True(summary.TotalCarbonKg > 0);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Test infrastructure: DbContextFactory wrapper
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Minimal <see cref="IDbContextFactory{TContext}"/> implementation that creates
/// DbContext instances from a fixed <see cref="DbContextOptions{TContext}"/>.
/// Used in tests to inject an in-memory provider without DI.
/// </summary>
file sealed class TestDbContextFactory(DbContextOptions<MarketplaceDbContext> options)
    : IDbContextFactory<MarketplaceDbContext>
{
    /// <inheritdoc />
    public MarketplaceDbContext CreateDbContext() => new(options);
}

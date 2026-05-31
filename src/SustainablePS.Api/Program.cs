using Microsoft.EntityFrameworkCore;
using SustainablePS.Core.Data;
using SustainablePS.Core.Models;
using SustainablePS.Core.Services;

// ──────────────────────────────────────────────────────────────────────────────
// SustainablePS Web API — ASP.NET Core Minimal API
//
// Exposes REST endpoints consumed by the Blazor web UI and any external client.
// Shares the same SQLite database as the MAUI desktop application via the
// relative-path connection string configured in MarketplaceDbContext.
// ──────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// Register the EF Core DbContext factory.
// The factory pattern (IDbContextFactory) is used because DatabaseMarketplaceService
// creates short-lived contexts per operation, which is safer than a long-lived context.
builder.Services.AddDbContextFactory<MarketplaceDbContext>(options =>
{
    // Resolve the SQLite DB path relative to the executable directory.
    // This means the .db file travels with the app and requires no manual config.
    var dbPath = Path.Combine(AppContext.BaseDirectory, MarketplaceDbContext.RelativeDbPath);
    options.UseSqlite($"Data Source={dbPath}");
});

// Register the database-backed marketplace service as a singleton.
// Singleton is appropriate because DatabaseMarketplaceService creates its own
// DbContext instances per operation via IDbContextFactory.
builder.Services.AddSingleton<DatabaseMarketplaceService>();

// Register CORS so the Blazor frontend (different port) can call the API.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Add OpenAPI/Swagger documentation generation.
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply CORS policy globally.
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    // Enable Swagger UI in development.
    app.MapOpenApi();
}

// ──────────────────────────────────────────────────────────────────────────────
// API ENDPOINTS
// ──────────────────────────────────────────────────────────────────────────────

// ── Authentication ─────────────────────────────────────────────────────────

/// <summary>POST /api/auth/login — authenticates a user and returns the account details.</summary>
app.MapPost("/api/auth/login", (LoginRequest req, DatabaseMarketplaceService svc) =>
{
    try
    {
        var user = svc.Login(req.Email, req.Password);
        return Results.Ok(user);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("Login").WithSummary("Authenticate a user by email and password");

/// <summary>POST /api/auth/register — creates a new user account.</summary>
app.MapPost("/api/auth/register", (RegisterRequest req, DatabaseMarketplaceService svc) =>
{
    try
    {
        var user = svc.Register(req.FullName, req.Email, req.Password, req.Role);
        return Results.Created($"/api/users/{user.Id}", user);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("Register").WithSummary("Register a new user account");

// ── Products ───────────────────────────────────────────────────────────────

/// <summary>GET /api/products — returns all active products ordered by name.</summary>
app.MapGet("/api/products", (DatabaseMarketplaceService svc) =>
    Results.Ok(svc.GetProducts()))
    .WithName("GetProducts").WithSummary("List all active products");

/// <summary>POST /api/products — merchant adds a new product.</summary>
app.MapPost("/api/products", (AddProductRequest req, DatabaseMarketplaceService svc) =>
{
    try
    {
        var draft = new ProductDraft(req.Name, req.Description, req.Category, req.Price, req.StockQuantity, req.CarbonKgPerUnit);
        var product = svc.AddProduct(req.MerchantId, draft);
        return Results.Created($"/api/products/{product.Id}", product);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("AddProduct").WithSummary("Add a new product to the catalog");

/// <summary>PUT /api/products/{id} — merchant updates an existing product.</summary>
app.MapPut("/api/products/{id:guid}", (Guid id, UpdateProductRequest req, DatabaseMarketplaceService svc) =>
{
    try
    {
        var draft = new ProductDraft(req.Name, req.Description, req.Category, req.Price, req.StockQuantity, req.CarbonKgPerUnit);
        svc.UpdateProduct(req.MerchantId, id, draft);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("UpdateProduct").WithSummary("Update an existing product");

/// <summary>PATCH /api/products/{id}/stock — merchant updates stock quantity.</summary>
app.MapPatch("/api/products/{id:guid}/stock", (Guid id, UpdateStockRequest req, DatabaseMarketplaceService svc) =>
{
    try
    {
        svc.UpdateStock(req.MerchantId, id, req.StockQuantity);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("UpdateStock").WithSummary("Update product stock quantity");

/// <summary>DELETE /api/products/{id} — merchant soft-deletes a product.</summary>
app.MapDelete("/api/products/{id:guid}", (Guid id, Guid merchantId, DatabaseMarketplaceService svc) =>
{
    try
    {
        svc.DeleteProduct(merchantId, id);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("DeleteProduct").WithSummary("Soft-delete a product from the catalog");

// ── Cart ───────────────────────────────────────────────────────────────────

/// <summary>GET /api/cart/{customerId} — returns the customer's current cart.</summary>
app.MapGet("/api/cart/{customerId:guid}", (Guid customerId, DatabaseMarketplaceService svc) =>
{
    try
    {
        return Results.Ok(svc.GetCart(customerId));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("GetCart").WithSummary("Get cart contents for a customer");

/// <summary>POST /api/cart/add — adds a product to the customer's cart.</summary>
app.MapPost("/api/cart/add", (CartActionRequest req, DatabaseMarketplaceService svc) =>
{
    try
    {
        svc.AddToCart(req.CustomerId, req.ProductId, req.Quantity);
        return Results.Ok(new { message = "Product added to cart." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("AddToCart").WithSummary("Add a product to the cart");

/// <summary>POST /api/cart/checkout — processes payment and creates an order.</summary>
app.MapPost("/api/cart/checkout", (CheckoutRequest req, DatabaseMarketplaceService svc) =>
{
    var payment = new PaymentDetails(req.CardNumber, req.CardHolder, req.ExpiryMonth, req.ExpiryYear, req.Cvv);
    var result = svc.Checkout(req.CustomerId, payment);
    return result.Success ? Results.Ok(result) : Results.BadRequest(new { error = result.Message });
}).WithName("Checkout").WithSummary("Process checkout and create an order");

// ── Orders ─────────────────────────────────────────────────────────────────

/// <summary>GET /api/orders/customer/{customerId} — returns the customer's order history.</summary>
app.MapGet("/api/orders/customer/{customerId:guid}", (Guid customerId, DatabaseMarketplaceService svc) =>
{
    try
    {
        return Results.Ok(svc.GetCustomerOrders(customerId));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("GetCustomerOrders").WithSummary("Get order history for a customer");

/// <summary>GET /api/orders/merchant/{merchantId} — returns all orders with merchant items.</summary>
app.MapGet("/api/orders/merchant/{merchantId:guid}", (Guid merchantId, DatabaseMarketplaceService svc) =>
{
    try
    {
        return Results.Ok(svc.GetMerchantOrders(merchantId));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("GetMerchantOrders").WithSummary("Get all orders for a merchant");

/// <summary>PATCH /api/orders/{id}/status — merchant updates the fulfillment status.</summary>
app.MapPatch("/api/orders/{id:guid}/status", (Guid id, UpdateOrderStatusRequest req, DatabaseMarketplaceService svc) =>
{
    try
    {
        svc.UpdateOrderStatus(req.MerchantId, id, req.Status);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("UpdateOrderStatus").WithSummary("Update the fulfillment status of an order");

// ── Analytics & carbon ─────────────────────────────────────────────────────

/// <summary>GET /api/analytics — returns LINQ-computed catalog analytics.</summary>
app.MapGet("/api/analytics", (DatabaseMarketplaceService svc) =>
    Results.Ok(svc.GetCatalogAnalytics()))
    .WithName("GetAnalytics").WithSummary("Get catalog analytics (LINQ)");

/// <summary>GET /api/carbon/{customerId} — returns the customer's carbon summary.</summary>
app.MapGet("/api/carbon/{customerId:guid}", (Guid customerId, DatabaseMarketplaceService svc) =>
{
    try
    {
        return Results.Ok(svc.GetCustomerCarbonSummary(customerId));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("GetCarbonSummary").WithSummary("Get carbon footprint summary for a customer");

/// <summary>GET /api/impact — returns the global impact report.</summary>
app.MapGet("/api/impact", (DatabaseMarketplaceService svc) =>
    Results.Ok(svc.BuildImpactReport()))
    .WithName("GetImpactReport").WithSummary("Get global product impact report");

// ── Notifications ──────────────────────────────────────────────────────────

/// <summary>GET /api/notifications/{userId} — returns all notifications for a user.</summary>
app.MapGet("/api/notifications/{userId:guid}", (Guid userId, DatabaseMarketplaceService svc) =>
    Results.Ok(svc.GetNotifications(userId)))
    .WithName("GetNotifications").WithSummary("Get notifications for a user");

/// <summary>POST /api/notifications/{userId}/read-all — marks all notifications read.</summary>
app.MapPost("/api/notifications/{userId:guid}/read-all", (Guid userId, DatabaseMarketplaceService svc) =>
{
    svc.MarkAllNotificationsRead(userId);
    return Results.Ok(new { message = "All notifications marked as read." });
}).WithName("MarkAllRead").WithSummary("Mark all notifications as read");

app.Run();

// ──────────────────────────────────────────────────────────────────────────────
// Request / Response DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Login request body.</summary>
record LoginRequest(string Email, string Password);

/// <summary>Registration request body.</summary>
record RegisterRequest(string FullName, string Email, string Password, UserRole Role);

/// <summary>Add product request body.</summary>
record AddProductRequest(Guid MerchantId, string Name, string Description, string Category,
    decimal Price, int StockQuantity, double CarbonKgPerUnit);

/// <summary>Update product request body.</summary>
record UpdateProductRequest(Guid MerchantId, string Name, string Description, string Category,
    decimal Price, int StockQuantity, double CarbonKgPerUnit);

/// <summary>Update stock request body.</summary>
record UpdateStockRequest(Guid MerchantId, int StockQuantity);

/// <summary>Cart add/set request body.</summary>
record CartActionRequest(Guid CustomerId, Guid ProductId, int Quantity);

/// <summary>Checkout request body.</summary>
record CheckoutRequest(Guid CustomerId, string CardNumber, string CardHolder,
    string ExpiryMonth, string ExpiryYear, string Cvv);

/// <summary>Order status update request body.</summary>
record UpdateOrderStatusRequest(Guid MerchantId, OrderStatus Status);

namespace SustainablePS.Core.Services;

// ──────────────────────────────────────────────────────────────────────────────
// Delegate definitions used throughout the marketplace event system.
// Each delegate represents a strongly-typed callback signature.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Raised when a new order is confirmed successfully.
/// </summary>
/// <param name="customerId">The ID of the customer who placed the order.</param>
/// <param name="orderId">The ID of the newly created order.</param>
/// <param name="totalCarbonKg">Total carbon footprint of the order in kg CO2e.</param>
public delegate void OrderConfirmedHandler(Guid customerId, Guid orderId, double totalCarbonKg);

/// <summary>
/// Raised when a product's stock level changes (update or purchase).
/// </summary>
/// <param name="productId">The ID of the affected product.</param>
/// <param name="productName">The display name of the product.</param>
/// <param name="newStock">The new stock quantity after the change.</param>
public delegate void StockChangedHandler(Guid productId, string productName, int newStock);

/// <summary>
/// Raised when a product's order status is advanced by a merchant.
/// </summary>
/// <param name="orderId">The ID of the updated order.</param>
/// <param name="newStatus">The new order status as a string label.</param>
public delegate void OrderStatusChangedHandler(Guid orderId, string newStatus);

/// <summary>
/// Raised when a new product is added to the catalog by a merchant.
/// </summary>
/// <param name="merchantId">The ID of the merchant adding the product.</param>
/// <param name="productName">The name of the newly added product.</param>
/// <param name="carbonKgPerUnit">The carbon footprint per unit in kg CO2e.</param>
public delegate void ProductAddedHandler(Guid merchantId, string productName, double carbonKgPerUnit);

using SustainablePS.Core.Models;
using SustainablePS.Core.Security;
using SustainablePS.Core.Services;

var dataPath = Path.Combine(Path.GetTempPath(), $"sustainableps-{Guid.NewGuid():N}.json");
var service = new MarketplaceService(dataPath);
var customer = service.Login(MarketplaceService.DemoCustomerEmail, MarketplaceService.DemoPassword);
var merchant = service.Login(MarketplaceService.DemoMerchantEmail, MarketplaceService.DemoPassword);
var product = service.Products.First(item => item.StockQuantity >= 2);
var startingStock = product.StockQuantity;

service.AddToCart(customer.Id, product.Id, 2);

Assert(service.GetCart(customer.Id).Count == 1, "Cart should contain one product.");
Assert(service.GetCartCarbon(customer.Id) > 0, "Cart carbon should be calculated.");

var failedCheckout = service.Checkout(
    customer.Id,
    new PaymentDetails("4000000000000000", "Demo Customer", "12", "30", "123"));

Assert(!failedCheckout.Success, "Checkout should fail when mock bank declines the card.");
Assert(service.Products.Single(item => item.Id == product.Id).StockQuantity == startingStock, "Failed payment must not reduce stock.");

var successfulCheckout = service.Checkout(
    customer.Id,
    new PaymentDetails("4242424242424242", "Demo Customer", "12", "30", "123"));

Assert(successfulCheckout.Success, "Checkout should succeed with the demo card.");
Assert(successfulCheckout.Order is not null, "Successful checkout should return an order.");
Assert(service.Products.Single(item => item.Id == product.Id).StockQuantity == startingStock - 2, "Successful checkout should reduce stock.");
Assert(service.GetCustomerOrders(customer.Id).Count >= 2, "Customer order history should include seeded and new orders.");
Assert(service.GetMerchantOrders(merchant.Id).Any(), "Merchant should see related orders.");
Assert(service.GetCustomerCarbonSummary(customer.Id).TotalCarbonKg > 0, "Customer dashboard should aggregate carbon.");
Assert(service.GetNotifications(customer.Id).Any(), "Customer should receive an order notification.");

var addedProduct = service.AddProduct(
    merchant.Id,
    new ProductDraft("Refillable Cleaner", "Concentrated tablet with reusable bottle.", "Home", 11.25m, 15, 0.35));

Assert(service.Products.Any(item => item.Id == addedProduct.Id), "Merchant-created product should appear in catalog.");

service.UpdateProduct(
    merchant.Id,
    addedProduct.Id,
    new ProductDraft("Refillable Cleaner Kit", "Concentrated tablets with reusable bottle.", "Home", 13.50m, 12, 0.33));

Assert(service.Products.Single(item => item.Id == addedProduct.Id).Name == "Refillable Cleaner Kit", "Merchant should edit product details.");

var order = service.GetMerchantOrders(merchant.Id).First();
service.UpdateOrderStatus(merchant.Id, order.Id, OrderStatus.Shipped);
Assert(service.GetMerchantOrders(merchant.Id).First(item => item.Id == order.Id).Status == OrderStatus.Shipped, "Merchant should update order status.");
Assert(service.GetNotifications(customer.Id).Any(item => item.Type == NotificationType.ShippingUpdate), "Customer should receive shipping update notification.");

var notification = service.GetNotifications(customer.Id).First();
service.MarkNotificationRead(customer.Id, notification.Id);
Assert(service.GetNotifications(customer.Id).First(item => item.Id == notification.Id).IsRead, "Notification should be marked read.");

service.DeleteProduct(merchant.Id, addedProduct.Id);
Assert(service.Products.All(item => item.Id != addedProduct.Id), "Deleted product should leave the active catalog.");

var secondProcessService = new MarketplaceService(dataPath);
Assert(secondProcessService.GetMerchantOrders(merchant.Id).First(item => item.Id == order.Id).Status == OrderStatus.Shipped, "A second app process should see persisted order status.");
Assert(PasswordHasher.Verify(MarketplaceService.DemoPassword, customer.PasswordHash), "PBKDF2 password verification should succeed.");

File.Delete(dataPath);
Console.WriteLine("Core smoke tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

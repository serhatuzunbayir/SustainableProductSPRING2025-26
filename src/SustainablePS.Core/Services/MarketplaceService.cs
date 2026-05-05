using SustainablePS.Core.Models;
using SustainablePS.Core.Security;
using System.Text.Json;

namespace SustainablePS.Core.Services;

public sealed class MarketplaceService
{
    public const string DemoPassword = "demo123";
    public const string DemoCustomerEmail = "customer@sustainable.test";
    public const string DemoMerchantEmail = "merchant@sustainable.test";

    private readonly object _gate = new();
    private readonly CarbonCalculator _carbonCalculator = new();
    private readonly MockPaymentProcessor _paymentProcessor = new();
    private readonly string _dataFilePath;
    private readonly List<UserAccount> _users = [];
    private readonly List<Product> _products = [];
    private readonly List<Order> _orders = [];
    private readonly List<Notification> _notifications = [];
    private readonly Dictionary<Guid, Dictionary<Guid, int>> _carts = [];
    private DateTime _lastLoadedAtUtc;

    public MarketplaceService()
        : this(MarketplaceDataPath.ResolveDefault())
    {
    }

    public MarketplaceService(string dataFilePath)
    {
        _dataFilePath = dataFilePath;

        lock (_gate)
        {
            LoadInitialState();
        }
    }

    public IReadOnlyList<Product> Products
    {
        get
        {
            lock (_gate)
            {
                LoadFromDiskIfChanged();
                return _products
                    .Where(product => product.IsActive)
                    .OrderBy(product => product.Name)
                    .ToList();
            }
        }
    }

    public IReadOnlyList<UserAccount> Users
    {
        get
        {
            lock (_gate)
            {
                LoadFromDiskIfChanged();
                return _users.OrderBy(user => user.FullName).ToList();
            }
        }
    }

    public UserAccount Login(string email, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var normalizedEmail = NormalizeEmail(email);
            var user = _users.SingleOrDefault(candidate => candidate.Email == normalizedEmail);

            if (user is null || !PasswordHasher.Verify(password, user.PasswordHash))
            {
                throw new InvalidOperationException("Invalid email or password.");
            }

            return user;
        }
    }

    public UserAccount Register(string fullName, string email, string password, UserRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var normalizedEmail = NormalizeEmail(email);
            if (_users.Any(user => user.Email == normalizedEmail))
            {
                throw new InvalidOperationException("An account with this email already exists.");
            }

            var account = new UserAccount
            {
                FullName = fullName.Trim(),
                Email = normalizedEmail,
                PasswordHash = PasswordHasher.Hash(password),
                Role = role
            };

            _users.Add(account);
            SaveState();
            return account;
        }
    }

    public UserAccount GetUserByEmail(string email)
    {
        lock (_gate)
        {
            LoadFromDiskIfChanged();
            return _users.Single(user => user.Email == NormalizeEmail(email));
        }
    }

    public Product AddProduct(Guid merchantId, ProductDraft draft)
    {
        EnsureRole(merchantId, UserRole.Merchant);
        draft.Validate();

        lock (_gate)
        {
            LoadFromDiskIfChanged();
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

            _products.Add(product);
            SaveState();
            return product;
        }
    }

    public void UpdateProduct(Guid merchantId, Guid productId, ProductDraft draft)
    {
        EnsureRole(merchantId, UserRole.Merchant);
        draft.Validate();

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var product = FindMerchantProduct(merchantId, productId);
            product.Name = draft.Name.Trim();
            product.Description = draft.Description.Trim();
            product.Category = draft.Category.Trim();
            product.Price = draft.Price;
            product.StockQuantity = draft.StockQuantity;
            product.CarbonKgPerUnit = draft.CarbonKgPerUnit;
            SaveState();
        }
    }

    public void UpdateStock(Guid merchantId, Guid productId, int stockQuantity)
    {
        EnsureRole(merchantId, UserRole.Merchant);

        if (stockQuantity < 0)
        {
            throw new ArgumentException("Stock quantity cannot be negative.", nameof(stockQuantity));
        }

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var product = FindMerchantProduct(merchantId, productId);
            product.StockQuantity = stockQuantity;
            SaveState();
        }
    }

    public void DeleteProduct(Guid merchantId, Guid productId)
    {
        EnsureRole(merchantId, UserRole.Merchant);

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var product = FindMerchantProduct(merchantId, productId);
            product.IsActive = false;
            SaveState();
        }
    }

    public void AddToCart(Guid customerId, Guid productId, int quantity)
    {
        EnsureRole(customerId, UserRole.Customer);

        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        }

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var product = FindActiveProduct(productId);
            var cart = GetMutableCart(customerId);
            var nextQuantity = cart.GetValueOrDefault(productId) + quantity;

            if (nextQuantity > product.StockQuantity)
            {
                throw new InvalidOperationException("There is not enough stock for this product.");
            }

            cart[productId] = nextQuantity;
            SaveState();
        }
    }

    public void SetCartQuantity(Guid customerId, Guid productId, int quantity)
    {
        EnsureRole(customerId, UserRole.Customer);

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var cart = GetMutableCart(customerId);
            if (quantity <= 0)
            {
                cart.Remove(productId);
                SaveState();
                return;
            }

            var product = FindActiveProduct(productId);
            if (quantity > product.StockQuantity)
            {
                throw new InvalidOperationException("There is not enough stock for this product.");
            }

            cart[productId] = quantity;
            SaveState();
        }
    }

    public IReadOnlyList<CartLine> GetCart(Guid customerId)
    {
        EnsureRole(customerId, UserRole.Customer);

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            if (!_carts.TryGetValue(customerId, out var cart))
            {
                return [];
            }

            return cart
                .Select(item => new CartLine(FindActiveProduct(item.Key), item.Value))
                .OrderBy(line => line.Product.Name)
                .ToList();
        }
    }

    public double GetCartCarbon(Guid customerId)
    {
        return _carbonCalculator.CalculateCartCarbon(GetCart(customerId));
    }

    public CheckoutResult Checkout(Guid customerId, PaymentDetails paymentDetails)
    {
        EnsureRole(customerId, UserRole.Customer);

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var lines = GetCart(customerId);
            if (lines.Count == 0)
            {
                return new CheckoutResult(false, "Cart is empty.", null, null);
            }

            foreach (var line in lines)
            {
                if (line.Quantity > line.Product.StockQuantity)
                {
                    return new CheckoutResult(false, $"{line.Product.Name} is out of stock.", null, null);
                }
            }

            var payment = _paymentProcessor.Charge(paymentDetails, lines.Sum(line => line.LineTotal));
            if (!payment.Success)
            {
                return new CheckoutResult(false, payment.Message, null, null);
            }

            var originalStock = _products.ToDictionary(product => product.Id, product => product.StockQuantity);

            try
            {
                foreach (var line in lines)
                {
                    FindActiveProduct(line.Product.Id).StockQuantity -= line.Quantity;
                }

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

                _orders.Add(order);
                _carts.Remove(customerId);
                AddOrderNotifications(order);
                SaveState();

                return new CheckoutResult(true, "Order confirmed.", order, payment.TransactionId);
            }
            catch
            {
                foreach (var product in _products)
                {
                    product.StockQuantity = originalStock[product.Id];
                }

                throw;
            }
        }
    }

    public IReadOnlyList<Order> GetCustomerOrders(Guid customerId)
    {
        EnsureRole(customerId, UserRole.Customer);

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            return _orders
                .Where(order => order.CustomerId == customerId)
                .OrderByDescending(order => order.CreatedAt)
                .ToList();
        }
    }

    public IReadOnlyList<Order> GetMerchantOrders(Guid merchantId)
    {
        EnsureRole(merchantId, UserRole.Merchant);

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            return _orders
                .Where(order => order.Items.Any(item => item.MerchantId == merchantId))
                .OrderByDescending(order => order.CreatedAt)
                .ToList();
        }
    }

    public void UpdateOrderStatus(Guid merchantId, Guid orderId, OrderStatus status)
    {
        EnsureRole(merchantId, UserRole.Merchant);

        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var order = _orders.SingleOrDefault(candidate =>
                candidate.Id == orderId &&
                candidate.Items.Any(item => item.MerchantId == merchantId));

            if (order is null)
            {
                throw new InvalidOperationException("Order was not found for this merchant.");
            }

            if (order.Status == status)
            {
                return;
            }

            order.Status = status;
            _notifications.Add(new Notification
            {
                UserId = order.CustomerId,
                Type = NotificationType.ShippingUpdate,
                Title = $"Order {status}",
                Message = $"Your order {order.Id.ToString()[..8]} is now {status}."
            });
            SaveState();
        }
    }

    public CarbonSummary GetCustomerCarbonSummary(Guid customerId)
    {
        return _carbonCalculator.BuildCustomerSummary(GetCustomerOrders(customerId));
    }

    public ImpactReport BuildImpactReport()
    {
        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var purchasedStats = _orders
                .Where(order => order.PaymentStatus == PaymentStatus.Paid)
                .SelectMany(order => order.Items)
                .GroupBy(item => new
                {
                    item.ProductId,
                    item.ProductName,
                    item.CarbonKgPerUnit
                })
                .Select(group =>
                {
                    var category = _products.SingleOrDefault(product => product.Id == group.Key.ProductId)?.Category ?? "Unknown";
                    var unitsPurchased = group.Sum(item => item.Quantity);

                    return new ProductImpactStat(
                        group.Key.ProductId,
                        group.Key.ProductName,
                        category,
                        unitsPurchased,
                        group.Key.CarbonKgPerUnit,
                        Math.Round(group.Sum(item => item.LineCarbonKg), 2));
                })
                .ToList();

            var catalogStats = _products
                .Where(product => product.IsActive)
                .Select(product => new ProductImpactStat(
                    product.Id,
                    product.Name,
                    product.Category,
                    purchasedStats.SingleOrDefault(stat => stat.ProductId == product.Id)?.UnitsPurchased ?? 0,
                    product.CarbonKgPerUnit,
                    product.CarbonKgPerUnit))
                .ToList();

            return new ImpactReport(
                catalogStats.OrderByDescending(stat => stat.CarbonKgPerUnit).Take(5).ToList(),
                catalogStats.OrderBy(stat => stat.CarbonKgPerUnit).Take(5).ToList(),
                purchasedStats.OrderByDescending(stat => stat.UnitsPurchased).Take(5).ToList());
        }
    }

    public IReadOnlyList<Notification> GetNotifications(Guid userId)
    {
        lock (_gate)
        {
            LoadFromDiskIfChanged();
            return _notifications
                .Where(notification => notification.UserId == userId)
                .OrderByDescending(notification => notification.CreatedAt)
                .ToList();
        }
    }

    public void MarkNotificationRead(Guid userId, Guid notificationId)
    {
        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var notification = _notifications.SingleOrDefault(candidate =>
                candidate.UserId == userId &&
                candidate.Id == notificationId);

            if (notification is null)
            {
                return;
            }

            notification.IsRead = true;
            SaveState();
        }
    }

    public void MarkAllNotificationsRead(Guid userId)
    {
        lock (_gate)
        {
            LoadFromDiskIfChanged();
            foreach (var notification in _notifications.Where(candidate => candidate.UserId == userId))
            {
                notification.IsRead = true;
            }

            SaveState();
        }
    }

    private Dictionary<Guid, int> GetMutableCart(Guid customerId)
    {
        if (!_carts.TryGetValue(customerId, out var cart))
        {
            cart = [];
            _carts[customerId] = cart;
        }

        return cart;
    }

    private Product FindActiveProduct(Guid productId)
    {
        return _products.Single(product => product.Id == productId && product.IsActive);
    }

    private Product FindMerchantProduct(Guid merchantId, Guid productId)
    {
        return _products.Single(product => product.Id == productId && product.MerchantId == merchantId);
    }

    private void EnsureRole(Guid userId, UserRole requiredRole)
    {
        lock (_gate)
        {
            LoadFromDiskIfChanged();
            var user = _users.SingleOrDefault(candidate => candidate.Id == userId);
            if (user?.Role != requiredRole)
            {
                throw new InvalidOperationException($"{requiredRole} access is required.");
            }
        }
    }

    private void AddOrderNotifications(Order order)
    {
        _notifications.Add(new Notification
        {
            UserId = order.CustomerId,
            Type = NotificationType.OrderConfirmation,
            Title = "Order confirmed",
            Message = $"Your order {order.Id.ToString()[..8]} was confirmed. Total carbon: {order.TotalCarbonKg:F2} kg CO2e."
        });

        foreach (var merchantId in order.Items.Select(item => item.MerchantId).Distinct())
        {
            _notifications.Add(new Notification
            {
                UserId = merchantId,
                Type = NotificationType.StockAlert,
                Title = "New order received",
                Message = $"A customer purchased {order.Items.Where(item => item.MerchantId == merchantId).Sum(item => item.Quantity)} item(s)."
            });
        }
    }

    private void SeedDemoData()
    {
        var customer = new UserAccount
        {
            FullName = "Demo Customer",
            Email = DemoCustomerEmail,
            PasswordHash = PasswordHasher.Hash(DemoPassword),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.Now.AddDays(-40)
        };

        var merchant = new UserAccount
        {
            FullName = "Green Merchant",
            Email = DemoMerchantEmail,
            PasswordHash = PasswordHasher.Hash(DemoPassword),
            Role = UserRole.Merchant,
            CreatedAt = DateTimeOffset.Now.AddDays(-60)
        };

        _users.AddRange([customer, merchant]);

        var bambooBrush = new Product
        {
            MerchantId = merchant.Id,
            Name = "Bamboo Toothbrush",
            Description = "Compostable handle with plant-based bristles.",
            Category = "Personal Care",
            Price = 4.99m,
            StockQuantity = 42,
            CarbonKgPerUnit = 0.18
        };

        var notebook = new Product
        {
            MerchantId = merchant.Id,
            Name = "Recycled Paper Notebook",
            Description = "A5 notebook made from 100% post-consumer paper.",
            Category = "Stationery",
            Price = 8.50m,
            StockQuantity = 25,
            CarbonKgPerUnit = 0.62
        };

        var tote = new Product
        {
            MerchantId = merchant.Id,
            Name = "Organic Cotton Tote",
            Description = "Reusable bag produced with certified organic cotton.",
            Category = "Bags",
            Price = 14.00m,
            StockQuantity = 18,
            CarbonKgPerUnit = 1.10
        };

        var solarBank = new Product
        {
            MerchantId = merchant.Id,
            Name = "Solar Power Bank",
            Description = "Portable charger with integrated solar panel.",
            Category = "Electronics",
            Price = 39.90m,
            StockQuantity = 9,
            CarbonKgPerUnit = 3.40
        };

        _products.AddRange([bambooBrush, notebook, tote, solarBank]);

        _orders.Add(new Order
        {
            CustomerId = customer.Id,
            TransactionId = "TX-DEMO-001",
            CreatedAt = DateTimeOffset.Now.AddMonths(-1),
            Items =
            [
                new OrderItem
                {
                    ProductId = bambooBrush.Id,
                    MerchantId = merchant.Id,
                    ProductName = bambooBrush.Name,
                    UnitPrice = bambooBrush.Price,
                    Quantity = 2,
                    CarbonKgPerUnit = bambooBrush.CarbonKgPerUnit
                },
                new OrderItem
                {
                    ProductId = notebook.Id,
                    MerchantId = merchant.Id,
                    ProductName = notebook.Name,
                    UnitPrice = notebook.Price,
                    Quantity = 1,
                    CarbonKgPerUnit = notebook.CarbonKgPerUnit
                }
            ]
        });
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private void LoadInitialState()
    {
        if (File.Exists(_dataFilePath))
        {
            LoadStateFromDisk();
            return;
        }

        SeedDemoData();
        SaveState();
    }

    private void LoadFromDiskIfChanged()
    {
        if (!File.Exists(_dataFilePath))
        {
            return;
        }

        var lastWriteAtUtc = File.GetLastWriteTimeUtc(_dataFilePath);
        if (lastWriteAtUtc <= _lastLoadedAtUtc)
        {
            return;
        }

        LoadStateFromDisk();
    }

    private void LoadStateFromDisk()
    {
        var json = File.ReadAllText(_dataFilePath);
        var state = JsonSerializer.Deserialize<PersistedMarketplaceState>(json) ?? new PersistedMarketplaceState();

        _users.Clear();
        _products.Clear();
        _orders.Clear();
        _notifications.Clear();
        _carts.Clear();

        _users.AddRange(state.Users);
        _products.AddRange(state.Products);
        _orders.AddRange(state.Orders);
        _notifications.AddRange(state.Notifications);

        foreach (var cart in state.Carts)
        {
            _carts[cart.Key] = cart.Value;
        }

        _lastLoadedAtUtc = File.GetLastWriteTimeUtc(_dataFilePath);
    }

    private void SaveState()
    {
        var directory = Path.GetDirectoryName(_dataFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var state = new PersistedMarketplaceState
        {
            Users = _users,
            Products = _products,
            Orders = _orders,
            Notifications = _notifications,
            Carts = _carts
        };

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var tempPath = $"{_dataFilePath}.tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _dataFilePath, overwrite: true);
        _lastLoadedAtUtc = File.GetLastWriteTimeUtc(_dataFilePath);
    }

    private sealed class PersistedMarketplaceState
    {
        public List<UserAccount> Users { get; set; } = [];
        public List<Product> Products { get; set; } = [];
        public List<Order> Orders { get; set; } = [];
        public List<Notification> Notifications { get; set; } = [];
        public Dictionary<Guid, Dictionary<Guid, int>> Carts { get; set; } = [];
    }
}

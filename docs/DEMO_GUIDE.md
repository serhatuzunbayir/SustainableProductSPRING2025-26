# Demo Guide

## One-Minute Explanation

SustainablePS is a C# marketplace split into three projects:

- `SustainablePS.Core` contains the business rules.
- `SustainablePS.Web` is the customer-facing Blazor web app.
- `SustainablePS.Maui` is the merchant desktop app.

Both apps use the same core service and the same local data file, so products, orders, stock, and notifications stay consistent during the demo.

## What To Show

1. Start the web app.

```bash
dotnet run --project src/SustainablePS.Web/SustainablePS.Web.csproj --urls http://localhost:5005
```

2. Open `http://localhost:5005`.
3. Log in with `customer@sustainable.test` and `demo123`.
4. Search or filter the product catalog.
5. Add products to cart.
6. Use the `+`, `-`, and `Remove` cart controls.
7. Show cart price and carbon total.
8. Place an order.
9. Show purchase history, carbon dashboard, and notifications.
10. Start the MAUI merchant app.

```bash
dotnet build src/SustainablePS.Maui/SustainablePS.Maui.csproj -f net10.0-maccatalyst -t:Run
```

11. Log in with `merchant@sustainable.test` and `demo123`.
12. Add a product.
13. Edit the product.
14. Update stock with `+` and `-`.
15. Advance an order status.
16. Show merchant notifications.
17. Return to the web app and show that customer notifications/orders reflect the same shared state.

## Requirement Talking Points

- Role-based access: customers use web; merchants use MAUI desktop.
- Product catalog: merchant creates, edits, deletes, categorizes products.
- Stock management: checkout checks stock; merchant adjusts stock.
- Checkout: mock payment creates orders and rolls back on failure.
- Carbon tracking: cart and orders multiply quantity by product carbon value.
- Dashboard: customer sees total, monthly, average, and history data.
- Notifications: order confirmation, merchant order alert, and shipping updates.
- Maintainability: UI projects stay separate from the shared business logic.

## Honest Limitations

- Payment is simulated for course safety.
- Persistence uses a local JSON file for demo speed.
- A production version should use SQLite or a Web API with a database.
- Password hashing uses PBKDF2, but production authentication should use ASP.NET Identity.

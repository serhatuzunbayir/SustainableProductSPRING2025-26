# Sustainable Product Shopping Platform with Carbon Tracking

Course project starter for a .NET MAUI and C# eco-marketplace.

## What Is Built

- `src/SustainablePS.Core`: C# business/domain layer for users, products, stock, carts, checkout, orders, carbon calculations, analytics, and notifications.
- `src/SustainablePS.Maui`: MAUI desktop merchant console for login, product add/edit/delete, stock changes, order status updates, and notifications.
- `src/SustainablePS.Web`: Blazor customer web app for browsing, cart quantity control, checkout, notifications, purchase history, and carbon dashboard.
- `tests/SustainablePS.Core.Tests`: executable smoke tests for checkout, payment rollback, stock changes, order history, carbon totals, merchant product changes, order status, notifications, and persistence.
- `docs/PROJECT_PLAN.md`: team plan, milestones, architecture, and requirements mapping.
- `docs/DEMO_GUIDE.md`: presentation script and talking points for the instructor.

## Demo Accounts

Both demo accounts use password `demo123`.

- Customer: `customer@sustainable.test`
- Merchant: `merchant@sustainable.test`

## Local Setup

This project uses one shared local data file by default:

```bash
~/Library/Application Support/SustainablePS/marketplace-state.json
```

You can override it for testing:

```bash
export SUSTAINABLEPS_DATA_PATH=/tmp/sustainableps-demo.json
```

To reset demo data:

```bash
rm "$HOME/Library/Application Support/SustainablePS/marketplace-state.json"
```

Install the MAUI workload if it is missing:

```bash
sudo dotnet workload install maui
```

The customer web app can run now:

```bash
dotnet run --project src/SustainablePS.Web/SustainablePS.Web.csproj --urls http://localhost:5005
```

After the MAUI workload is installed, run the desktop app with:

```bash
dotnet build src/SustainablePS.Maui/SustainablePS.Maui.csproj -f net10.0-maccatalyst
dotnet run --project src/SustainablePS.Maui/SustainablePS.Maui.csproj -f net10.0-maccatalyst
```

Mac note: .NET 10 Mac Catalyst requires a matching Xcode version. This project has been verified with Xcode `26.4.1`.

The core layer can already be built and tested without MAUI:

```bash
dotnet build tests/SustainablePS.Core.Tests/SustainablePS.Core.Tests.csproj
dotnet run --project tests/SustainablePS.Core.Tests/SustainablePS.Core.Tests.csproj
```

## MVP Scope

The starter uses a local JSON data store and a mock payment processor so the team can demonstrate the full workflow quickly. For the final version, replace the JSON store with SQLite or a Web API backed by a real database, and replace the mock payment gateway with a documented simulation or real provider depending on the course rules.

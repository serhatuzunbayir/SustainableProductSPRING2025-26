# Project Plan

## Goal

Build a .NET MAUI and C# marketplace where merchants manage eco-friendly products and customers purchase them while seeing carbon footprint data.

## Architecture

- `SustainablePS.Maui`: desktop UI layer for merchants.
- `SustainablePS.Web`: Blazor customer web app.
- `SustainablePS.Core`: business layer with domain models and services.
- Current persistence layer: local JSON state file shared by Web and MAUI on the same machine.
- Future persistence layer: SQLite or Web API backend if the course requires multi-device data sharing.

Current starter persists data locally so the workflow can be demonstrated early. The service boundaries are designed so SQLite or a backend API can be added later without rewriting the UI pages.

## Functional Requirements Mapping

| Requirement | Starter Status | Implementation Notes |
| --- | --- | --- |
| User registration and authentication | Implemented for demo | Core supports register/login and roles. Web exposes login/register; MAUI exposes merchant login. |
| Product catalog management | Implemented for demo | Merchant can add, edit, delete, and categorize products in MAUI. |
| Stock management | Implemented for demo | Merchant can update stock; cart and checkout block quantities above stock. |
| Order placement and checkout | Implemented for demo | Blazor customer cart, quantity controls, checkout, order creation, and item totals are implemented. |
| Payment processing | Implemented as simulation | Mock card processor supports success/failure; failed payments do not reduce stock. |
| Order tracking and history | Implemented for demo | Customers see purchase history; merchants see orders and can advance status. |
| Carbon footprint calculator | Implemented | Cart and order carbon totals use per-product carbon values. |
| Carbon footprint dashboard | Implemented for demo | Customer dashboard shows cumulative, average, monthly, and historical carbon totals. |
| Product impact analytics | Core implemented | Core produces highest-carbon, lowest-carbon, and most-purchased product reports. A dedicated UI can be added if required. |
| Notification system | Implemented for demo | Core creates notifications; web and MAUI show notification inboxes and support marking them read. |

## Non-Functional Plan

| Requirement | Plan |
| --- | --- |
| Performance | Keep product queries simple for MVP; add indexed SQLite tables or backend pagination later. |
| Security | Replace demo auth with ASP.NET Identity or secure local auth; keep password hashing; enforce role checks in services. |
| Availability and reliability | Use transaction-like checkout flow; persist orders before final demo if required. |
| Maintainability and architecture | Keep UI, models, and business rules separated; avoid business logic in page code-behind. |
| Data accuracy and compatibility | Centralize carbon calculations in `CarbonCalculator`; write tests for totals and edge cases. |
| Usability | Web is optimized for customer shopping; MAUI is optimized for merchant desktop operations. |

## Milestones

1. Project foundation
   - Create MAUI solution and C# core library.
   - Implement product, cart, order, payment, carbon, and notification models.
   - Add smoke tests for core workflows.

2. Customer MVP
   - Blazor product browsing.
   - Add to cart.
   - Checkout with mock card.
   - Purchase history and carbon dashboard.

3. Merchant MVP
   - MAUI desktop merchant console.
   - Add products.
   - Edit and delete products.
   - Update stock.
   - Update order status.
   - View related orders.
   - View notification inbox.

4. Persistence and polish
   - Local JSON persistence is complete for demo.
   - Replace with SQLite or backend API if the final grading expects a database.
   - Add charts and deeper validation if time allows.

5. Final presentation
   - Prepare demo script.
   - Show customer and merchant workflows.
   - Explain carbon formulas and requirement coverage.

## Suggested Team Split

- Member 1: Blazor customer pages and web navigation.
- Member 2: MAUI merchant pages and stock/order screens.
- Member 3: Core services, checkout, carbon calculations, and tests.
- Member 4: Persistence, documentation, final demo script, and presentation.

## Demo Script

1. Open the Blazor web catalog as the demo customer.
2. Add multiple products to the cart.
3. Show cart total and carbon total.
4. Complete checkout with the demo card.
5. Open dashboard and show updated purchase history and carbon totals.
6. Open the MAUI merchant console.
7. Add or edit a product, adjust stock, and show merchant orders.
8. Advance an order status and show the customer notification.
9. Explain failed card rollback using a card number ending in `0000`.

## Backlog

- Add SQLite persistence.
- Add charts for monthly carbon data.
- Add automated unit tests with xUnit or MSTest if required by the instructor.

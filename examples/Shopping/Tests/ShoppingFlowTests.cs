using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Catalog.Contracts.Dtos;
using Cysharp.Serialization.Json;
using Orders.Contracts.Dtos;

namespace Shopping.Tests;

public class ShoppingFlowTests : IClassFixture<ShoppingApiTestFactory>
{
    private readonly ShoppingApiTestFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new UlidJsonConverter() }
    };

    public ShoppingFlowTests(ShoppingApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CompleteShoppingFlow_WithAllProfilesAndReports_Succeeds()
    {
        var anonymousClient = _factory.CreateClient();

        // ── 1. USER PROFILES & ROLE AUTHORIZATION ────────────────────────────
        // 1.1 Client self-registration (Public)
        var clientRegisterResp = await anonymousClient.PostAsJsonAsync("/api/v1/identity/users/register", new
        {
            Email = "client@shopping.local",
            UserName = "client1",
            Password = "Password123!",
            ProfileName = "Client"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, clientRegisterResp.StatusCode);

        // 1.2 Anonymous/Client cannot register directly as Manager (IsPublic = false)
        var forbiddenRegisterResp = await anonymousClient.PostAsJsonAsync("/api/v1/identity/users/register", new
        {
            Email = "fake_manager@shopping.local",
            UserName = "fakemanager",
            Password = "Password123!",
            ProfileName = "Manager"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRegisterResp.StatusCode);

        // 1.3 System super-user logs in
        var systemToken = await _factory.LoginAsync(anonymousClient, "admin@localhost", "Admin123!");
        var systemClient = _factory.CreateAuthenticatedClient(systemToken);

        // 1.4 System creates a Manager
        var createManagerResp = await systemClient.PostAsJsonAsync("/api/v1/identity/users/managers", new
        {
            Email = "manager@shopping.local",
            UserName = "manager1",
            Password = "Password123!"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, createManagerResp.StatusCode);

        // 1.5 Manager logs in
        var managerToken = await _factory.LoginAsync(anonymousClient, "manager@shopping.local", "Password123!");
        var managerClient = _factory.CreateAuthenticatedClient(managerToken);

        // 1.6 Manager creates an Employee
        var createEmployeeResp = await managerClient.PostAsJsonAsync("/api/v1/identity/users/employees", new
        {
            Email = "employee@shopping.local",
            UserName = "employee1",
            Password = "Password123!"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, createEmployeeResp.StatusCode);

        // 1.7 Employee logs in
        var employeeToken = await _factory.LoginAsync(anonymousClient, "employee@shopping.local", "Password123!");
        var employeeClient = _factory.CreateAuthenticatedClient(employeeToken);

        // 1.8 Client logs in
        var clientToken = await _factory.LoginAsync(anonymousClient, "client@shopping.local", "Password123!");
        var clientClient = _factory.CreateAuthenticatedClient(clientToken);

        // 1.9 Manager cannot create another Manager (Only System can create managers)
        var managerCreateManagerResp = await managerClient.PostAsJsonAsync("/api/v1/identity/users/managers", new
        {
            Email = "manager2@shopping.local",
            UserName = "manager2",
            Password = "Password123!"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, managerCreateManagerResp.StatusCode);

        // ── 2. CATALOG & STOCK MANAGEMENT ────────────────────────────────────
        // 2.1 Client/Employee cannot create products
        var clientCreateProductResp = await clientClient.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            Name = "Hacker Device",
            Description = "Unauthorized",
            Sku = "HACK-01",
            Price = 999m,
            StockQuantity = 10,
            LowStockThreshold = 2
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, clientCreateProductResp.StatusCode);

        var employeeCreateProductResp = await employeeClient.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            Name = "Hacker Device",
            Description = "Unauthorized",
            Sku = "HACK-02",
            Price = 999m,
            StockQuantity = 10,
            LowStockThreshold = 2
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, employeeCreateProductResp.StatusCode);

        // 2.2 Manager creates products
        var laptopResp = await managerClient.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            Name = "Gaming Laptop",
            Description = "High-end gaming laptop with RTX 4080",
            Sku = "LAP-001",
            Price = 1500.00m,
            StockQuantity = 5,
            LowStockThreshold = 2
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, laptopResp.StatusCode);
        var laptop = (await laptopResp.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions))!;

        var mouseResp = await managerClient.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            Name = "Wireless Mouse",
            Description = "Ergonomic wireless optical mouse",
            Sku = "MOU-001",
            Price = 50.00m,
            StockQuantity = 2, // Low stock (<= threshold 5)
            LowStockThreshold = 5
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, mouseResp.StatusCode);
        var mouse = (await mouseResp.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions))!;

        var keyboardResp = await managerClient.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            Name = "Mechanical Keyboard",
            Description = "RGB mechanical keyboard",
            Sku = "KEY-001",
            Price = 100.00m,
            StockQuantity = 10,
            LowStockThreshold = 3
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Created, keyboardResp.StatusCode);
        var keyboard = (await keyboardResp.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions))!;

        // 2.3 Public can browse products
        var browseResp = await anonymousClient.GetAsync("/api/v1/catalog/products");
        Assert.Equal(HttpStatusCode.OK, browseResp.StatusCode);
        var products = (await browseResp.Content.ReadFromJsonAsync<List<ProductDto>>(_jsonOptions))!;
        Assert.Contains(products, p => p.Sku == "LAP-001");
        Assert.Contains(products, p => p.Sku == "MOU-001");
        Assert.Contains(products, p => p.Sku == "KEY-001");

        // 2.4 Employee queries low-stock products -> sees Wireless Mouse (Stock: 2 <= 5)
        var lowStockResp = await employeeClient.GetAsync("/api/v1/catalog/products/low-stock");
        Assert.Equal(HttpStatusCode.OK, lowStockResp.StatusCode);
        var lowStockProducts = (await lowStockResp.Content.ReadFromJsonAsync<List<ProductDto>>(_jsonOptions))!;
        Assert.Contains(lowStockProducts, p => p.Sku == "MOU-001");

        // 2.5 Employee creates restock request for Wireless Mouse (+10 units)
        var restockRequestResp = await employeeClient.PostAsJsonAsync("/api/v1/catalog/restock-requests", new
        {
            ProductId = mouse.Id.ToString(),
            Quantity = 10,
            Reason = "Store stock is running critically low"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.OK, restockRequestResp.StatusCode);
        var restockRequest = (await restockRequestResp.Content.ReadFromJsonAsync<RestockRequestDto>(_jsonOptions))!;

        // 2.6 Employee fulfills restock request -> Wireless Mouse stock becomes 12 (2 + 10)
        var fulfillResp = await employeeClient.PostAsync($"/api/v1/catalog/restock-requests/{restockRequest.Id}/fulfill", null);
        Assert.Equal(HttpStatusCode.OK, fulfillResp.StatusCode);

        var updatedMouseResp = await anonymousClient.GetAsync($"/api/v1/catalog/products/{mouse.Id}");
        var updatedMouse = (await updatedMouseResp.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions))!;
        Assert.Equal(12, updatedMouse.StockQuantity);

        // 2.7 Manager soft-deletes Mechanical Keyboard -> no longer in active catalog
        var deleteResp = await managerClient.DeleteAsync($"/api/v1/catalog/products/{keyboard.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var browseAfterDeleteResp = await anonymousClient.GetAsync("/api/v1/catalog/products");
        var productsAfterDelete = (await browseAfterDeleteResp.Content.ReadFromJsonAsync<List<ProductDto>>(_jsonOptions))!;
        Assert.DoesNotContain(productsAfterDelete, p => p.Sku == "KEY-001");

        // ── 3. CLIENT CART OPERATIONS ────────────────────────────────────────
        // 3.1 Client adds 2 Laptops to cart
        var addLaptopResp = await clientClient.PostAsJsonAsync("/api/v1/orders/cart/items", new
        {
            ProductId = laptop.Id.ToString(),
            Quantity = 2
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.OK, addLaptopResp.StatusCode);
        var cart1 = (await addLaptopResp.Content.ReadFromJsonAsync<CartDto>(_jsonOptions))!;
        Assert.Equal(2, cart1.TotalItems);
        Assert.Equal(3000.00m, cart1.TotalPrice);

        // 3.2 Client adds 3 Mice to cart
        var addMouseResp = await clientClient.PostAsJsonAsync("/api/v1/orders/cart/items", new
        {
            ProductId = mouse.Id.ToString(),
            Quantity = 3
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.OK, addMouseResp.StatusCode);
        var cart2 = (await addMouseResp.Content.ReadFromJsonAsync<CartDto>(_jsonOptions))!;
        Assert.Equal(5, cart2.TotalItems);
        Assert.Equal(3150.00m, cart2.TotalPrice);

        // 3.3 Client updates Mouse quantity to 2
        var updateMouseResp = await clientClient.PutAsJsonAsync($"/api/v1/orders/cart/items/{mouse.Id}", new
        {
            Quantity = 2
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.OK, updateMouseResp.StatusCode);
        var cart3 = (await updateMouseResp.Content.ReadFromJsonAsync<CartDto>(_jsonOptions))!;
        Assert.Equal(4, cart3.TotalItems); // 2 laptops + 2 mice
        Assert.Equal(3100.00m, cart3.TotalPrice); // (2 * 1500) + (2 * 50)

        // 3.4 Client views cart
        var getCartResp = await clientClient.GetAsync("/api/v1/orders/cart");
        Assert.Equal(HttpStatusCode.OK, getCartResp.StatusCode);
        var activeCart = (await getCartResp.Content.ReadFromJsonAsync<CartDto>(_jsonOptions))!;
        Assert.Equal(2, activeCart.Items.Count);

        // ── 4. CHECKOUT & STOCK DEDUCTION ────────────────────────────────────
        // 4.1 Client checks out
        var checkoutResp = await clientClient.PostAsync("/api/v1/orders/checkout", null);
        Assert.Equal(HttpStatusCode.Created, checkoutResp.StatusCode);
        var order1 = (await checkoutResp.Content.ReadFromJsonAsync<OrderDto>(_jsonOptions))!;
        Assert.Equal("Paid", order1.Status);
        Assert.Equal(3100.00m, order1.TotalAmount);
        Assert.NotNull(order1.OrderNumber);

        // 4.2 Cart is now empty
        var emptyCartResp = await clientClient.GetAsync("/api/v1/orders/cart");
        var emptyCart = (await emptyCartResp.Content.ReadFromJsonAsync<CartDto>(_jsonOptions))!;
        Assert.Empty(emptyCart.Items);

        // 4.3 Catalog stock has been decremented
        var laptopAfterOrderResp = await anonymousClient.GetAsync($"/api/v1/catalog/products/{laptop.Id}");
        var laptopAfterOrder = (await laptopAfterOrderResp.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions))!;
        Assert.Equal(3, laptopAfterOrder.StockQuantity); // 5 - 2 = 3

        var mouseAfterOrderResp = await anonymousClient.GetAsync($"/api/v1/catalog/products/{mouse.Id}");
        var mouseAfterOrder = (await mouseAfterOrderResp.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions))!;
        Assert.Equal(10, mouseAfterOrder.StockQuantity); // 12 - 2 = 10

        // 4.4 Client views own orders
        var myOrdersResp = await clientClient.GetAsync("/api/v1/orders/my-orders");
        Assert.Equal(HttpStatusCode.OK, myOrdersResp.StatusCode);
        var myOrders = (await myOrdersResp.Content.ReadFromJsonAsync<List<OrderDto>>(_jsonOptions))!;
        Assert.Contains(myOrders, o => o.Id == order1.Id);

        // ── 5. EMPLOYEE FULFILLMENT (MARK SENT & DELIVERED) ───────────────────
        // 5.1 Employee views all orders
        var allOrdersResp = await employeeClient.GetAsync("/api/v1/orders");
        Assert.Equal(HttpStatusCode.OK, allOrdersResp.StatusCode);
        var allOrders = (await allOrdersResp.Content.ReadFromJsonAsync<List<OrderDto>>(_jsonOptions))!;
        Assert.Contains(allOrders, o => o.Id == order1.Id);

        // 5.2 Employee marks order as Sent
        var markSentResp = await employeeClient.PostAsync($"/api/v1/orders/{order1.Id}/send", null);
        Assert.Equal(HttpStatusCode.OK, markSentResp.StatusCode);
        var sentOrder = (await markSentResp.Content.ReadFromJsonAsync<OrderDto>(_jsonOptions))!;
        Assert.Equal("Sent", sentOrder.Status);
        Assert.NotNull(sentOrder.SentAt);

        // 5.3 Employee marks order as Delivered
        var markDeliveredResp = await employeeClient.PostAsync($"/api/v1/orders/{order1.Id}/deliver", null);
        Assert.Equal(HttpStatusCode.OK, markDeliveredResp.StatusCode);
        var deliveredOrder = (await markDeliveredResp.Content.ReadFromJsonAsync<OrderDto>(_jsonOptions))!;
        Assert.Equal("Delivered", deliveredOrder.Status);
        Assert.NotNull(deliveredOrder.DeliveredAt);

        // ── 6. ORDER CANCELLATION & STOCK RESTORATION ─────────────────────────
        // 6.1 Client places 2nd order for 1 Laptop (Stock is currently 3)
        await clientClient.PostAsJsonAsync("/api/v1/orders/cart/items", new
        {
            ProductId = laptop.Id.ToString(),
            Quantity = 1
        }, _jsonOptions);
        var checkout2Resp = await clientClient.PostAsync("/api/v1/orders/checkout", null);
        Assert.Equal(HttpStatusCode.Created, checkout2Resp.StatusCode);
        var order2 = (await checkout2Resp.Content.ReadFromJsonAsync<OrderDto>(_jsonOptions))!;

        // Stock is now 2
        var laptopStockBeforeCancelResp = await anonymousClient.GetAsync($"/api/v1/catalog/products/{laptop.Id}");
        var laptopStockBeforeCancel = (await laptopStockBeforeCancelResp.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions))!;
        Assert.Equal(2, laptopStockBeforeCancel.StockQuantity);

        // 6.2 Employee and Client cannot cancel orders (Only Manager & System can cancel)
        var clientCancelResp = await clientClient.PostAsJsonAsync($"/api/v1/orders/{order2.Id}/cancel", new
        {
            Reason = "I changed my mind"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, clientCancelResp.StatusCode);

        var employeeCancelResp = await employeeClient.PostAsJsonAsync($"/api/v1/orders/{order2.Id}/cancel", new
        {
            Reason = "Employee trying to cancel"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, employeeCancelResp.StatusCode);

        // 6.3 Manager cancels order 2
        var managerCancelResp = await managerClient.PostAsJsonAsync($"/api/v1/orders/{order2.Id}/cancel", new
        {
            Reason = "Customer requested cancellation prior to shipping"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.OK, managerCancelResp.StatusCode);
        var cancelledOrder = (await managerCancelResp.Content.ReadFromJsonAsync<OrderDto>(_jsonOptions))!;
        Assert.Equal("Cancelled", cancelledOrder.Status);
        Assert.NotNull(cancelledOrder.CancelledAt);
        Assert.Equal("Customer requested cancellation prior to shipping", cancelledOrder.CancellationReason);

        // 6.4 Stock has been restored from 2 back to 3
        var laptopStockAfterCancelResp = await anonymousClient.GetAsync($"/api/v1/catalog/products/{laptop.Id}");
        var laptopStockAfterCancel = (await laptopStockAfterCancelResp.Content.ReadFromJsonAsync<ProductDto>(_jsonOptions))!;
        Assert.Equal(3, laptopStockAfterCancel.StockQuantity);

        // ── 7. REPORTS (PRODUCTS SOLD & REVENUE) ──────────────────────────────
        // 7.1 Client and Employee cannot access reports
        var clientReportResp = await clientClient.GetAsync("/api/v1/orders/reports/products-sold");
        Assert.Equal(HttpStatusCode.Forbidden, clientReportResp.StatusCode);

        var employeeReportResp = await employeeClient.GetAsync("/api/v1/orders/reports/revenue");
        Assert.Equal(HttpStatusCode.Forbidden, employeeReportResp.StatusCode);

        // 7.2 Manager requests Products Sold Report
        var productsSoldReportResp = await managerClient.GetAsync("/api/v1/orders/reports/products-sold");
        Assert.Equal(HttpStatusCode.OK, productsSoldReportResp.StatusCode);
        var productsSoldReport = (await productsSoldReportResp.Content.ReadFromJsonAsync<ProductsSoldReportDto>(_jsonOptions))!;

        // Order 1 was 2 Laptops ($3000) and 2 Mice ($100) = 4 units, $3100.
        // Order 2 was cancelled and must NOT be included in sales report.
        Assert.Equal(4, productsSoldReport.TotalUnitsSold);
        Assert.Equal(3100.00m, productsSoldReport.TotalSalesAmount);
        Assert.Equal(2, productsSoldReport.UniqueProductsCount);

        var laptopSales = productsSoldReport.Items.FirstOrDefault(i => i.ProductId == laptop.Id);
        Assert.NotNull(laptopSales);
        Assert.Equal(2, laptopSales.UnitsSold);
        Assert.Equal(3000.00m, laptopSales.TotalRevenue);

        var mouseSales = productsSoldReport.Items.FirstOrDefault(i => i.ProductId == mouse.Id);
        Assert.NotNull(mouseSales);
        Assert.Equal(2, mouseSales.UnitsSold);
        Assert.Equal(100.00m, mouseSales.TotalRevenue);

        // 7.3 Manager requests Revenue Report
        var revenueReportResp = await managerClient.GetAsync("/api/v1/orders/reports/revenue?groupBy=total");
        Assert.Equal(HttpStatusCode.OK, revenueReportResp.StatusCode);
        var revenueReport = (await revenueReportResp.Content.ReadFromJsonAsync<RevenueReportDto>(_jsonOptions))!;

        Assert.Equal(3100.00m, revenueReport.TotalRevenue);
        Assert.Equal(1, revenueReport.TotalOrders); // Only order 1, since order 2 is cancelled
        Assert.Equal(3100.00m, revenueReport.AverageOrderValue);
        Assert.NotEmpty(revenueReport.Breakdowns);

        // 7.4 System super-user can also access reports
        var systemReportResp = await systemClient.GetAsync("/api/v1/orders/reports/revenue");
        Assert.Equal(HttpStatusCode.OK, systemReportResp.StatusCode);

        // ── 8. EDGE CASES & BUSINESS RULE VALIDATIONS ────────────────────────
        // 8.1 Cannot cancel an already delivered order (Order 1 was marked Delivered in step 5.3)
        var cancelDeliveredResp = await managerClient.PostAsJsonAsync($"/api/v1/orders/{order1.Id}/cancel", new
        {
            Reason = "Attempt to cancel delivered order"
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, cancelDeliveredResp.StatusCode);

        // 8.2 Cannot mark a cancelled order as sent or delivered (Order 2 was cancelled in step 6.3)
        var sendCancelledResp = await employeeClient.PostAsync($"/api/v1/orders/{order2.Id}/send", null);
        Assert.Equal(HttpStatusCode.BadRequest, sendCancelledResp.StatusCode);

        var deliverCancelledResp = await employeeClient.PostAsync($"/api/v1/orders/{order2.Id}/deliver", null);
        Assert.Equal(HttpStatusCode.BadRequest, deliverCancelledResp.StatusCode);

        // 8.3 Date range filtering on reports (future date range returns 0 sales)
        var futureFrom = DateTime.UtcNow.AddDays(10).ToString("o");
        var futureTo = DateTime.UtcNow.AddDays(20).ToString("o");
        var emptyReportResp = await managerClient.GetAsync($"/api/v1/orders/reports/products-sold?from={futureFrom}&to={futureTo}");
        Assert.Equal(HttpStatusCode.OK, emptyReportResp.StatusCode);
        var emptyReport = (await emptyReportResp.Content.ReadFromJsonAsync<ProductsSoldReportDto>(_jsonOptions))!;
        Assert.Equal(0, emptyReport.TotalUnitsSold);
        Assert.Equal(0m, emptyReport.TotalSalesAmount);
        Assert.Empty(emptyReport.Items);

        // 8.4 Adding more quantity than available stock fails
        var exceedStockResp = await clientClient.PostAsJsonAsync("/api/v1/orders/cart/items", new
        {
            ProductId = laptop.Id.ToString(),
            Quantity = 999
        }, _jsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, exceedStockResp.StatusCode);
    }
}

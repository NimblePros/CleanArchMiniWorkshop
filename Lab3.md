# Lab 3: Implement Place Order Feature (Vertical Slice)

**Time Estimate:** 40 minutes

## Objectives

By the end of this lab, you will:
- Create complex aggregates with business rules
- Implement a command with validation
- Work with entity relationships in EF Core
- Handle transactional operations
- Write unit tests for business logic
- Build a POST API endpoint

---

## The Feature: Place Order

In this lab, we'll implement the complete **"Place Order"** feature. This is more complex than listing items because it involves:

- Multiple related entities (Order, OrderItem)
- Business rule validation
- Transactional operations (must succeed or fail as a unit)
- Inventory management
- Data consistency

---

## Step 3.1: Analyze the Legacy Code

First, let's see what we're replacing.

1. **Open** `legacy/TightlyCoupled.WebShop/Controllers/ShoppingCartController.cs`

Look at the `Checkout` method. You'll find:
- 1000+ lines of mixed concerns
- Direct database access
- File system operations
- Email sending
- Hard-coded business rules
- No abstraction layers

2. **Open** `legacy/TightlyCoupled.WebShop/Models/Order.cs`

Note the properties we'll need:
- UserId
- CustomerAddress
- ShippingOption
- PaymentMethod
- TotalAmount
- OrderItems (collection)

**Our goal:** Implement this cleanly with proper separation and testability!

---

## Step 3.2: Create the Order Aggregate (Core Layer)

Let's build our domain model. In Clean Architecture, related entities are grouped into **Aggregates**.

### Create OrderAggregate Folder

In `CleanArchWebShop.Core`, create folder: `OrderAggregate`

### Create OrderItem Entity

Create `OrderItem.cs`:

```csharp
namespace CleanArchWebShop.Core.OrderAggregate;

public class OrderItem : EntityBase
{
  public int OrderId { get; private set; }
  public int ItemId { get; private set; }
  public string ItemName { get; private set; } = string.Empty;
  public int Quantity { get; private set; }
  public decimal UnitPrice { get; private set; }
  
  public decimal TotalPrice => Quantity * UnitPrice;

  // EF Core requires a parameterless constructor
  private OrderItem() { }

  public OrderItem(int itemId, string itemName, int quantity, decimal unitPrice)
  {
    Guard.Against.NegativeOrZero(itemId, nameof(itemId));
    Guard.Against.NullOrWhiteSpace(itemName, nameof(itemName));
    Guard.Against.NegativeOrZero(quantity, nameof(quantity));
    Guard.Against.Negative(unitPrice, nameof(unitPrice));

    ItemId = itemId;
    ItemName = itemName;
    Quantity = quantity;
    UnitPrice = unitPrice;
  }
}
```

### Create Order Entity

Create `Order.cs`:

```csharp
using Ardalis.GuardClauses;

namespace CleanArchWebShop.Core.OrderAggregate;

public class Order : EntityBase, IAggregateRoot
{
  public string UserId { get; private set; } = string.Empty;
  public string CustomerAddress { get; private set; } = string.Empty;
  public string ShippingOption { get; private set; } = string.Empty;
  public string PaymentMethod { get; private set; } = string.Empty;
  public decimal TotalAmount { get; private set; }
  public DateTime OrderDate { get; private set; }
  public OrderStatus Status { get; private set; }

  private readonly List<OrderItem> _orderItems = new();
  public IReadOnlyCollection<OrderItem> OrderItems => _orderItems.AsReadOnly();

  // EF Core requires a parameterless constructor
  private Order() { }

  public Order(string userId, string customerAddress, string shippingOption, string paymentMethod)
  {
    UserId = Guard.Against.NullOrWhiteSpace(userId, nameof(userId));
    CustomerAddress = Guard.Against.NullOrWhiteSpace(customerAddress, nameof(customerAddress));
    ShippingOption = Guard.Against.NullOrWhiteSpace(shippingOption, nameof(shippingOption));
    PaymentMethod = Guard.Against.NullOrWhiteSpace(paymentMethod, nameof(paymentMethod));
    
    OrderDate = DateTime.UtcNow;
    Status = OrderStatus.Pending;
    TotalAmount = 0;
  }

  public void AddItem(int itemId, string itemName, int quantity, decimal unitPrice)
  {
    // Business Rule: Can't add items to a completed order
    if (Status == OrderStatus.Completed || Status == OrderStatus.Cancelled)
    {
      throw new InvalidOperationException($"Cannot add items to an order with status {Status}");
    }

    // Business Rule: Check if item already exists in order
    var existingItem = _orderItems.FirstOrDefault(i => i.ItemId == itemId);
    if (existingItem != null)
    {
      throw new InvalidOperationException($"Item {itemName} is already in the order");
    }

    var orderItem = new OrderItem(itemId, itemName, quantity, unitPrice);
    _orderItems.Add(orderItem);
    
    RecalculateTotal();
  }

  public void RemoveItem(int itemId)
  {
    // Business Rule: Can't modify completed orders
    if (Status == OrderStatus.Completed || Status == OrderStatus.Cancelled)
    {
      throw new InvalidOperationException($"Cannot remove items from an order with status {Status}");
    }

    var item = _orderItems.FirstOrDefault(i => i.ItemId == itemId);
    if (item == null)
    {
      throw new InvalidOperationException($"Item with id {itemId} not found in order");
    }

    _orderItems.Remove(item);
    RecalculateTotal();
  }

  public void Complete()
  {
    // Business Rule: Order must have items
    if (!_orderItems.Any())
    {
      throw new InvalidOperationException("Cannot complete an order with no items");
    }

    // Business Rule: Order must be pending
    if (Status != OrderStatus.Pending)
    {
      throw new InvalidOperationException($"Cannot complete an order with status {Status}");
    }

    Status = OrderStatus.Completed;
  }

  public void Cancel()
  {
    // Business Rule: Can't cancel completed orders
    if (Status == OrderStatus.Completed)
    {
      throw new InvalidOperationException("Cannot cancel a completed order");
    }

    Status = OrderStatus.Cancelled;
  }

  private void RecalculateTotal()
  {
    TotalAmount = _orderItems.Sum(item => item.TotalPrice);
  }
}
```

### Create OrderStatus Enum

Create `OrderStatus.cs`:

```csharp
namespace CleanArchWebShop.Core.OrderAggregate;

public enum OrderStatus
{
  Pending = 0,
  Completed = 1,
  Cancelled = 2,
  Shipped = 3,
  Delivered = 4
}
```

**Key Points:**
- ✅ Order is an **Aggregate Root** - entry point for the aggregate
- ✅ OrderItem is part of the aggregate - can only be accessed through Order
- ✅ Private setters protect invariants
- ✅ Business rules are enforced in the entity (e.g., can't add items to completed orders)
- ✅ `RecalculateTotal()` keeps total in sync automatically
- ✅ Read-only collection prevents external modification of items

---

## Step 3.3: Create the Place Order Command (UseCases Layer)

Now let's define the application operation.

### Create Command DTOs

In `CleanArchWebShop.UseCases`, create folder: `Orders/PlaceOrder`

Create `PlaceOrderCommand.cs`:

```csharp
using Ardalis.Result;

namespace CleanArchWebShop.UseCases.Orders.PlaceOrder;

public record PlaceOrderCommand(
  string UserId,
  string CustomerAddress,
  string ShippingOption,
  string PaymentMethod,
  List<OrderItemRequest> Items
) : IRequest<Result<int>>;

public record OrderItemRequest(
  int ItemId,
  string ItemName,
  decimal UnitPrice,
  int Quantity
);
```

**Note:** For this simplified lab, we're passing item details directly in the request. In a real application, you would:
- Look up items from a Catalog/Item repository
- Validate stock availability
- Get current prices from the database

**Why return `Result<int>`?**
- `int` is the Order ID
- `Result<T>` handles success/failure explicitly
- No exceptions for business rule violations

### Create the Command Handler

Create `PlaceOrderCommandHandler.cs`:

```csharp
using CleanArchWebShop.Core.OrderAggregate;

namespace CleanArchWebShop.UseCases.Orders.PlaceOrder;

public class PlaceOrderCommandHandler(IRepository<Order> orderRepository)
  : IRequestHandler<PlaceOrderCommand, Result<int>>
{
  public async ValueTask<Result<int>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
  {
    // Validate that we have items
    if (request.Items == null || !request.Items.Any())
    {
      return Result<int>.Error("Order must contain at least one item");
    }

    // Create the order
    var order = new Order(
      request.UserId,
      request.CustomerAddress,
      request.ShippingOption,
      request.PaymentMethod
    );

    // Add items to the order
    foreach (var requestItem in request.Items)
    {
      try
      {
        order.AddItem(
          requestItem.ItemId,
          requestItem.ItemName,
          requestItem.Quantity,
          requestItem.UnitPrice
        );
      }
      catch (InvalidOperationException ex)
      {
        return Result<int>.Error(ex.Message);
      }
    }

    // Complete the order
    try
    {
      order.Complete();
    }
    catch (InvalidOperationException ex)
    {
      return Result<int>.Error(ex.Message);
    }

    // Save the order
    await orderRepository.AddAsync(order, cancellationToken);

    return Result<int>.Success(order.Id);
  }
}
```

**What's happening:**
1. Validate that items are provided
2. Create the order with user details
3. Add each item to the order (business rules enforced in Order entity)
4. Complete the order (validates order has items)
5. Save to repository
6. Return order ID or error

**Simplified Approach:** This lab focuses on the architecture patterns. In production, you would:
- Validate items exist in catalog
- Check stock availability  
- Reduce inventory
- Handle payment processing
- Send confirmation emails

**Discussion:** Notice how business rules are in the Order entity (`AddItem`, `Complete`), while orchestration is in the handler. This separation makes both testable!

---

## Step 3.4: Configure EF Core Relationships (Infrastructure Layer)

Now let's set up the database mapping.

### Update AppDbContext

In `Infrastructure/Data/AppDbContext.cs`, add:

```csharp
public DbSet<Order> Orders => Set<Order>();
```

### Create Order Configuration

Create `Infrastructure/Data/Config/OrderConfiguration.cs`:

```csharp
using CleanArchWebShop.Core.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchWebShop.Infrastructure.Data.Config;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
  public void Configure(EntityTypeBuilder<Order> builder)
  {
    builder.Property(o => o.UserId)
      .IsRequired()
      .HasMaxLength(450);

    builder.Property(o => o.CustomerAddress)
      .IsRequired()
      .HasMaxLength(500);

    builder.Property(o => o.ShippingOption)
      .IsRequired()
      .HasMaxLength(100);

    builder.Property(o => o.PaymentMethod)
      .IsRequired()
      .HasMaxLength(50);

    builder.Property(o => o.TotalAmount)
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(o => o.Status)
      .IsRequired();

    // IMPORTANT: Use OwnsMany for owned entities (not HasMany)
    // OrderItems are owned by Order and should not have independent lifecycle
    builder.OwnsMany(o => o.OrderItems, oi =>
    {
      oi.Property(item => item.ItemName)
        .IsRequired()
        .HasMaxLength(200);

      oi.Property(item => item.Quantity)
        .IsRequired();

      oi.Property(item => item.UnitPrice)
        .HasPrecision(18, 2)
        .IsRequired();

      // TotalPrice is a calculated property, doesn't need to be stored
      oi.Ignore(item => item.TotalPrice);
    });

    // Index for common queries
    builder.HasIndex(o => o.UserId);
    builder.HasIndex(o => o.OrderDate);
  }
}
```

**⚠️ Critical Note:** OrderItems are **owned entities** and must use `OwnsMany`, not `HasMany`. Owned entities:
- Have no independent identity outside their owner
- Cannot be queried directly via DbSet
- Are always loaded with their owner
- Are automatically deleted when the owner is deleted

Using `HasMany` would create a separate table with unnecessary foreign key relationships.

### Create OrderItem Configuration

**Note:** Since we're using `OwnsMany` in OrderConfiguration, we don't need a separate configuration file for OrderItem. The configuration is done inline within the `OwnsMany` call above. This is the recommended approach for owned entities in EF Core.

---

## Step 3.5: Create the API Endpoint (Web Layer)

Let's expose the feature through an API using **FastEndpoints**.

### Create PlaceOrder Endpoint

In `CleanArchWebShop.Web`, create folder: `Cart` (if it doesn't exist from Lab 2)

Create `Cart/PlaceOrder.cs`:

```csharp
using CleanArchWebShop.UseCases.Orders.PlaceOrder;
using FastEndpoints;
using Mediator;

namespace CleanArchWebShop.Web.Cart;

public class PlaceOrder(IMediator mediator) : Endpoint<PlaceOrderCommand>
{
  public override void Configure()
  {
    Post("/orders");
    AllowAnonymous();
    Summary(s =>
    {
      s.Summary = "Place a new order";
      s.Description = "Creates a new order with the provided items";
      s.Response(201, "Order created successfully", example: 123);
      s.Response(400, "Invalid request or business rule violation");
    });
  }

  public override async Task HandleAsync(PlaceOrderCommand req, CancellationToken ct)
  {
    var result = await mediator.Send(req, ct);

    if (result.IsSuccess)
    {
      HttpContext.Response.StatusCode = 201;
      await SendAsync(result.Value, cancellation: ct);
      return;
    }

    await SendErrorsAsync(cancellation: ct);
  }
}
```

**Key Points:**
- `Endpoint<PlaceOrderCommand>` - takes command as request, no explicit response DTO
- `Post("/orders")` - HTTP POST to /orders
- Status 201 for successful creation
- `SendErrorsAsync()` automatically formats validation errors
- Mediator handles the command and returns Result

**FastEndpoints Benefits:**
- Clean, focused endpoint classes
- No controller bloat
- Built-in validation support
- Automatic OpenAPI/Swagger generation
- Easy to test

**API Design:**
- POST creates a resource → Returns 201 Created
- Location header points to the new resource
- Different status codes for different errors
- Returns the order ID on success

---

## Step 3.6: Create and Apply Migration

Let's update the database schema.

```powershell
cd CleanArchWebShop

# Create migration
dotnet ef migrations add AddOrders --project src\CleanArchWebShop.Infrastructure --startup-project src\CleanArchWebShop.Web

# Apply migration
dotnet ef database update --project src\CleanArchWebShop.Infrastructure --startup-project src\CleanArchWebShop.Web
```

**⚠️ Important:** If you need to recreate the database (e.g., after fixing entity configurations):

```powershell
# Delete the database file
Remove-Item src\CleanArchWebShop.Web\CleanArchWebShop.db

# Reapply all migrations
dotnet ef database update --project src\CleanArchWebShop.Infrastructure --startup-project src\CleanArchWebShop.Web
```

---

## Step 3.7: Test the Feature

Time to test placing an order!

### Running the App for Testing

**⚠️ PowerShell Note:** If you need to test API endpoints with curl while the app is running, you have two options:

**Option 1: Use a Background Job** (Recommended for testing)
```powershell
# Start the app in the background
$job = Start-Job -ScriptBlock { Set-Location "C:\path\to\CleanArchWebShop\src\CleanArchWebShop.Web"; dotnet run }

# Wait a moment for it to start
Start-Sleep -Seconds 10

# Now you can run curl commands in the same terminal
curl -k https://localhost:57679/cart/testuser

# When done, stop the job
Stop-Job $job
Remove-Job $job
```

**Option 2: Use Multiple Terminals**
- Terminal 1: Run `dotnet run --project src\CleanArchWebShop.Web`
- Terminal 2: Run your curl commands

### Test Place Order

1. **Place an order** using curl:

```powershell
curl -k -X POST https://localhost:57679/orders `
  -H "Content-Type: application/json" `
  -d '{
    "userId": "testuser",
    "customerAddress": "123 Main St, Springfield, IL 62701",
    "shippingOption": "Standard",
    "paymentMethod": "Credit Card",
    "items": [
      { 
        "itemId": 1, 
        "itemName": "Laptop",
        "unitPrice": 999.99,
        "quantity": 1
      },
      { 
        "itemId": 2,
        "itemName": "Mouse", 
        "unitPrice": 29.99,
        "quantity": 2
      }
    ]
  }'
```

2. **Expected response:**

```
1
```

And status code `201 Created`.

3. **Test error scenarios:**

```powershell
# Order with no items
curl -k -X POST https://localhost:57679/orders `
  -H "Content-Type: application/json" `
  -d '{
    "userId": "testuser",
    "customerAddress": "123 Main St",
    "shippingOption": "Standard",
    "paymentMethod": "Credit Card",
    "items": []
  }'
```

Should return `400 Bad Request` with error message about requiring items.

```powershell
# Order with duplicate items
curl -k -X POST https://localhost:57679/orders `
  -H "Content-Type: application/json" `
  -d '{
    "userId": "testuser",
    "customerAddress": "123 Main St",
    "shippingOption": "Standard",
    "paymentMethod": "Credit Card",
    "items": [
      { "itemId": 1, "itemName": "Laptop", "unitPrice": 999.99, "quantity": 1 },
      { "itemId": 1, "itemName": "Laptop", "unitPrice": 999.99, "quantity": 1 }
    ]
  }'
```

Should return `400 Bad Request` with error about duplicate items.

---

## Step 3.8: Write Unit Tests

Now let's write tests for our business logic. This is where Clean Architecture really shines!

### Unit Test Overview

The template uses:
- **xUnit** - Test framework
- **NSubstitute** - Mocking library
- **Shouldly** - Fluent assertions

### Create Order Entity Tests

In `CleanArchWebShop.UnitTests`, create folder: `Core/OrderAggregate`

Create `Core/OrderAggregate/OrderTests.cs`:

```csharp
using CleanArchWebShop.Core.OrderAggregate;
using Xunit;

namespace CleanArchWebShop.UnitTests.Core.OrderAggregate;

public class OrderTests
{
  [Fact]
  public void Constructor_WithValidData_CreatesOrder()
  {
    // Arrange & Act
    var order = new Order(
      "user123",
      "123 Main St",
      "Standard",
      "Credit Card"
    );

    // Assert
    Assert.Equal("user123", order.UserId);
    Assert.Equal("123 Main St", order.CustomerAddress);
    Assert.Equal(OrderStatus.Pending, order.Status);
    Assert.Equal(0, order.TotalAmount);
  }

  [Fact]
  public void AddItem_ValidItem_AddsItemAndCalculatesTotal()
  {
    // Arrange
    var order = new Order("user123", "123 Main St", "Standard", "Credit Card");

    // Act
    order.AddItem(1, "Laptop", 2, 999.99m);

    // Assert
    Assert.Single(order.OrderItems);
    Assert.Equal(1999.98m, order.TotalAmount);
  }

  [Fact]
  public void AddItem_DuplicateItem_ThrowsException()
  {
    // Arrange
    var order = new Order("user123", "123 Main St", "Standard", "Credit Card");
    order.AddItem(1, "Laptop", 2, 999.99m);

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(
      () => order.AddItem(1, "Laptop", 1, 999.99m)
    );
    Assert.Contains("already in the order", exception.Message);
  }

  [Fact]
  public void Complete_WithItems_CompletesOrder()
  {
    // Arrange
    var order = new Order("user123", "123 Main St", "Standard", "Credit Card");
    order.AddItem(1, "Laptop", 2, 999.99m);

    // Act
    order.Complete();

    // Assert
    Assert.Equal(OrderStatus.Completed, order.Status);
  }

  [Fact]
  public void Complete_WithoutItems_ThrowsException()
  {
    // Arrange
    var order = new Order("user123", "123 Main St", "Standard", "Credit Card");

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(
      () => order.Complete()
    );
    Assert.Contains("no items", exception.Message);
  }

  [Fact]
  public void AddItem_ToCompletedOrder_ThrowsException()
  {
    // Arrange
    var order = new Order("user123", "123 Main St", "Standard", "Credit Card");
    order.AddItem(1, "Laptop", 1, 999.99m);
    order.Complete();

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(
      () => order.AddItem(2, "Mouse", 1, 29.99m)
    );
    Assert.Contains("Cannot add items", exception.Message);
  }

  [Fact]
  public void RemoveItem_ExistingItem_RemovesAndRecalculates()
  {
    // Arrange
    var order = new Order("user123", "123 Main St", "Standard", "Credit Card");
    order.AddItem(1, "Laptop", 2, 999.99m);
    order.AddItem(2, "Mouse", 1, 29.99m);

    // Act
    order.RemoveItem(2);

    // Assert
    Assert.Single(order.OrderItems);
    Assert.Equal(1999.98m, order.TotalAmount);
  }
}
```

### Run the Tests

```powershell
cd CleanArchWebShop.UnitTests
dotnet test
```

All tests should pass! ✅

**Discussion:**
- No mocking needed for domain logic tests
- Fast execution (no database)
- Clear business rule validation
- Easy to add more test cases

---

## Step 3.9: Test the Command Handler

Now let's test the use case with mocked dependencies.

In `CleanArchWebShop.UnitTests`, create folder: `UseCases/Orders`

Create `UseCases/Orders/PlaceOrderCommandHandlerTests.cs`:

```csharp
using CleanArchWebShop.Core.OrderAggregate;
using CleanArchWebShop.UseCases.Orders.PlaceOrder;

namespace CleanArchWebShop.UnitTests.UseCases.Orders;

public class PlaceOrderCommandHandlerTests
{
  private readonly IRepository<Order> _repository = Substitute.For<IRepository<Order>>();
  private readonly PlaceOrderCommandHandler _handler;

  public PlaceOrderCommandHandlerTests()
  {
    // Configure the repository to return the order that was passed in
    // Note: AddAsync returns Task<T>, not Task
    _repository.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
      .Returns(callInfo => Task.FromResult(callInfo.Arg<Order>()));
    
    _handler = new PlaceOrderCommandHandler(_repository);
  }

  [Fact]
  public async Task Handle_ReturnsSuccess_WhenOrderIsValid()
  {
    // Arrange
    var command = new PlaceOrderCommand(
      UserId: "testuser",
      CustomerAddress: "123 Main St",
      ShippingOption: "Standard",
      PaymentMethod: "Credit Card",
      Items: new List<OrderItemRequest>
      {
        new(ItemId: 1, ItemName: "Laptop", UnitPrice: 999.99m, Quantity: 1)
      }
    );

    Order? capturedOrder = null;
    _repository.AddAsync(Arg.Do<Order>(o => capturedOrder = o), Arg.Any<CancellationToken>())
      .Returns(callInfo => Task.FromResult(callInfo.Arg<Order>()));

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    await _repository.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    
    capturedOrder.ShouldNotBeNull();
    capturedOrder.UserId.ShouldBe("testuser");
    capturedOrder.CustomerAddress.ShouldBe("123 Main St");
    capturedOrder.OrderItems.Count.ShouldBe(1);
    capturedOrder.Status.ShouldBe(OrderStatus.Completed);
    capturedOrder.TotalAmount.ShouldBe(999.99m);
  }

  [Fact]
  public async Task Handle_ReturnsError_WhenNoItemsProvided()
  {
    // Arrange
    var command = new PlaceOrderCommand(
      UserId: "testuser",
      CustomerAddress: "123 Main St",
      ShippingOption: "Standard",
      PaymentMethod: "Credit Card",
      Items: new List<OrderItemRequest>()
    );

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.ShouldBeFalse();
    result.Errors.ShouldContain(e => e.Contains("at least one item"));
    await _repository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_CalculatesTotalCorrectly_WithMultipleItems()
  {
    // Arrange
    var command = new PlaceOrderCommand(
      UserId: "testuser",
      CustomerAddress: "123 Main St",
      ShippingOption: "Express",
      PaymentMethod: "PayPal",
      Items: new List<OrderItemRequest>
      {
        new(ItemId: 1, ItemName: "Laptop", UnitPrice: 999.99m, Quantity: 1),
        new(ItemId: 2, ItemName: "Mouse", UnitPrice: 29.99m, Quantity: 2),
        new(ItemId: 3, ItemName: "Keyboard", UnitPrice: 89.99m, Quantity: 1)
      }
    );

    Order? capturedOrder = null;
    _repository.AddAsync(Arg.Do<Order>(o => capturedOrder = o), Arg.Any<CancellationToken>())
      .Returns(callInfo => Task.FromResult(callInfo.Arg<Order>()));

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    capturedOrder.ShouldNotBeNull();
    capturedOrder.OrderItems.Count.ShouldBe(3);
    // 999.99 + (29.99 * 2) + 89.99 = 1149.96
    capturedOrder.TotalAmount.ShouldBe(1149.96m);
  }

  [Fact]
  public async Task Handle_ReturnsError_WhenDuplicateItemsProvided()
  {
    // Arrange
    var command = new PlaceOrderCommand(
      UserId: "testuser",
      CustomerAddress: "123 Main St",
      ShippingOption: "Standard",
      PaymentMethod: "Credit Card",
      Items: new List<OrderItemRequest>
      {
        new(ItemId: 1, ItemName: "Laptop", UnitPrice: 999.99m, Quantity: 1),
        new(ItemId: 1, ItemName: "Laptop", UnitPrice: 999.99m, Quantity: 1) // Duplicate
      }
    );

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.ShouldBeFalse();
    result.Errors.ShouldContain(e => e.Contains("already in the order"));
    await _repository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
  }
}
```

**Key Testing Concepts:**

1. **NSubstitute Mocking:**
   ```csharp
   _repository = Substitute.For<IRepository<Order>>();
   ```
   Creates a mock that you can configure and verify

2. **Return Value Configuration:**
   ```csharp
   _repository.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
     .Returns(callInfo => Task.FromResult(callInfo.Arg<Order>()));
   ```
   Important: `AddAsync` returns `Task<T>`, not `Task`. Use `Task.FromResult()`.

3. **Argument Capture:**
   ```csharp
   Order? capturedOrder = null;
   _repository.AddAsync(Arg.Do<Order>(o => capturedOrder = o), ...)
   ```
   Captures the argument for detailed assertions

4. **Shouldly Assertions:**
   ```csharp
   result.IsSuccess.ShouldBeTrue();
   capturedOrder.TotalAmount.ShouldBe(999.99m);
   ```
   Readable, fluent assertion syntax

5. **Verification:**
   ```csharp
   await _repository.Received(1).AddAsync(...);
   await _repository.DidNotReceive().AddAsync(...);
   ```
   Verify method calls and their frequency

### Run the Tests

```powershell
cd tests\CleanArchWebShop.UnitTests
dotnet test --filter "FullyQualifiedName~PlaceOrderCommandHandlerTests"
```

Expected output:
```
Test summary: total: 4, failed: 0, succeeded: 4, skipped: 0
```

All should pass! ✅

---

## Checkpoint ✅

By now, you should have:
- ✅ Created Order and OrderItem entities with business rules
- ✅ Implemented the PlaceOrderCommand and handler
- ✅ Configured EF Core relationships with OwnsMany
- ✅ Created a FastEndpoint for placing orders
- ✅ Tested the feature end-to-end
- ✅ Written comprehensive unit tests
- ✅ Validated error handling (empty orders, duplicates)

---

## Compare: Legacy vs. Clean

| Aspect | Legacy | Clean Architecture |
|--------|--------|-------------------|
| **Lines in Controller** | 1000+ | ~30 (FastEndpoint) |
| **Business Logic Location** | Controller | Order entity + Handler |
| **Validation** | Scattered, inconsistent | Centralized in entity |
| **Error Handling** | Try-catch, swallowed errors | Result<T> pattern |
| **Stock Management** | Direct SQL, file operations | (Simplified for lab) |
| **Testability** | Impossible without database | Easy with mocks |
| **Transaction Handling** | Manual, error-prone | Repository handles it |
| **Entity Relationships** | Loose, error-prone FKs | Clean owned entities |

---

## Exercise: Implement Get Order Query

Try implementing the missing `GetOrder` endpoint on your own!

**Requirements:**
1. Create `GetOrderQuery` in UseCases
2. Create `GetOrderQueryHandler`
3. Return `OrderDto` with order details
4. Update `OrdersController.GetOrder()` method

<details>
<summary>Hint (click to expand)</summary>

**Files to create:**
- `UseCases/Orders/GetOrder/GetOrderQuery.cs`
- `UseCases/Orders/GetOrder/OrderDto.cs`
- `UseCases/Orders/GetOrder/GetOrderQueryHandler.cs`

**Query structure:**
```csharp
public record GetOrderQuery(int OrderId) : IRequest<Result<OrderDto>>;

public record OrderDto(
  int Id,
  string UserId,
  string CustomerAddress,
  decimal TotalAmount,
  OrderStatus Status,
  DateTime OrderDate,
  List<OrderItemDto> Items
);

public record OrderItemDto(
  string ItemName,
  int Quantity,
  decimal UnitPrice,
  decimal TotalPrice
);
```
</details>

---

## Next Steps

Fantastic work! You've implemented a complete complex feature with:
- Domain-driven design
- Business rule validation
- Transaction handling
- Comprehensive testing

In the next lab, we'll refactor anti-patterns from the legacy code and see how to properly handle cross-cutting concerns.

➡️ **Continue to [Lab 4: Refactor Legacy Code Patterns](Lab4.md)**

---

## Additional Resources

- [Domain-Driven Design Basics](https://enterprisecraftsmanship.com/posts/ddd-aggregate-boundaries/)
- [Guard Clauses Library](https://github.com/ardalis/GuardClauses)
- [Entity Framework Core Relationships](https://docs.microsoft.com/en-us/ef/core/modeling/relationships)

---

*Questions? Review the tests or ask your instructor for guidance.*

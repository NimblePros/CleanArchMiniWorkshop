# Clean Architecture Migration Workshop Lab

## Overview

Welcome to the Clean Architecture Mini-Workshop! In this hands-on lab, you'll learn how to transform a tightly coupled, monolithic ASP.NET Core application into a well-structured Clean Architecture solution.

**Time Estimate:** 2 hours

## Learning Objectives

By the end of this workshop, you will be able to:
- Identify common architectural problems in tightly coupled applications
- Install and use the Ardalis Clean Architecture template
- Migrate domain models to the Core project
- Implement repository patterns and use cases
- Properly separate concerns across architectural layers
- Apply dependency injection and inversion of control principles

## Prerequisites

- .NET 10 SDK installed
- Visual Studio 2022 or VS Code with C# extension
- Basic understanding of ASP.NET Core MVC
- Familiarity with Entity Framework Core

## The Legacy Application

The `legacy/TightlyCoupled.WebShop` application is a deliberately poorly designed e-commerce application that exhibits many anti-patterns:

- **Hard-coded paths and configuration** throughout the codebase
- **Static utility classes** that violate dependency injection principles
- **Mixed concerns** - business logic, data access, and infrastructure all intertwined
- **Direct file system and database access** in controllers
- **No abstractions** - concrete implementations everywhere
- **Poor testability** - tightly coupled to external dependencies
- **Global state** - shared mutable state across the application

Your mission is to migrate this application to Clean Architecture!

---

## Part 1: Setup and Template Installation (15 minutes)

### Step 1.1: Install the Ardalis Clean Architecture Template

First, let's install the latest Clean Architecture template from NuGet.

1. Open a terminal in the root of the workshop repository
2. Install the template:

```powershell
dotnet new install Ardalis.CleanArchitecture.Template
```

3. Verify the template is installed:

```powershell
dotnet new list | Select-String "clean-arch"
```

You should see the `clean-arch` template listed.

### Step 1.2: Create the New Solution

Now let's create a new Clean Architecture solution in the `src` folder.

1. Navigate to the `src` folder (create it if it doesn't exist):

```powershell
cd src
```

2. Create the new solution using the template:

```powershell
dotnet new clean-arch -n CleanArchWebShop
```

This will create a complete solution structure with:
- **CleanArchWebShop.Core** - Domain entities and interfaces
- **CleanArchWebShop.UseCases** - Application business logic
- **CleanArchWebShop.Infrastructure** - Data access and external services
- **CleanArchWebShop.Web** - ASP.NET Core UI
- Plus test projects for each layer

3. Open the new solution and explore the structure:

```powershell
cd CleanArchWebShop
start CleanArchWebShop.slnx
```

### Step 1.3: Understand the Clean Architecture Layers

Take 5 minutes to explore the generated solution:

- **Core Project**: Contains domain entities, enums, interfaces, and specifications. Has NO dependencies on other projects.
- **UseCases Project**: Contains use cases/commands/queries. Depends only on Core.
- **Infrastructure Project**: Contains EF Core, repositories, and external service implementations. Depends on Core and UseCases.
- **Web Project**: ASP.NET Core MVC/API project. Depends on all other projects.

**Key Principle**: Dependencies point inward. Core has very few dependencies. Infrastructure depends on Core but Core doesn't know about Infrastructure.

---

## Part 2: Migrate Domain Models (20 minutes)

### Step 2.1: Identify Domain Entities

In the legacy application, examine these models in `legacy/TightlyCoupled.WebShop/Models/`:
- `Item.cs` - Product catalog items
- `Order.cs` - Customer orders
- `OrderItem.cs` - Line items in an order
- `CartItem.cs` - Shopping cart items

### Step 2.2: Create Domain Entities in Core

1. In the `CleanArchWebShop.Core` project, create a new folder: `OrderAggregate`

2. Create `Item.cs` in the Core project (in a suitable namespace/folder):

```csharp
namespace CleanArchWebShop.Core.OrderAggregate;

public class Item : EntityBase, IAggregateRoot
{
  public string Name { get; set; } = string.Empty;
  public decimal Price { get; set; }
  public int Quantity { get; set; }
}
```

**Note**: In Clean Architecture, entities should:
- Extend `EntityBase` (provides Id property)
- Implement `IAggregateRoot` for root entities
- Contain only domain logic and properties
- Have NO dependencies on infrastructure

3. Create `Order.cs`:

```csharp
namespace CleanArchWebShop.Core.OrderAggregate;

public class Order : EntityBase, IAggregateRoot
{
  public string UserId { get; set; } = string.Empty;
  public string CustomerAddress { get; set; } = string.Empty;
  public string ShippingOption { get; set; } = string.Empty;
  public string CourierService { get; set; } = string.Empty;
  public bool IsVipOrder { get; set; }
  public string PaymentMethod { get; set; } = string.Empty;
  public decimal TotalAmount { get; private set; }
  public DateTime CreatedDate { get; set; }
  
  private readonly List<OrderItem> _orderItems = new();
  public IReadOnlyCollection<OrderItem> OrderItems => _orderItems.AsReadOnly();

  public void AddOrderItem(OrderItem item)
  {
    _orderItems.Add(item);
    RecalculateTotal();
  }

  private void RecalculateTotal()
  {
    TotalAmount = _orderItems.Sum(i => i.Quantity * i.UnitPrice);
  }
}
```

4. Create `OrderItem.cs`:

```csharp
namespace CleanArchWebShop.Core.OrderAggregate;

public class OrderItem : EntityBase
{
  public int OrderId { get; set; }
  public int ItemId { get; set; }
  public string ItemName { get; set; } = string.Empty;
  public int Quantity { get; set; }
  public decimal UnitPrice { get; set; }
}
```

### Step 2.3: Add Business Logic to Entities

**Discussion Point**: Notice how the `Order` entity encapsulates the logic for calculating totals. In the legacy app, this logic was scattered across controllers and services. In Clean Architecture, domain logic lives in domain entities.

**Exercise**: Add validation logic to the `Item` entity to ensure:
- Price cannot be negative
- Name cannot be empty
- Quantity cannot be negative

<details>
<summary>Solution (click to expand)</summary>

```csharp
public class Item : EntityBase, IAggregateRoot
{
  private string _name = string.Empty;
  private decimal _price;
  private int _quantity;

  public string Name 
  { 
    get => _name;
    set
    {
      if (string.IsNullOrWhiteSpace(value))
        throw new ArgumentException("Item name cannot be empty", nameof(Name));
      _name = value;
    }
  }

  public decimal Price 
  { 
    get => _price;
    set
    {
      if (value < 0)
        throw new ArgumentException("Price cannot be negative", nameof(Price));
      _price = value;
    }
  }

  public int Quantity 
  { 
    get => _quantity;
    set
    {
      if (value < 0)
        throw new ArgumentException("Quantity cannot be negative", nameof(Quantity));
      _quantity = value;
    }
  }
}
```
</details>

---

## Part 3: Implement Repositories and Data Access (25 minutes)

### Step 3.1: Define Repository Interfaces in Core

1. In `CleanArchWebShop.Core/Interfaces`, create `IRepository.cs` (if not already present from template):

The template already provides generic repository interfaces. Review them in the Core project.

2. Create a specific repository interface for Orders:

```csharp
namespace CleanArchWebShop.Core.OrderAggregate;

public interface IOrderRepository : IRepository<Order>
{
  Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId);
  Task<Order?> GetOrderWithItemsAsync(int orderId);
}
```

### Step 3.2: Implement Repository in Infrastructure

1. In `CleanArchWebShop.Infrastructure/Data`, review the `AppDbContext.cs` file

2. Add DbSets for your entities:

```csharp
public DbSet<Item> Items => Set<Item>();
public DbSet<Order> Orders => Set<Order>();
public DbSet<OrderItem> OrderItems => Set<OrderItem>();
```

3. Configure entity relationships in `AppDbContext.cs`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
  base.OnModelCreating(modelBuilder);
  
  modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
  
  // Add specific configurations if needed
}
```

4. Create entity configurations. In `Infrastructure/Data/Config`, create `OrderConfiguration.cs`:

```csharp
using CleanArchWebShop.Core.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchWebShop.Infrastructure.Data.Config;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
  public void Configure(EntityTypeBuilder<Order> builder)
  {
    builder.Property(o => o.CustomerAddress)
      .HasMaxLength(500)
      .IsRequired();

    builder.Property(o => o.TotalAmount)
      .HasPrecision(18, 2);

    builder.HasMany(o => o.OrderItems)
      .WithOne()
      .HasForeignKey(oi => oi.OrderId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
```

5. Create repository implementation in `Infrastructure/Data`:

```csharp
using CleanArchWebShop.Core.OrderAggregate;
using Microsoft.EntityFrameworkCore;

namespace CleanArchWebShop.Infrastructure.Data;

public class OrderRepository : EfRepository<Order>, IOrderRepository
{
  public OrderRepository(AppDbContext dbContext) : base(dbContext)
  {
  }

  public async Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId)
  {
    return await _dbContext.Orders
      .Where(o => o.UserId == userId)
      .Include(o => o.OrderItems)
      .OrderByDescending(o => o.CreatedDate)
      .ToListAsync();
  }

  public async Task<Order?> GetOrderWithItemsAsync(int orderId)
  {
    return await _dbContext.Orders
      .Include(o => o.OrderItems)
      .FirstOrDefaultAsync(o => o.Id == orderId);
  }
}
```

### Step 3.3: Register Dependencies

In `Infrastructure/InfrastructureServiceExtensions.cs` or `DependencyInjection.cs`, register the repository:

```csharp
services.AddScoped<IOrderRepository, OrderRepository>();
```

---

## Part 4: Implement Use Cases (30 minutes)

### Step 4.1: Create a Command for Order Placement

In Clean Architecture, business operations are implemented as commands or queries (CQRS pattern).

1. In `CleanArchWebShop.UseCases`, create a folder: `Orders/PlaceOrder`

2. Create `PlaceOrderCommand.cs`:

```csharp
using Ardalis.Result;
using MediatR;

namespace CleanArchWebShop.UseCases.Orders.PlaceOrder;

public record PlaceOrderCommand(
  string UserId,
  string CustomerAddress,
  string ShippingOption,
  string PaymentMethod,
  List<OrderItemDto> Items
) : IRequest<Result<int>>;

public record OrderItemDto(
  int ItemId,
  string ItemName,
  int Quantity,
  decimal UnitPrice
);
```

3. Create `PlaceOrderCommandHandler.cs`:

```csharp
using Ardalis.Result;
using CleanArchWebShop.Core.OrderAggregate;
using MediatR;

namespace CleanArchWebShop.UseCases.Orders.PlaceOrder;

public class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Result<int>>
{
  private readonly IOrderRepository _orderRepository;

  public PlaceOrderCommandHandler(IOrderRepository orderRepository)
  {
    _orderRepository = orderRepository;
  }

  public async Task<Result<int>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
  {
    // Create order
    var order = new Order
    {
      UserId = request.UserId,
      CustomerAddress = request.CustomerAddress,
      ShippingOption = request.ShippingOption,
      PaymentMethod = request.PaymentMethod,
      CreatedDate = DateTime.UtcNow
    };

    // Add order items
    foreach (var item in request.Items)
    {
      var orderItem = new OrderItem
      {
        ItemId = item.ItemId,
        ItemName = item.ItemName,
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice
      };
      order.AddOrderItem(orderItem);
    }

    // Save order
    await _orderRepository.AddAsync(order, cancellationToken);

    return Result<int>.Success(order.Id);
  }
}
```

### Step 4.2: Create a Query for Getting User Orders

1. In `CleanArchWebShop.UseCases`, create folder: `Orders/GetUserOrders`

2. Create `GetUserOrdersQuery.cs`:

```csharp
using Ardalis.Result;
using MediatR;

namespace CleanArchWebShop.UseCases.Orders.GetUserOrders;

public record GetUserOrdersQuery(string UserId) : IRequest<Result<List<OrderDto>>>;

public record OrderDto(
  int Id,
  string CustomerAddress,
  decimal TotalAmount,
  DateTime CreatedDate,
  List<OrderItemDto> Items
);

public record OrderItemDto(
  string ItemName,
  int Quantity,
  decimal UnitPrice
);
```

3. Create `GetUserOrdersQueryHandler.cs`:

```csharp
using Ardalis.Result;
using CleanArchWebShop.Core.OrderAggregate;
using MediatR;

namespace CleanArchWebShop.UseCases.Orders.GetUserOrders;

public class GetUserOrdersQueryHandler : IRequestHandler<GetUserOrdersQuery, Result<List<OrderDto>>>
{
  private readonly IOrderRepository _orderRepository;

  public GetUserOrdersQueryHandler(IOrderRepository orderRepository)
  {
    _orderRepository = orderRepository;
  }

  public async Task<Result<List<OrderDto>>> Handle(GetUserOrdersQuery request, CancellationToken cancellationToken)
  {
    var orders = await _orderRepository.GetOrdersByUserIdAsync(request.UserId);

    var orderDtos = orders.Select(o => new OrderDto(
      o.Id,
      o.CustomerAddress,
      o.TotalAmount,
      o.CreatedDate,
      o.OrderItems.Select(oi => new OrderItemDto(
        oi.ItemName,
        oi.Quantity,
        oi.UnitPrice
      )).ToList()
    )).ToList();

    return Result<List<OrderDto>>.Success(orderDtos);
  }
}
```

---

## Part 5: Update the Web Layer (25 minutes)

### Step 5.1: Create API Endpoints

In `CleanArchWebShop.Web`, create `Api/OrdersController.cs`:

```csharp
using CleanArchWebShop.UseCases.Orders.PlaceOrder;
using CleanArchWebShop.UseCases.Orders.GetUserOrders;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchWebShop.Web.Api;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
  private readonly IMediator _mediator;

  public OrdersController(IMediator mediator)
  {
    _mediator = mediator;
  }

  [HttpPost]
  public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderCommand command)
  {
    var result = await _mediator.Send(command);
    
    if (result.IsSuccess)
    {
      return CreatedAtAction(nameof(GetOrder), new { id = result.Value }, result.Value);
    }

    return BadRequest(result.Errors);
  }

  [HttpGet("user/{userId}")]
  public async Task<IActionResult> GetUserOrders(string userId)
  {
    var query = new GetUserOrdersQuery(userId);
    var result = await _mediator.Send(query);

    if (result.IsSuccess)
    {
      return Ok(result.Value);
    }

    return BadRequest(result.Errors);
  }

  [HttpGet("{id}")]
  public async Task<IActionResult> GetOrder(int id)
  {
    // TODO: Implement GetOrderByIdQuery
    return Ok();
  }
}
```

### Step 5.2: Compare with Legacy Controller

**Exercise**: Open `legacy/TightlyCoupled.WebShop/Controllers/ShoppingCartController.cs` and compare it with the new `OrdersController`. Notice:

- **Legacy**: 1000+ lines of code, multiple concerns mixed together
- **Clean**: ~40 lines, single responsibility, delegates to use cases
- **Legacy**: Direct database access, file system operations, email sending all in controller
- **Clean**: Controller only orchestrates, delegates to MediatR handlers
- **Legacy**: Hard-coded dependencies, impossible to test
- **Clean**: Dependency injection, easy to test and mock

---

## Part 6: Eliminate Anti-Patterns (20 minutes)

### Step 6.1: Replace Static Utilities

In the legacy app, `GlobalUtilities.cs` is a static class with hard-coded paths and global state. This violates dependency injection principles.

**Exercise**: Identify what the legacy `GlobalUtilities` class does and create proper abstractions.

1. In `Core/Interfaces`, create `ILoggingService.cs`:

```csharp
namespace CleanArchWebShop.Core.Interfaces;

public interface ILoggingService
{
  void LogError(string message);
  void LogInformation(string message);
}
```

2. In `Infrastructure/Services`, create `FileLoggingService.cs`:

```csharp
namespace CleanArchWebShop.Infrastructure.Services;

public class FileLoggingService : ILoggingService
{
  private readonly string _logDirectory;

  public FileLoggingService(IConfiguration configuration)
  {
    _logDirectory = configuration["Logging:LogDirectory"] ?? "logs";
    Directory.CreateDirectory(_logDirectory);
  }

  public void LogError(string message)
  {
    var logFile = Path.Combine(_logDirectory, $"errors_{DateTime.Now:yyyy-MM-dd}.log");
    File.AppendAllText(logFile, $"[{DateTime.Now}] ERROR: {message}{Environment.NewLine}");
  }

  public void LogInformation(string message)
  {
    var logFile = Path.Combine(_logDirectory, $"info_{DateTime.Now:yyyy-MM-dd}.log");
    File.AppendAllText(logFile, $"[{DateTime.Now}] INFO: {message}{Environment.NewLine}");
  }
}
```

**Better Approach**: Use the built-in `ILogger<T>` from ASP.NET Core instead of rolling your own!

### Step 6.2: Replace Hard-Coded Configuration

The legacy app has hard-coded SMTP settings, file paths, URLs, etc.

**Best Practice**: Use the Options pattern:

1. In `Core/Interfaces`, create configuration models:

```csharp
namespace CleanArchWebShop.Core.Configuration;

public class EmailSettings
{
  public string SmtpServer { get; set; } = string.Empty;
  public int SmtpPort { get; set; }
  public string Username { get; set; } = string.Empty;
  public string Password { get; set; } = string.Empty;
}
```

2. In `appsettings.json`:

```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.example.com",
    "SmtpPort": 587,
    "Username": "no-reply@example.com",
    "Password": ""
  }
}
```

3. Register in `Program.cs`:

```csharp
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
```

4. Inject using `IOptions<T>`:

```csharp
public class EmailService : IEmailService
{
  private readonly EmailSettings _settings;

  public EmailService(IOptions<EmailSettings> settings)
  {
    _settings = settings.Value;
  }

  // Use _settings.SmtpServer, etc.
}
```

---

## Part 7: Testing (15 minutes)

### Step 7.1: Unit Test a Use Case

One of the biggest benefits of Clean Architecture is testability!

In the test project `CleanArchWebShop.UnitTests`, create `Orders/PlaceOrderCommandHandlerTests.cs`:

```csharp
using CleanArchWebShop.Core.OrderAggregate;
using CleanArchWebShop.UseCases.Orders.PlaceOrder;
using Moq;
using Xunit;

namespace CleanArchWebShop.UnitTests.Orders;

public class PlaceOrderCommandHandlerTests
{
  private readonly Mock<IOrderRepository> _orderRepositoryMock;
  private readonly PlaceOrderCommandHandler _handler;

  public PlaceOrderCommandHandlerTests()
  {
    _orderRepositoryMock = new Mock<IOrderRepository>();
    _handler = new PlaceOrderCommandHandler(_orderRepositoryMock.Object);
  }

  [Fact]
  public async Task Handle_ValidCommand_CreatesOrder()
  {
    // Arrange
    var command = new PlaceOrderCommand(
      UserId: "user123",
      CustomerAddress: "123 Main St",
      ShippingOption: "Standard",
      PaymentMethod: "Credit Card",
      Items: new List<OrderItemDto>
      {
        new(ItemId: 1, ItemName: "Test Item", Quantity: 2, UnitPrice: 10.00m)
      }
    );

    _orderRepositoryMock
      .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Order o, CancellationToken ct) => o);

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    _orderRepositoryMock.Verify(r => r.AddAsync(
      It.Is<Order>(o => o.UserId == "user123" && o.TotalAmount == 20.00m),
      It.IsAny<CancellationToken>()),
      Times.Once);
  }
}
```

### Step 7.2: Compare Testability

**Discussion**: Try to imagine unit testing the legacy `ShoppingCartController`. You would need to:
- Mock file system operations
- Mock direct database connections
- Mock SMTP clients
- Mock HTTP clients for external APIs
- Deal with static methods that can't be mocked

With Clean Architecture:
- Everything is abstracted behind interfaces
- Dependencies are injected
- Easy to mock and test in isolation
- Fast tests with no external dependencies

---

## Part 8: Migration Strategy and Summary (10 minutes)

### Step 8.1: Incremental Migration Strategy

When migrating a real application, you would:

1. **Phase 1**: Install Clean Architecture template alongside legacy code
2. **Phase 2**: Migrate domain models to Core layer
3. **Phase 3**: Create interfaces for external dependencies
4. **Phase 4**: Implement repositories and data access in Infrastructure
5. **Phase 5**: Move business logic to Use Cases (Commands/Queries)
6. **Phase 6**: Update Web layer to use MediatR and new use cases
7. **Phase 7**: Run both systems in parallel, gradually route traffic to new system
8. **Phase 8**: Decommission legacy code

### Step 8.2: Key Principles Recap

| Principle | Legacy Violation | Clean Architecture Solution |
|-----------|-----------------|---------------------------|
| **Dependency Inversion** | Controllers depend on concrete implementations | Controllers depend on abstractions (interfaces) |
| **Single Responsibility** | Controllers have 1000+ lines doing everything | Each class has one reason to change |
| **Open/Closed** | Hard-coded behavior, changes require modifying existing code | Open for extension via interfaces |
| **Separation of Concerns** | Business logic, data access, UI all mixed | Clear layers with distinct responsibilities |
| **Testability** | Impossible to unit test in isolation | Easy to test with mocked dependencies |
| **Configuration** | Hard-coded values throughout | Centralized configuration with Options pattern |

### Step 8.3: What We've Achieved

✅ **Clear Separation**: Core, Use Cases, Infrastructure, and Web layers  
✅ **Dependency Inversion**: Dependencies point inward toward Core  
✅ **Testability**: Easy to unit test business logic in isolation  
✅ **Flexibility**: Can swap out Infrastructure implementations  
✅ **Maintainability**: Each layer has a single, clear responsibility  
✅ **Scalability**: Easy to add new features following established patterns  

---

## Bonus Challenges

If you complete the lab early, try these additional exercises:

### Challenge 1: Implement Specifications Pattern
Create a specification to find orders within a date range using the Specification pattern from the template.

### Challenge 2: Add Validation
Use FluentValidation to validate the `PlaceOrderCommand` before it reaches the handler.

### Challenge 3: Add Integration Tests
Create an integration test that uses an in-memory database to test the entire flow from controller to database.

### Challenge 4: Implement CQRS
Separate read models from write models for better query performance.

### Challenge 5: Add Domain Events
Implement domain events that fire when an order is placed (e.g., send confirmation email, update inventory).

---

## Resources

- [Ardalis Clean Architecture Template](https://github.com/ardalis/CleanArchitecture)
- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [Ardalis Result Pattern](https://github.com/ardalis/Result)
- [Specification Pattern](https://github.com/ardalis/Specification)

---

## Conclusion

Congratulations! You've successfully learned how to:
- Identify architectural problems in legacy code
- Set up a Clean Architecture solution
- Migrate domain models and business logic
- Implement proper separation of concerns
- Apply SOLID principles in practice
- Create a maintainable, testable, and scalable application

Remember: Clean Architecture is not about being "pure" or following rules dogmatically. It's about creating systems that are **maintainable**, **testable**, and **flexible** for changing business needs.

## Next Steps

- Complete the bonus challenges
- Review the [NimblePros resources](https://nimblepros.com) for more training
- Explore the full Ardalis Clean Architecture template features
- Apply these principles to your own projects!

---

*Need help? Ask your instructor or refer to the solution branch in the repository.*

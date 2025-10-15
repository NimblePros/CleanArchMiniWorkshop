# Lab 2: View Cart Feature (Vertical Slice)

**Time Estimate:** 30 minutes

## Objectives

By the end of this lab, you will:
- Implement a complete feature from domain to API
- Create your first domain entity in Core (CartItem)
- Define and implement a repository
- Build a query with Mediator
- Create a FastEndpoint for the API
- Test the feature end-to-end

---

## The Shopping Cart Features

In this workshop, we'll build four core shopping cart features:
1. **View Cart** ← This lab
2. Add Item to Cart (Lab 3)
3. Remove Item from Cart (Lab 3)
4. Checkout (Lab 3)

---

## The Vertical Slice Approach

In this lab, we'll implement the **"View Cart"** feature completely through all layers:

```
User Request → FastEndpoint → Query Handler → Repository → Database
                  ↓               ↓              ↓
                 Web         UseCases      Infrastructure
```

This approach helps you see how data flows through Clean Architecture and how each layer plays its role.

---

## Step 2.1: Understand the Legacy Feature

First, let's look at what we're replacing.

1. **Open** `legacy/TightlyCoupled.WebShop/Models/CartItem.cs`

You'll see a simple entity:
```csharp
public class CartItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
```

2. **Open** `legacy/TightlyCoupled.WebShop/Controllers/ShoppingCartController.cs`

Look for the cart viewing logic. You'll see:
- Direct DbContext usage in the controller
- No separation of concerns
- Mixed business logic and data access

**Our goal:** Implement this cleanly with proper separation!

---

## Step 2.2: Create the CartItem Entity (Core Layer)

Let's start at the **Core** - the heart of our application.

1. In `CleanArchWebShop.Core`, create a new folder: `CartAggregate`

2. Create `CartItem.cs` in that folder:

```csharp
using Ardalis.GuardClauses;

namespace CleanArchWebShop.Core.CartAggregate;

public class CartItem : EntityBase, IAggregateRoot
{
  public string UserId { get; private set; } = string.Empty;
  public int ItemId { get; private set; }
  public string ItemName { get; private set; } = string.Empty;
  public decimal UnitPrice { get; private set; }
  public int Quantity { get; private set; }
  
  public decimal TotalPrice => UnitPrice * Quantity;

  // EF Core requires a parameterless constructor
  private CartItem() { }

  public CartItem(string userId, int itemId, string itemName, decimal unitPrice, int quantity)
  {
    UserId = Guard.Against.NullOrWhiteSpace(userId, nameof(userId));
    ItemId = Guard.Against.NegativeOrZero(itemId, nameof(itemId));
    ItemName = Guard.Against.NullOrWhiteSpace(itemName, nameof(itemName));
    UnitPrice = Guard.Against.Negative(unitPrice, nameof(unitPrice));
    Quantity = Guard.Against.NegativeOrZero(quantity, nameof(quantity));
  }

  public void UpdateQuantity(int newQuantity)
  {
    Quantity = Guard.Against.NegativeOrZero(newQuantity, nameof(newQuantity));
  }

  public void UpdatePrice(decimal newPrice)
  {
    UnitPrice = Guard.Against.Negative(newPrice, nameof(newPrice));
  }
}
```

**Key points:**
- ✅ Extends `EntityBase` (provides `Id` property)
- ✅ Implements `IAggregateRoot` (marks it as a root entity)
- ✅ Uses private setters to protect invariants
- ✅ Has a constructor that enforces business rules
- ✅ Validates data using `Guard` clauses
- ✅ Contains domain logic (e.g., `UpdateQuantity`, `TotalPrice` calculation)
- ✅ Belongs to a specific user (UserId)
- ✅ **NO** infrastructure dependencies

**Discussion:** Compare this to the legacy `CartItem.cs`. What's different? What business rules are now enforced?

---

## Step 2.3: Create Repository Interface (Core Layer)

Still in Core, we need to define how we'll access items.

1. Check if `Core/Interfaces/IRepository.cs` exists (it should from the template)

2. Create `Core/Interfaces/IReadRepository.cs` if it doesn't exist:

```csharp
using Ardalis.Specification;

namespace CleanArchWebShop.Core.Interfaces;

public interface IReadRepository<T> : IReadRepositoryBase<T> where T : class, IAggregateRoot
{
}
```

3. Create `Core/Interfaces/IRepository.cs`:

```csharp
using Ardalis.Specification;

namespace CleanArchWebShop.Core.Interfaces;

public interface IRepository<T> : IRepositoryBase<T> where T : class, IAggregateRoot
{
}
```

These interfaces come from **Ardalis.Specification** package, which provides:
- Generic repository pattern
- Specification pattern for complex queries
- Separation of read and write operations

**Note:** The template should have these already. We're just making sure they're in place!

---

## Step 2.4: Create the View Cart Query (UseCases Layer)

Now we move up to the **UseCases** layer. This is where we define application operations.

1. In `CleanArchWebShop.UseCases`, create folder: `Cart/ViewCart`

2. Create `ViewCartQuery.cs`:

```csharp
using Ardalis.Result;
using Mediator;

namespace CleanArchWebShop.UseCases.Cart.ViewCart;

public record ViewCartQuery(
  string UserId
) : IRequest<Result<CartSummaryDto>>;
```

**What's happening:**
- Using `record` syntax for immutable query objects
- Takes a UserId to get that user's cart
- Returns `Result<T>` for better error handling (no exceptions for expected errors)
- Returns a CartSummaryDto with all cart items and totals

3. Create `CartSummaryDto.cs` in the same folder:

```csharp
namespace CleanArchWebShop.UseCases.Cart.ViewCart;

public record CartSummaryDto(
  string UserId,
  List<CartItemDto> Items,
  decimal SubTotal,
  int TotalItems
);

public record CartItemDto(
  int Id,
  int ItemId,
  string ItemName,
  decimal UnitPrice,
  int Quantity,
  decimal TotalPrice
);
```

**Why DTOs?**
- Domain entities shouldn't be exposed directly to the UI
- DTOs shape data for specific use cases
- Provides calculated totals (SubTotal, TotalItems)
- UI changes don't affect domain model

4. Create `ViewCartQueryHandler.cs`:

```csharp
using Ardalis.Result;
using CleanArchWebShop.Core.CartAggregate;
using CleanArchWebShop.Core.Interfaces;
using Mediator;

namespace CleanArchWebShop.UseCases.Cart.ViewCart;

public class ViewCartQueryHandler(IReadRepository<CartItem> repository)
  : IRequestHandler<ViewCartQuery, Result<CartSummaryDto>>
{
  public async Task<Result<CartSummaryDto>> Handle(
    ViewCartQuery request,
    CancellationToken cancellationToken)
  {
    // Build the query
    var items = await repository.ListAsync(cancellationToken);
    
    // Filter if requested
    if (request.InStockOnly)
    {
      items = items.Where(i => i.IsInStock()).ToList();
    }
    
    // Get all cart items for this user
    var cartItems = await repository.ListAsync(cancellationToken);
    var userCartItems = cartItems.Where(ci => ci.UserId == request.UserId).ToList();
    
    // Map to DTOs
    var cartItemDtos = userCartItems.Select(ci => new CartItemDto(
      ci.Id,
      ci.ItemId,
      ci.ItemName,
      ci.UnitPrice,
      ci.Quantity,
      ci.TotalPrice
    )).ToList();
    
    // Calculate totals
    var subTotal = cartItemDtos.Sum(ci => ci.TotalPrice);
    var totalItems = cartItemDtos.Sum(ci => ci.Quantity);
    
    var cartSummary = new CartSummaryDto(
      request.UserId,
      cartItemDtos,
      subTotal,
      totalItems
    );
    
    return Result<CartSummaryDto>.Success(cartSummary);
  }
}
```

**Discussion Points:**
- Handler depends only on `IReadRepository<CartItem>` (an interface from Core)
- No knowledge of how data is stored (could be SQL, NoSQL, files, etc.)
- Uses primary constructor syntax (C# 12 feature)
- Calculates totals in the handler (presentation logic, not domain logic)
- Returns `Result<T>` for explicit success/failure handling

---

## Step 2.5: Implement the Repository (Infrastructure Layer)

Now we go to the **Infrastructure** layer to implement data access.

1. In `CleanArchWebShop.Infrastructure/Data`, open or update `AppDbContext.cs`:

```csharp
using CleanArchWebShop.Core.CartAggregate;
using Microsoft.EntityFrameworkCore;

namespace CleanArchWebShop.Infrastructure.Data;

public class AppDbContext : DbContext
{
  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
  {
  }

  public DbSet<CartItem> CartItems => Set<CartItem>();
  
  // Other DbSets...

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
  }
}
```

2. Create `Infrastructure/Data/Config/CartItemConfiguration.cs`:

```csharp
using CleanArchWebShop.Core.CartAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchWebShop.Infrastructure.Data.Config;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
  public void Configure(EntityTypeBuilder<CartItem> builder)
  {
    builder.Property(ci => ci.UserId)
      .IsRequired()
      .HasMaxLength(450); // Matches ASP.NET Identity user ID length

    builder.Property(ci => ci.ItemName)
      .IsRequired()
      .HasMaxLength(200);

    builder.Property(ci => ci.UnitPrice)
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(ci => ci.Quantity)
      .IsRequired();

    // TotalPrice is calculated, not stored
    builder.Ignore(ci => ci.TotalPrice);

    // Index for common queries (get cart by user)
    builder.HasIndex(ci => ci.UserId);
  }
}
```

3. Check that `Infrastructure/Data/EfRepository.cs` exists (it should from template)

This generic repository implementation handles CRUD operations for all aggregate roots.

4. Verify `InfrastructureServiceExtensions.cs` registers repositories:

```csharp
services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
```

---

## Step 2.6: Create the FastEndpoint (Web Layer)

Finally, we create the **Web** layer endpoint using **FastEndpoints**.

1. In `CleanArchWebShop.Web`, create folder: `Endpoints/Cart`

2. Create `ViewCart.cs`:

```csharp
using CleanArchWebShop.UseCases.Cart.ViewCart;
using FastEndpoints;
using Mediator;

namespace CleanArchWebShop.Web.Endpoints.Cart;

public class ViewCartEndpoint(IMediator mediator) 
  : Endpoint<ViewCartRequest, CartSummaryDto>
{
  public override void Configure()
  {
    Get("/api/cart/{userId}");
    AllowAnonymous(); // Change to authorized in production
  }

  public override async Task HandleAsync(ViewCartRequest req, CancellationToken ct)
  {
    var query = new ViewCartQuery(req.UserId);
    var result = await mediator.Send(query, ct);

    if (result.IsSuccess)
    {
      await SendAsync(result.Value, cancellation: ct);
    }

    else
    {
      await SendAsync(result.Errors.ToString(), statusCode: 400, cancellation: ct);
    }
  }
}

public record ViewCartRequest
{
  public string UserId { get; init; } = string.Empty;
}
```

**What's happening with FastEndpoints:**
- Endpoint is thin - only orchestration
- Each endpoint is its own class (Single Responsibility Principle)
- Uses Mediator to send the query
- No business logic in the endpoint
- FastEndpoints handles routing automatically
- Request object defines the route parameters
- Type-safe and performant

---

## Step 2.7: Add Sample Data

Let's add some seed data so we have items to list.

1. In `Infrastructure/Data`, create or update `AppDbContextSeed.cs`:

```csharp
using CleanArchWebShop.Core.CartAggregate;
using Microsoft.EntityFrameworkCore;

namespace CleanArchWebShop.Infrastructure.Data;

public static class AppDbContextSeed
{
  public static async Task SeedAsync(AppDbContext context)
  {
    if (await context.CartItems.AnyAsync())
    {
      return; // Already seeded
    }

    // Sample cart items for user "testuser"
    var cartItems = new List<CartItem>
    {
      new CartItem("testuser", 1, "Laptop Computer", 999.99m, 1),
      new CartItem("testuser", 2, "Wireless Mouse", 29.99m, 2),
      new CartItem("testuser", 3, "Mechanical Keyboard", 89.99m, 1),
      
      // Sample cart items for user "user2"
      new CartItem("user2", 4, "Monitor 27 inch", 299.99m, 1),
      new CartItem("user2", 5, "Webcam HD", 79.99m, 1)
    };

    await context.CartItems.AddRangeAsync(cartItems);
    await context.SaveChangesAsync();
  }
}
```

2. Update `Web/Program.cs` to seed data on startup:

```csharp
// After app.UseEndpoints() or at the end of configuration

if (app.Environment.IsDevelopment())
{
  using var scope = app.Services.CreateScope();
  var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
  await context.Database.EnsureCreatedAsync();
  await AppDbContextSeed.SeedAsync(context);
}
```

---

## Step 2.8: Create and Apply Migration

Now let's create a database migration for our CartItem entity.

1. Make sure you're in the `CleanArchWebShop/src/CleanArchWebShop.Web` directory:

```powershell
cd CleanArchWebShop/src/CleanArchWebShop.Web
```

2. Add a migration:

```powershell
dotnet ef migrations add AddCartItems -s CleanArchWebShop.Web.csproj -p ..\CleanArchWebShop.Infrastructure\CleanArchWebShop.Infrastructure.csproj -o Data/Migrations
```

3. Apply the migration:

```powershell
dotnet ef database update -s CleanArchWebShop.Web.csproj -p ..\CleanArchWebShop.Infrastructure\CleanArchWebShop.Infrastructure.csproj
```

**Note:** If you encounter errors, ensure:
- Your connection string is correct in `appsettings.json`
- You have SQL Server LocalDB or SQL Server installed
- The Infrastructure project references the correct EF Core packages

---

## Step 2.9: Test the Feature

Time to see it all work together!

1. Build the solution:

```powershell
dotnet build
```

2. Run the web application:

```powershell
dotnet run --project CleanArchWebShop/src/CleanArchWebShop.Web
```

3. Test the endpoint using your browser or a tool like `curl`:

```powershell
# View cart for testuser
curl https://localhost:5001/api/cart/testuser

# View cart for user2
curl https://localhost:5001/api/cart/user2
```

Or use the FastEndpoints Swagger UI at: `https://localhost:5001/swagger`

4. You should see JSON output with the cart summary!

**Expected response for testuser:**
```json
{
  "userId": "testuser",
  "items": [
    {
      "id": 1,
      "itemId": 1,
      "itemName": "Laptop Computer",
      "unitPrice": 999.99,
      "quantity": 1,
      "totalPrice": 999.99
    },
    {
      "id": 2,
      "itemId": 2,
      "itemName": "Wireless Mouse",
      "unitPrice": 29.99,
      "quantity": 2,
      "totalPrice": 59.98
    },
    {
      "id": 3,
      "itemId": 3,
      "itemName": "Mechanical Keyboard",
      "unitPrice": 89.99,
      "quantity": 1,
      "totalPrice": 89.99
    }
  ],
  "subTotal": 1149.96,
  "totalItems": 4
}
```

---

## Step 2.10: Trace the Data Flow

Let's trace how a request flows through the system:

```
1. HTTP GET → /api/cart/testuser
   └─ FastEndpoints receives request and routes to ViewCartEndpoint
   
2. Endpoint creates ViewCartQuery(userId: "testuser")
   └─ Sends to Mediator
   
3. Mediator finds ViewCartQueryHandler
   └─ Handler receives query
   
4. Handler calls IReadRepository<CartItem>.ListAsync()
   └─ EfRepository (Infrastructure) executes query
   
5. EF Core translates to SQL
   └─ Queries AppDbContext.CartItems WHERE UserId = 'testuser'
   
6. Database returns data
   └─ Entities hydrated as CartItem objects
   
7. Handler maps CartItem → CartItemDto
   └─ Calculates SubTotal and TotalItems
   └─ Creates CartSummaryDto
   
8. Handler returns Result<CartSummaryDto>
   └─ Mediator returns to Endpoint
   
9. Endpoint checks Result.IsSuccess
   └─ Returns HTTP 200 OK with JSON via SendAsync
   
10. Client receives cart summary with items and totals
```

**Key Observations:**
- Each layer has a single responsibility
- FastEndpoints provides clean, focused endpoint classes
- Dependencies point inward (Web → UseCases → Core)
- Easy to test each layer in isolation
- Can swap Infrastructure without changing UseCases or Core
- User-specific data filtered in the handler

---

## Checkpoint ✅

By now, you should have:
- ✅ Created the `CartItem` entity in Core with business logic
- ✅ Defined repository interfaces in Core
- ✅ Created a `ViewCartQuery` and handler in UseCases
- ✅ Configured EF Core mapping in Infrastructure
- ✅ Created a FastEndpoint for viewing cart
- ✅ Seeded sample cart data
- ✅ Tested the complete feature end-to-end

---

## Compare: Legacy vs. Clean

Let's compare what we just built with the legacy approach:

| Aspect | Legacy | Clean Architecture |
|--------|--------|-------------------|
| **Endpoint Structure** | 1000+ line controller with mixed concerns | Focused FastEndpoint class (~30 lines) |
| **Data Access** | DbContext directly in controller | Repository abstraction |
| **Business Rules** | Scattered in controller logic | In CartItem entity |
| **User Filtering** | Manual SQL or LINQ in controller | Clean query in handler |
| **Testability** | Requires database | Mock IRepository |
| **Reusability** | Logic tied to HTTP | Query handler works anywhere |
| **Dependencies** | Controller knows about EF Core | Endpoint knows only about Mediator |
| **Totals Calculation** | Mixed with data access | Clear in handler |

---

## What's Next?

In this lab, you implemented the **View Cart** feature. The remaining cart features are:

2. **Add Item to Cart** - Lab 3
3. **Remove Item from Cart** - Lab 3  
4. **Checkout** - Lab 3

Each will follow the same vertical slice pattern you just learned!

<details>
<summary>Solution (click to expand)</summary>

**Update ListItemsQuery.cs:**
```csharp
public record ListItemsQuery(
  int PageNumber = 1,
  int PageSize = 10,
  bool InStockOnly = false,
  decimal? MinPrice = null,
  decimal? MaxPrice = null
) : IRequest<Result<PagedResult<ItemDto>>>;
```

**Update ListItemsQueryHandler.cs:**
```csharp
// After filtering for InStockOnly
if (request.MinPrice.HasValue)
{
  items = items.Where(i => i.Price >= request.MinPrice.Value).ToList();
}

if (request.MaxPrice.HasValue)
{
  items = items.Where(i => i.Price <= request.MaxPrice.Value).ToList();
}
```

**Update CatalogController.cs:**
```csharp
public async Task<ActionResult<PagedResult<ItemDto>>> ListItems(
  [FromQuery] int pageNumber = 1,
  [FromQuery] int pageSize = 10,
  [FromQuery] bool inStockOnly = false,
  [FromQuery] decimal? minPrice = null,
  [FromQuery] decimal? maxPrice = null)
{
  var query = new ListItemsQuery(pageNumber, pageSize, inStockOnly, minPrice, maxPrice);
  // ... rest of the code
}
```
</details>

---

## Next Steps

Excellent work! You've implemented your first complete vertical slice through Clean Architecture.

In the next lab, we'll tackle a more complex feature with business rules: **placing an order**.

➡️ **Continue to [Lab 3: Implement Place Order Feature](Lab3.md)**

---

## Additional Resources

- [Ardalis Specification Pattern](https://github.com/ardalis/Specification)
- [Result Pattern for Error Handling](https://github.com/ardalis/Result)
- [Mediator Source Generator](https://github.com/martinothamar/Mediator)

---

*Questions? Compare your code with the solution branch or ask your instructor.*

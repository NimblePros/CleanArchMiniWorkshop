# Lab 1: Setup and Template Installation

**Time Estimate:** 15 minutes

## Objectives

By the end of this lab, you will:
- Install the Ardalis Clean Architecture template
- Create a new Clean Architecture solution
- Understand the project structure and dependencies
- Learn the core principles of Clean Architecture

---

## Step 1.1: Install the Ardalis Clean Architecture Template

First, let's install the latest Clean Architecture template from NuGet.

1. Open a terminal in the root of the workshop repository
2. Install the template:

```powershell
dotnet new install Ardalis.CleanArchitecture.Template::11.0.0-beta.1
```

3. Verify the template is installed:

```powershell
dotnet new list | Select-String "clean-arch"
```

You should see the `clean-arch` template listed with output similar to:

```
Ardalis Clean Architecture Solution                clean-arch          [C#]     Web/MVC/CleanArchitecture
```

---

## Step 1.2: Create the New Solution

Now let's create a new Clean Architecture solution from the root of the workshop repository.

1. Make sure you're in the root folder of the workshop:

```powershell
# You should be in /CleanArchMiniWorkshop
# If not, navigate there
```

2. Create the new solution using the template:

```powershell
dotnet new clean-arch -n CleanArchWebShop
```

This command will scaffold a complete solution with multiple projects. The template will create a `CleanArchWebShop` folder with a `src` subfolder containing:
- **CleanArchWebShop.Core** - Domain entities and interfaces
- **CleanArchWebShop.UseCases** - Application business logic
- **CleanArchWebShop.Infrastructure** - Data access and external services
- **CleanArchWebShop.Web** - ASP.NET Core UI/API
- **Test Projects** - Unit and functional test projects

3. Navigate into the solution and open it:

```powershell
cd CleanArchWebShop
start CleanArchWebShop.slnx
```

This will open the solution in Visual Studio. If you're using VS Code, use:

```powershell
code .
```

---

## Step 1.3: Understand the Clean Architecture Layers

Take 5-10 minutes to explore the generated solution. Here's what each project contains:

### 🎯 Core Project (CleanArchWebShop.Core)

**Location:** `src/CleanArchWebShop.Core/`

**Purpose:** The heart of your application - contains domain models and business rules.

**Contains:**
- **Entities** - Domain objects (e.g.  `Contributor.cs`)
- **Interfaces** - Abstractions for repositories and services
- **Enums** - Domain enumerations
- **Specifications** - Query specifications for complex queries
- **Aggregates** - Related entities grouped together
- **Value Objects** - These are strongly typed representations of things that describe entities. They include strongly-typed IDs like `ContributorId` and sometimes larger composite types like `PhoneNumber`. These may use the Vogen tool, which uses source generators.

**Key Characteristic:** ⚠️ Has **very few dependencies**. Core should be independent of frameworks, UI, and data access concerns.

**Example entities you'll find:**
```
CleanArchWebShop.Core/
  ├── ContributorAggregate/
  │   ├── Contributor.cs
  │   └── ContributorStatus.cs
  └── Interfaces/
      ├── IRepository.cs
      └── IEmailSender.cs
```

---

### 🔧 UseCases Project (CleanArchWebShop.UseCases)

**Location:** `src/CleanArchWebShop.UseCases/`

**Purpose:** Application business logic - the "use cases" or "features" of your app.

**Contains:**
- **Commands** - Actions that change state (Create, Update, Delete)
- **Queries** - Actions that retrieve data (List, Get)
- **Handlers** - MediatR handlers that execute commands/queries
- **DTOs** - Data transfer objects for input/output

**Dependencies:** ✅ Depends **only** on Core

**Example structure:**
```
CleanArchWebShop.UseCases/
  ├── Contributors/
  │   ├── Create/
  │   │   ├── CreateContributorCommand.cs
  │   │   └── CreateContributorHandler.cs
  │   ├── List/
  │   │   ├── ListContributorsQuery.cs
  │   │   └── ListContributorsHandler.cs
  │   └── ContributorDto.cs
```

---

### 🗄️ Infrastructure Project (CleanArchWebShop.Infrastructure)

**Location:** `src/CleanArchWebShop.Infrastructure/`

**Purpose:** Implementation of external concerns - databases, file systems, email, etc.

**Contains:**
- **Data** - EF Core DbContext, configurations, repositories
- **Services** - Email services, file storage, external APIs
- **Migrations** - Database migrations

**Dependencies:** Depends on Core and UseCases

**Example structure:**
```
CleanArchWebShop.Infrastructure/
  ├── Data/
  │   ├── AppDbContext.cs
  │   ├── Config/
  │   │   └── ContributorConfiguration.cs
  │   └── EfRepository.cs
  ├── Email/
  │   └── FakeEmailSender.cs
  └── InfrastructureServiceExtensions.cs
```

---

### 🌐 Web Project (CleanArchWebShop.Web)

**Location:** `src/CleanArchWebShop.Web/`

**Purpose:** User interface - API endpoints using FastEndpoints, static files, and Razor pages.

**Contains:**
- **Endpoints** - FastEndpoints for API operations
- **Pages** - Razor pages for server-rendered UI
- **wwwroot** - Static files (HTML, CSS, JS)
- **Program.cs** - Application startup

**Dependencies:** Depends on all other projects (Core, UseCases, Infrastructure)

**Key Technology:** Uses **FastEndpoints** instead of traditional Controllers for a more performant, focused API approach.

**Example structure:**
```
CleanArchWebShop.Web/
  ├── Endpoints/
  │   └── Contributors/
  │       └── List.cs
  ├── Pages/
  ├── wwwroot/
  ├── Program.cs
  └── appsettings.json
```

---

## Step 1.4: The Dependency Rule

**The Golden Rule of Clean Architecture:**

> Dependencies point **INWARD** toward the Core.

```
┌─────────────────────────────────────────┐
│             Web Layer                    │
│  (FastEndpoints, Pages, Static Files)    │
│  Depends on: ALL                         │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│        Infrastructure Layer              │
│  (EF Core, Email, File System)          │
│  Depends on: Core, UseCases              │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│          UseCases Layer                  │
│  (Commands, Queries, Handlers)           │
│  Depends on: Core ONLY                   │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│            Core Layer                    │
│  (Entities, Interfaces, Domain Logic)    │
│  Depends on: VERY FEW EXTERNAL PACKAGES  │
└─────────────────────────────────────────┘
```

**What this means:**
- ✅ Core can be used by all other layers
- ✅ UseCases can use Core
- ✅ Infrastructure can use Core and UseCases
- ✅ Web can use everything
- ❌ Core **CANNOT** reference Infrastructure
- ❌ Core **CANNOT** reference Web
- ❌ UseCases **CANNOT** reference Infrastructure or Web

**Why this matters:**
- Your domain logic (Core) is independent of frameworks
- You can swap out databases without changing business logic
- You can change UI frameworks without touching Core
- Testing is easier - mock only interfaces, not concrete implementations

---

## Step 1.5: Explore the Sample Code

The template includes sample code to demonstrate patterns. Take a few minutes to explore:

1. **Open** `Core/ContributorAggregate/Contributor.cs`
   - Notice it extends `EntityBase` and implements `IAggregateRoot`
   - See how it encapsulates business logic (validation, status changes)

2. **Open** `UseCases/Contributors/List/ListContributorsQuery.cs`
   - Notice the record syntax for immutable queries
   - See how it returns `Result<T>` for better error handling

3. **Open** `Infrastructure/Data/AppDbContext.cs`
   - See how entities are configured
   - Notice the `DbSet<T>` properties

4. **Open** `Web/Endpoints/Contributors/List.cs` (or similar endpoint)
   - See how FastEndpoints defines API endpoints
   - Notice how thin the endpoint is - just orchestration
   - Mediator is used to send commands/queries

---

## Step 1.6: Build and Run

Let's make sure everything works:

1. Build the solution:

```powershell
dotnet build
```

You should see all projects build successfully.

2. Run the Aspire AppHost project:

```powershell
cd CleanArchWebShop/src/CleanArchWebShop.AppHost
dotnet run
```

This will start the .NET Aspire dashboard and orchestrate your application services.

3. The Aspire dashboard will open in your browser (usually at `http://localhost:15888` or similar)

4. From the dashboard, you can:
   - View all running services
   - Access the Web project URL
   - Monitor logs and traces
   - Check resource health

5. Click on the Web project endpoint to explore the sample API at `/api/contributors`

6. Stop the application (Ctrl+C in the terminal)

---

## Step 1.7: Understanding FastEndpoints and Mediator

The template uses **FastEndpoints** for HTTP endpoints and **Mediator** (with SourceGenerator) for implementing the mediator pattern.

### FastEndpoints: Modern API Development

**Traditional Controller approach:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class ContributorsController : ControllerBase
{
    private readonly IContributorService _service;
    private readonly ILogger<ContributorsController> _logger;
    private readonly IMapper _mapper;
    // ... more dependencies
    
    [HttpGet]
    public async Task<IActionResult> GetAll() { /* ... */ }
    
    [HttpPost]
    public async Task<IActionResult> Create() { /* ... */ }
}
```

**FastEndpoints approach:**
```csharp
// Each endpoint is a separate class - Single Responsibility!
public class ListContributorsEndpoint : Endpoint<ListContributorsRequest, ListContributorsResponse>
{
    private readonly IMediator _mediator;
    
    public override void Configure()
    {
        Get("/api/contributors");
        AllowAnonymous();
    }
    
    public override async Task HandleAsync(ListContributorsRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new ListContributorsQuery(), ct);
        await SendAsync(result, cancellation: ct);
    }
}
```

### Benefits of FastEndpoints + Mediator

**FastEndpoints:**
- Each endpoint is a separate class (true Single Responsibility)
- No controller bloat - no massive controller files
- Better performance (no reflection overhead like Controllers)
- Built-in request/response validation
- Cleaner routing and easier testing

**Mediator:**
- Endpoints stay thin - just orchestration
- Easy to add new features without modifying existing code
- Better separation of concerns
- Easier to test
- **Source generation** for improved performance and reduced reflection

**Why Mediator.SourceGenerator?**
- Compile-time code generation instead of runtime reflection
- Better performance and AOT (Ahead-of-Time) compilation support
- Similar API to MediatR but with modern C# features

---

## Checkpoint ✅

By now, you should have:
- ✅ Installed the Clean Architecture template
- ✅ Generated a new solution (`CleanArchWebShop` folder with `src` subfolder)
- ✅ Understood the four main layers (Core, UseCases, Infrastructure, Web)
- ✅ Learned the dependency rule (dependencies point inward)
- ✅ Built and run the application successfully
- ✅ Explored the sample code

---

## Comparison: Legacy vs. Clean Architecture

Let's compare what we have now with the legacy application:

| Aspect | Legacy (`TightlyCoupled.WebShop`) | Clean Architecture |
|--------|-----------------------------------|-------------------|
| **Structure** | Single project with folders | Multiple projects with clear boundaries |
| **Dependencies** | Everything references everything | Dependencies point inward |
| **API Layer** | Controllers with multiple endpoints | FastEndpoints - one class per endpoint |
| **Database** | DbContext used directly in endpoints | Repository pattern with abstractions |
| **Business Logic** | Scattered in controllers, services, utilities | Centralized in Core entities and UseCases |
| **Configuration** | Hard-coded strings throughout | Options pattern with appsettings |
| **Testability** | Difficult - tightly coupled | Easy - interfaces and DI |
| **File/External Access** | Direct in endpoints | Abstracted behind interfaces |

---

## Next Steps

Great job! You now have a Clean Architecture solution ready to go.

In the next lab, we'll implement our first complete feature using a **vertical slice** approach - building the "List Items" feature from domain to API.

➡️ **Continue to [Lab 2: Implement List Items Feature](Lab2.md)**

---

## Additional Resources

- [Clean Architecture Template Docs](https://github.com/ardalis/CleanArchitecture)
- [FastEndpoints Documentation](https://fast-endpoints.com/)
- [Mediator Source Generator](https://github.com/martinothamar/Mediator)
- [Dependency Inversion Principle](https://deviq.com/principles/dependency-inversion-principle)

---

*Questions? Ask your instructor or review the sample code in the generated solution.*

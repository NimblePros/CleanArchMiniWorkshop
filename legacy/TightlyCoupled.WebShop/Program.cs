using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TightlyCoupled.WebShop.Data;
using TightlyCoupled.WebShop.Models;
using TightlyCoupled.WebShop.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(); // aspire

// Configure Serilog from appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

// Use Serilog as the logging provider
builder.Host.UseSerilog();

// Add services to the container.
// Hard-coded connection string - no abstraction
//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
//    ?? "Server=localhost,51433;Database=TightlyCoupledWebShop;User Id=sa;Password=YourStrong!Passw0rd;";

//// Verify the connection string matches our global utility - tight coupling
//if (connectionString != GlobalUtilities.DATABASE_CONNECTION)
//{
//    Console.WriteLine("WARNING: Connection string mismatch between appsettings and GlobalUtilities!");
//    GlobalUtilities.LogError("Connection string mismatch detected");
//}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TightlyCoupledWebShop")));

builder.Services.AddDefaultIdentity<AppUser>(options => 
{
    // Hard-coded identity configuration - should be in configuration
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// No dependency injection for services - they create their own dependencies
// This violates IoC principles
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Environment-specific setup mixed with application startup
var isDevelopment = app.Environment.IsDevelopment();
GlobalUtilities.LogError($"Environment: {(isDevelopment ? "Development" : "Production")}");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

// Startup health checks with direct file system access
try
{
    var healthCheckFile = Path.Combine(GlobalUtilities.LOG_DIRECTORY, "health_check.txt");
    File.WriteAllText(healthCheckFile, $"App healthy at {DateTime.Now}");
    GlobalUtilities.LogError("Health check file created successfully");
}
catch (Exception ex)
{
    GlobalUtilities.LogError($"Health check failed: {ex.Message}");
}

// Global exception handling setup that writes to files
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    try
    {
        var errorFile = Path.Combine(GlobalUtilities.LOG_DIRECTORY, $"unhandled_exceptions_{DateTime.Now:yyyy-MM-dd}.log");
        File.AppendAllText(errorFile, $"[{DateTime.Now}] Unhandled exception: {e.ExceptionObject}\n");
    }
    catch
    {
        // Can't even log the logging error
    }
};

GlobalUtilities.LogError("Application startup completed");

// Aspire integration
app.MapDefaultEndpoints(); // aspire

// Run EF Core migrations at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();


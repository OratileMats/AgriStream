using AgriStream.Data;
using AgriStream.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Application Insights (task f). Reads APPLICATIONINSIGHTS_CONNECTION_STRING from configuration.
builder.Services.AddApplicationInsightsTelemetry();

// Azure SQL (task b)
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("AzureSql"),
        sql => sql.EnableRetryOnFailure()));

// Cookie authentication with role claims (Farmer / Agronomist / Admin)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// Azure Storage (task c) + Functions client (task d)
builder.Services.AddSingleton<IBlobService, BlobService>();
builder.Services.AddSingleton<TelemetryService>();
builder.Services.AddHttpClient<IFunctionsClient, FunctionsClient>();
builder.Services.AddScoped<TelemetryIngestService>();

var app = builder.Build();

// Create schema + seed demo data on startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DbSeeder.SeedAsync(db, app.Configuration);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialisation failed. Check ConnectionStrings:AzureSql.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Lightweight anonymous endpoint, handy for load tests and App Service health checks
app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTimeOffset.UtcNow }));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

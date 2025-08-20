using Microsoft.EntityFrameworkCore;
using FinanceApp.Web.Data;
using FinanceApp.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages()
    .AddRazorRuntimeCompilation();

// Configure connection string with validation for ALL environments
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsProduction())
{
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
    }
    
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException(
            "SECURITY ERROR: Database connection string not configured for production environment. " +
            "Set DATABASE_CONNECTION_STRING environment variable or configure DefaultConnection in appsettings.json.");
    }
}
else
{
    // SECURITY FIX: Validate connection string in ALL environments
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                $"SECURITY ERROR: Database connection string not configured for {builder.Environment.EnvironmentName} environment. " +
                "Configure DefaultConnection in appsettings.json or set DATABASE_CONNECTION_STRING environment variable.");
        }
    }
}

// Configure Entity Framework with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register application services
builder.Services.AddScoped<PdfParserService>();
builder.Services.AddScoped<DataImportService>();
builder.Services.AddScoped<ITrendAnalysisService, TrendAnalysisService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();

// Register security services
builder.Services.AddScoped<IFileValidationService, FileValidationService>();
builder.Services.AddScoped<IInputSanitizationService, InputSanitizationService>();

// Configure file upload size limits from configuration
var maxUploadSize = builder.Configuration.GetValue<int>("Security:MaxUploadSizeInMB", 10);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadSize * 1024 * 1024;
});

// Configure Kestrel server limits
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = maxUploadSize * 1024 * 1024;
});

// Add security headers
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Add security headers middleware
app.Use(async (context, next) =>
{
    // Prevent XSS attacks
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    
    // Content Security Policy optimized for German Finance Application
    // Allows Chart.js, Bootstrap Icons, Google Fonts, and other required resources
    context.Response.Headers.Add("Content-Security-Policy", 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' cdn.jsdelivr.net fonts.googleapis.com; " +
        "font-src 'self' data: fonts.googleapis.com fonts.gstatic.com cdn.jsdelivr.net; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self' fonts.googleapis.com fonts.gstatic.com;");
    
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // Create database if it doesn't exist
        await context.Database.EnsureCreatedAsync();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Database initialized successfully");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database");
    }
}

app.Run();
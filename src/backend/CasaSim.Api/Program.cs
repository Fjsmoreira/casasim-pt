using CasaSim.Api;
using CasaSim.Api.Services;
using CasaSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

    // Services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "CasaSim.pt API",
            Version = "v1",
            Description = "Property aggregator for Pombal, Portugal"
        });
    });

    // Admin authentication
    builder.Services.AddScoped<CasaSim.Api.Auth.AdminAuthenticationFilter>();

    // Database
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connStr))
        {
            options.UseNpgsql(connStr, npgsql =>
                npgsql.UseNetTopologySuite());
            options.UseSnakeCaseNamingConvention();
        }
    });

    // CORS — allow the frontend in dev
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", p => p
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
    });

    // Application services
    builder.Services.AddScoped<CasaSim.Api.Services.IListingQueryService, CasaSim.Api.Services.ListingQueryService>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors("Frontend");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();

    // Auto-migrate on startup (dev convenience)
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible for integration tests
public partial class Program { }

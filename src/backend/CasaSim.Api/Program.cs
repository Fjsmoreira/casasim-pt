using CasaSim.Api;
using CasaSim.Api.Services;
using CasaSim.Core.Interfaces;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Json;
using System.Diagnostics;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter(renderMessage: true))
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("service.name", "casasim-api")
        .Enrich.WithProperty("environment", ctx.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(new JsonFormatter(renderMessage: true)));

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
            options.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
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

    // OpenTelemetry — distributed tracing
    var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService("CasaSim.Api")
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName
            }))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql();

            // Only export via OTLP when an endpoint is explicitly configured.
            // Without this, traces are collected in-process but discarded —
            // no-op and safe for local development.
            if (!string.IsNullOrEmpty(otelEndpoint))
            {
                tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
            }
        });

    // Application services
    builder.Services.AddScoped<CasaSim.Api.Services.IListingQueryService, CasaSim.Api.Services.ListingQueryService>();

    var app = builder.Build();

    app.Use(async (context, next) =>
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;
        var traceId = Activity.Current?.TraceId.ToString() ?? requestId;

        using (LogContext.PushProperty("requestId", requestId))
        using (LogContext.PushProperty("traceId", traceId))
        using (LogContext.PushProperty("route", (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value ?? string.Empty))
        {
            try
            {
                await next();
            }
            finally
            {
                stopwatch.Stop();
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                    ?? context.Request.Path.Value
                    ?? string.Empty;

                using (LogContext.PushProperty("route", route))
                using (LogContext.PushProperty("statusCode", context.Response.StatusCode))
                using (LogContext.PushProperty("durationMs", stopwatch.Elapsed.TotalMilliseconds))
                {
                    Log.Information(
                        "HTTP {Method} {Route} responded {StatusCode} in {DurationMs:0.0000} ms",
                        context.Request.Method,
                        route,
                        context.Response.StatusCode,
                        stopwatch.Elapsed.TotalMilliseconds);
                }
            }
        }
    });

    app.UseCors("Frontend");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();

    if (app.Environment.IsDevelopment())
    {
        // Auto-migrate only for local developer convenience. Production uses
        // explicit migration bundles/services so startup is never destructive.
        using var scope = app.Services.CreateScope();
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

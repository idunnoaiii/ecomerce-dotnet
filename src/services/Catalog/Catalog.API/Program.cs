using System.Reflection;
using Catalog.API;
using Catalog.API.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddCustomDbContext(builder.Configuration);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);



var app = builder.Build();

app.MapGet("/ping/", () => "Hello World!");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

using var scope = app.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<CatalogContext>();
var env = app.Services.GetService<IWebHostEnvironment>();
var settings = app.Services.GetService<IOptions<CatalogSettings>>();
var logger = app.Services.GetService<ILogger<CatalogContextSeed>>();
await context.Database.MigrateAsync();

await new CatalogContextSeed().SeedAsync(context, env, settings, logger);

app.MapControllers();

app.Run();

public static class CustomExtensionMethods
{
    public static IServiceCollection AddCustomDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEntityFrameworkSqlServer()
            .AddDbContext<CatalogContext>(options =>
            {
                options.UseSqlServer(configuration["ConnectionString"],
                    sqlServerOptionsAction: sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(typeof(Program).GetTypeInfo().Assembly.GetName().Name);
                        //Configuring Connection Resiliency: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency 
                        sqlOptions.EnableRetryOnFailure(maxRetryCount: 15, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
                    });
            });

        // services.AddDbContext<IntegrationEventLogContext>(options =>
        // {
        //     options.UseSqlServer(configuration["ConnectionString"],
        //         sqlServerOptionsAction: sqlOptions =>
        //         {
        //             sqlOptions.MigrationsAssembly(typeof(Program).GetTypeInfo().Assembly.GetName().Name);
        //             //Configuring Connection Resiliency: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency 
        //             sqlOptions.EnableRetryOnFailure(maxRetryCount: 15, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        //         });
        // });

        return services;
    }
    
    public static IServiceCollection AddCustomHealthCheck(this IServiceCollection services, IConfiguration configuration)
    {var hcBuilder = services.AddHealthChecks();

        hcBuilder
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddSqlServer(
                configuration["ConnectionString"],
                name: "CatalogDB-check",
                tags: new string[] { "catalogdb" });
        return services;
    }
}
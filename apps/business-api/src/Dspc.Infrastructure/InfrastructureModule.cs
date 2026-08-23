using Dspc.Application.Abstractions;
using Dspc.Infrastructure.Identity;
using Dspc.Infrastructure.Outbox;
using Dspc.Infrastructure.Persistence;
using Dspc.Infrastructure.Seeding;
using Dspc.Infrastructure.Services;
using Dspc.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dspc.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));
        services.Configure<PlanningEngineOptions>(config.GetSection(PlanningEngineOptions.Section));
        services.Configure<SeedOptions>(config.GetSection(SeedOptions.Section));

        var cs = config.GetConnectionString("Default") ?? "Host=localhost;Port=5432;Database=dspc;Username=dspc;Password=dspc";
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs, n => n.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)).EnableDetailedErrors());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IDemoClock, DemoClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IRecentErrors, RecentErrorsBuffer>();
        services.AddSingleton<SeedState>();
        services.AddHttpClient("probe");
        services.AddSingleton<IExternalServiceProbe, ExternalServiceProbe>();
        services.AddSingleton<IFileScanner, NoOpFileScanner>();

        services.AddSingleton<IDocumentStorage>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<StorageOptions>>();
            return o.Value.Provider.Equals("Minio", StringComparison.OrdinalIgnoreCase) ? new MinioDocumentStorage(o) : new FileSystemDocumentStorage(o);
        });

        services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        services.AddScoped<IDemoSeeder, DemoSeeder>();
        services.AddHostedService<OutboxDispatcherHostedService>();
        services.AddHostedService<MigrateAndSeedHostedService>();
        return services;
    }
}

/// <summary>Applies migrations and seeds demo data on startup (demo profile). Failure is logged, not fatal — /health/ready reports the state.</summary>
public sealed class MigrateAndSeedHostedService(IServiceScopeFactory scopes, IConfiguration config, IHostEnvironment env, ILogger<MigrateAndSeedHostedService> log) : IHostedService
{
    public static bool Ready { get; private set; }
    public static string? LastError { get; private set; }

    public async Task StartAsync(CancellationToken ct)
    {
        if (config.GetValue<bool>("Seed:Skip")) { Ready = true; return; }
        for (var attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync(ct);
                if (config.GetValue<bool>("Demo:Enabled") || env.IsDevelopment())
                {
                    var seeder = scope.ServiceProvider.GetRequiredService<IDemoSeeder>();
                    var result = await seeder.SeedIfEmptyAsync(ct);
                    log.LogInformation("Demo seed state: version {Version}, {Ms} ms", result.SeedVersion, result.DurationMs);
                }
                Ready = true; LastError = null;
                return;
            }
            catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
            {
                LastError = ex.Message;
                log.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                log.LogError(ex, "Migration/seed failed");
                return;
            }
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

using Dspc.Application.Abstractions;
using Dspc.Application.Modules.Admin;
using Dspc.Application.Modules.Audit;
using Dspc.Application.Modules.Dashboard;
using Dspc.Application.Modules.Demo;
using Dspc.Application.Modules.Identity;
using Dspc.Application.Modules.Inbound;
using Dspc.Application.Modules.Inventory;
using Dspc.Application.Modules.Notifications;
using Dspc.Application.Modules.Planning;
using Dspc.Application.Modules.Risk;
using Dspc.Application.Modules.Suppliers;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dspc.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RiskOptions>(config.GetSection(RiskOptions.Section));
        services.Configure<PlanningOptions>(config.GetSection(PlanningOptions.Section));
        services.Configure<DemoOptions>(config.GetSection(DemoOptions.Section));
        services.Configure<LocalAiOptions>(config.GetSection(LocalAiOptions.Section));
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

        services.AddScoped<Modules.Sites.ISiteContext, Modules.Sites.SiteContext>();
        services.AddScoped<Modules.Sites.SiteQueries>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IdentityService>();
        services.AddScoped<PlanModelBuilder>();
        services.AddScoped<IPlanImpactEvaluator, PlanImpactService>();
        services.AddScoped<PlanningQueries>();
        services.AddScoped<ScenarioService>();
        services.AddScoped<ScenarioPresetProvider>();
        services.AddSingleton<ScenarioRunQueue>();
        services.AddSingleton<PlanningEngineMetrics>();
        services.AddHostedService<ScenarioRunnerHostedService>();
        services.AddScoped<RiskAssessmentService>();
        services.AddScoped<PurchaseOrderQueries>();
        services.AddScoped<PurchaseOrderCommands>();
        services.AddScoped<ShipmentService>();
        services.AddScoped<LogisticsEventService>();
        services.AddScoped<Modules.Documents.DocumentService>();
        services.AddScoped<Modules.Documents.AiExtractionService>();
        services.AddScoped<Modules.Quality.TraceabilityIndex>();
        services.AddScoped<Modules.Quality.LotService>();
        services.AddScoped<Modules.Traceability.TraceQueries>();
        services.AddScoped<Modules.Passports.PassportService>();
        services.AddScoped<Modules.Passports.PassportInvalidationService>();
        services.AddScoped<IDomainEventHandler<Domain.Events.MaterialLotBlocked>, Modules.Quality.MaterialLotBlockedHandler>();
        services.AddScoped<SupplierQueries>();
        services.AddScoped<DashboardQueries>();
        services.AddScoped<InventoryQueries>();
        services.AddScoped<NotificationService>();
        services.AddScoped<AuditQueries>();
        services.AddScoped<DemoService>();
        services.AddScoped<AdminService>();
        return services;
    }
}

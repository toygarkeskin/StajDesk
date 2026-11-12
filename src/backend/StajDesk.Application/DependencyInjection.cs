using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Admin;
using StajDesk.Application.Features.Assets;
using StajDesk.Application.Features.Auth;
using StajDesk.Application.Features.Categories;
using StajDesk.Application.Features.Notifications;
using StajDesk.Application.Features.Stats;
using StajDesk.Application.Features.Users;
using StajDesk.Application.Features.Workspace;
using StajDesk.Application.Abstractions;

namespace StajDesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // CQRS: talep modülünün komut ve sorgu handler'ları + dispatcher
        services.AddRequestHandlers(assembly);

        // Uygulama servisleri (Scoped: her HTTP isteği için bir örnek, DbContext ile aynı yaşam süresi)
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<ITicketSearch, ContainsTicketSearch>(); // Infrastructure PostgreSQL tam metin aramasıyla değiştirir
        services.AddSingleton(TimeProvider.System);

        // PasswordRule bir "alt kural"dır; her string parametreye uygulanmasın diye otomatik kayıttan hariç.
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true, filter: r => r.ValidatorType != typeof(PasswordRule));
        return services;
    }
}

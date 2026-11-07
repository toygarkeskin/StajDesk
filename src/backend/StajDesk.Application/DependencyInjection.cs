using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StajDesk.Application.Features.Assets;
using StajDesk.Application.Features.Auth;
using StajDesk.Application.Features.Categories;
using StajDesk.Application.Features.Notifications;
using StajDesk.Application.Features.Stats;
using StajDesk.Application.Features.Tickets;
using StajDesk.Application.Features.Users;

namespace StajDesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped: her HTTP isteği için bir örnek (DbContext ile aynı yaşam süresi).
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using StajDesk.Application.Abstractions;
using StajDesk.Infrastructure.Caching;
using StajDesk.Infrastructure.Messaging;
using StajDesk.Infrastructure.Persistence;
using StajDesk.Infrastructure.Security;

namespace StajDesk.Infrastructure;

public static class DependencyInjection
{
    /// <summary>PostgreSQL + şifre hash (API ve Worker ortak kullanır).</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Postgres")
                 ?? throw new InvalidOperationException("ConnectionStrings:Postgres tanımlı değil.");

        services.AddDbContext<AppDbContext>(o => o
            .UseNpgsql(cs, npg => npg.EnableRetryOnFailure(3).UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<DbSeeder>();
        return services;
    }

    /// <summary>API için: Redis (önbellek + hız sınırı), outbox, unit of work, denetim kaydı ve outbox dağıtıcısı.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddPersistence(config);

        var redisCs = config.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var opts = ConfigurationOptions.Parse(redisCs);
            opts.AbortOnConnectFail = false; // Redis kapalı olsa da uygulama açılsın, arka planda yeniden bağlansın
            opts.ConnectTimeout = 2000;
            opts.SyncTimeout = 2000;
            opts.AsyncTimeout = 2000;
            return ConnectionMultiplexer.Connect(opts);
        });
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.Configure<RateLimitOptions>(config.GetSection("LoginRateLimit"));
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();

        services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        services.Configure<RabbitMqOptions>(config.GetSection("RabbitMq"));
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
}

using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace StajDesk.Application.Common;

/*
 * Hafif CQRS altyapısı (dış kütüphane yok):
 *  - ICommand<T>: sistemin durumunu değiştirir (yazma tarafı).
 *  - IQuery<T>  : yalnızca okur, doğrudan DTO döndürür (okuma tarafı; AsNoTracking + projeksiyon).
 * Controller iş mantığını bilmez; yalnızca mesajı dispatcher'a verir.
 * Dispatcher, mesaj için kayıtlı bir FluentValidation doğrulayıcısı varsa önce onu çalıştırır.
 */
public interface IRequest<TResult>;
public interface ICommand<TResult> : IRequest<TResult>;
public interface IQuery<TResult> : IRequest<TResult>;

public interface IRequestHandler<in TRequest, TResult> where TRequest : IRequest<TResult>
{
    Task<TResult> HandleAsync(TRequest request, CancellationToken ct);
}

public interface IDispatcher
{
    Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct = default);
}

public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<(Type, Type), Type> HandlerTypes = new();

    public async Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken ct = default)
    {
        var requestType = request.GetType();

        var validatorType = typeof(IValidator<>).MakeGenericType(requestType);
        if (services.GetService(validatorType) is IValidator validator)
        {
            var result = await validator.ValidateAsync(new ValidationContext<object>(request), ct);
            if (!result.IsValid) throw new ValidationException(result.Errors);
        }

        var handlerType = HandlerTypes.GetOrAdd((requestType, typeof(TResult)),
            k => typeof(IRequestHandler<,>).MakeGenericType(k.Item1, k.Item2));
        dynamic handler = services.GetRequiredService(handlerType);
        return await handler.HandleAsync((dynamic)request, ct);
    }
}

public static class CqrsRegistration
{
    /// <summary>Bir derlemedeki tüm IRequestHandler uygulamalarını scoped olarak kaydeder.</summary>
    public static IServiceCollection AddRequestHandlers(this IServiceCollection services, System.Reflection.Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            services.AddScoped(iface, type);
        services.AddScoped<IDispatcher, Dispatcher>();
        return services;
    }
}

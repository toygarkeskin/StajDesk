using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Application.Common;
using StajDesk.Domain.Common;

namespace StajDesk.Api.Infrastructure;

/// <summary>
/// Global hata yakalayıcı: her hatayı standart yanıt zarfına ({ success:false, message, errors }) çevirir.
/// Beklenmeyen hatalarda iç ayrıntı kullanıcıya gösterilmez, yalnızca loglanır.
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        // OAuth uç noktaları RFC 6749 §5.2 biçiminde hata döner: { error, error_description }
        if (ex is StajDesk.Application.Features.Auth.OAuthException oauth)
        {
            logger.LogInformation("OAuth isteği reddedildi: {Error} {Description}", oauth.Error, oauth.Message);
            ctx.Response.StatusCode = oauth.Status;
            ctx.Response.Headers.CacheControl = "no-store";
            await ctx.Response.WriteAsJsonAsync(new Dictionary<string, string> { ["error"] = oauth.Error, ["error_description"] = oauth.Message }, ct);
            return true;
        }

        var (status, message, errors) = ex switch
        {
            ValidationException v => (StatusCodes.Status400BadRequest, "Bazı alanlar eksik veya geçersiz. İşaretli alanları düzeltip tekrar deneyin.", ToErrors(v)),
            DomainException d => (StatusCodes.Status400BadRequest, d.Message, null),
            NotFoundException n => (StatusCodes.Status404NotFound, n.Message, null),
            UnauthorizedException u => (StatusCodes.Status401Unauthorized, u.Message, null),
            ForbiddenException f => (StatusCodes.Status403Forbidden, f.Message, null),
            ConflictException c => (StatusCodes.Status409Conflict, c.Message, null),
            TooManyRequestsException t => (StatusCodes.Status429TooManyRequests, t.Message, null),
            OperationCanceledException => (499, "İstek iptal edildi.", null),
            _ => (StatusCodes.Status500InternalServerError, "Sunucu isteği tamamlayamadı. Birkaç saniye sonra tekrar deneyin; sorun sürerse hata ayrıntılarını destek ekibine iletin.", (IDictionary<string, string[]>?)null)
        };

        if (status >= 500) logger.LogError(ex, "İşlenmeyen hata: {Path}", ctx.Request.Path);
        else logger.LogInformation("İstek reddedildi {Status}: {Message}", status, ex.Message);

        if (ex is TooManyRequestsException tm)
            ctx.Response.Headers.RetryAfter = tm.RetryAfterSeconds.ToString();

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(ApiResponse.Fail(message, errors), ct);
        return true;
    }

    private static Dictionary<string, string[]> ToErrors(ValidationException v) =>
        v.Errors.GroupBy(e => ToCamel(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

    internal static string ToCamel(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

/// <summary>
/// Her action argümanı için kayıtlı bir FluentValidation doğrulayıcısı varsa çalıştırır.
/// Böylece controller'larda tek satır doğrulama kodu yazmamız gerekmez.
/// </summary>
internal sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(arg), context.HttpContext.RequestAborted);
            if (!result.IsValid) throw new ValidationException(result.Errors);
        }
        await next();
    }
}

internal static class ApiBehavior
{
    /// <summary>Model binding hataları (ör. bozuk JSON) da aynı zarfla dönsün.</summary>
    public static IActionResult InvalidModelState(ActionContext ctx)
    {
        var errors = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(kv => GlobalExceptionHandler.ToCamel(kv.Key.TrimStart('$', '.')),
                kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Geçersiz değer." : e.ErrorMessage).ToArray());
        return new BadRequestObjectResult(ApiResponse.Fail("Bazı alanlar eksik veya geçersiz. İşaretli alanları düzeltip tekrar deneyin.", errors));
    }
}

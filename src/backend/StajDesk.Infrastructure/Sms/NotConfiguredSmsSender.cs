using StajDesk.Application.Abstractions;

namespace StajDesk.Infrastructure.Sms;

/// <summary>
/// Varsayılan SMS göndericisi: gerçek sağlayıcı bağlı DEĞİL. <see cref="IsConfigured"/> false olduğu için
/// SMS doğrulaması arayüzde ve API'de kapalı görünür; hiçbir yerde "SMS gönderildi" denmez.
/// Gerçek sağlayıcı eklemek için ISmsSender'ı uygulayan bir sınıf yazıp DependencyInjection'da bunun yerine kaydedin
/// (bkz. docs/ORGANIZASYON.md → SMS sağlayıcısı).
/// </summary>
internal sealed class NotConfiguredSmsSender : ISmsSender
{
    public bool IsConfigured => false;
    public string ProviderName => "yapılandırılmadı";

    public Task SendAsync(string phone, string message, CancellationToken ct = default) =>
        throw new InvalidOperationException("SMS sağlayıcısı yapılandırılmadı.");
}

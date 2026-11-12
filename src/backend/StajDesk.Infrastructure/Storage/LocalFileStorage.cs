using Microsoft.Extensions.Configuration;
using StajDesk.Application.Abstractions;

namespace StajDesk.Infrastructure.Storage;

/// <summary>
/// Talep eklerini diskte saklar (Docker'da "uploads" volume'u). Anahtar sunucuda üretilen 32 haneli hex'tir;
/// yine de doğrulanır — böylece "../" gibi bir değer asla yol olarak kullanılamaz.
/// </summary>
internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration config)
    {
        _root = Path.GetFullPath(config["Storage:Path"] is { Length: > 0 } p ? p : Path.Combine(AppContext.BaseDirectory, "uploads"));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken ct = default)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); // anahtarın ilk 2 hanesiyle alt klasör (tek klasörde binlerce dosya olmasın)
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = PathOf(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = PathOf(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string PathOf(string key)
    {
        if (key.Length != 32 || !key.All(Uri.IsHexDigit)) throw new ArgumentException("Geçersiz dosya anahtarı.", nameof(key));
        return Path.Combine(_root, key[..2], key);
    }
}

using Microsoft.AspNetCore.Identity;
using StajDesk.Domain.Entities;

namespace StajDesk.Infrastructure.Security;

/// <summary>
/// ASP.NET Core Identity'nin PBKDF2 tabanlı hasher'ı: her şifre için rastgele salt üretir,
/// salt ve hash'i tek bir metinde birlikte saklar.
/// </summary>
internal sealed class PasswordHasher : Application.Abstractions.IPasswordHasher
{
    private static readonly PasswordHasher<User> Inner = new();
    private static readonly User Dummy = new();

    public string Hash(string password) => Inner.HashPassword(Dummy, password);

    public bool Verify(string hash, string password) =>
        Inner.VerifyHashedPassword(Dummy, hash, password) is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
}

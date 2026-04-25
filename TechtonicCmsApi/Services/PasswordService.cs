using System.Security.Cryptography;
using System.Text;

namespace TechtonicCmsApi.Services;

public class PasswordService
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, 12);
    }

    public (bool isValid, string? newHash) VerifyPassword(string password, string existingHash)
    {
        if (existingHash.StartsWith("$2"))
        {
            bool isValid = BCrypt.Net.BCrypt.Verify(password, existingHash);
            return (isValid, null);
        }

        string sha256Hash = ComputeSha256(password);
        if (sha256Hash == existingHash)
        {
            return (true, HashPassword(password));
        }

        return (false, null);
    }

    private static string ComputeSha256(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

using System.Security.Cryptography;
using System.Text;

namespace TechtonicCmsApi.Services;

public class ApiKeyService
{
    public (string RawKey, string Hash, string Prefix) GenerateKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var raw = "ttcms_" + Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
        var hash = ComputeHash(raw);
        return (raw, hash, raw[..Math.Min(8, raw.Length)]);
    }

    public string ComputeHash(string rawKey)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
    }
}

using System.Security.Cryptography;
using System.Text;
using Isopoh.Cryptography.Argon2;

namespace TechtonicCmsApi.Services;

public class PasswordService
{
    private static readonly Argon2Config DefaultConfig = new()
    {
        Type = Argon2Type.DataIndependentAddressing,
        Version = Argon2Version.Nineteen,
        TimeCost = 3,
        MemoryCost = 65536,
        Lanes = 4,
        Threads = 4,
        HashLength = 32,
    };

    public string HashPassword(string password)
    {
        var config = new Argon2Config
        {
            Type = DefaultConfig.Type,
            Version = DefaultConfig.Version,
            TimeCost = DefaultConfig.TimeCost,
            MemoryCost = DefaultConfig.MemoryCost,
            Lanes = DefaultConfig.Lanes,
            Threads = DefaultConfig.Threads,
            HashLength = DefaultConfig.HashLength,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = RandomNumberGenerator.GetBytes(16),
        };

        var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }

    public (bool isValid, string? newHash) VerifyPassword(string password, string existingHash)
    {
        if (existingHash.StartsWith("$argon2"))
        {
            bool isValid = Argon2.Verify(existingHash, password);
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

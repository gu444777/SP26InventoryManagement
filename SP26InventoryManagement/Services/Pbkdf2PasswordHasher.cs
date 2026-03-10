using System.Security.Cryptography;

namespace SP26InventoryManagement.Services;

public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2";
    private const int Iterations = 100000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    private const string UpperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LowerChars = "abcdefghijkmnopqrstuvwxyz";
    private const string DigitChars = "23456789";
    private const string SpecialChars = "!@#$%^&*()-_=+[]{}";

    public string Hash(string plainPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainPassword);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(plainPassword, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string plainPassword, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string[] parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out int iterations))
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expectedKey = Convert.FromBase64String(parts[3]);
            byte[] actualKey = Rfc2898DeriveBytes.Pbkdf2(plainPassword, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);

            return CryptographicOperations.FixedTimeEquals(expectedKey, actualKey);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public string GenerateRandomPassword()
    {
        const int passwordLength = 12;

        List<char> chars =
        [
            GetRandomChar(UpperChars),
            GetRandomChar(LowerChars),
            GetRandomChar(DigitChars),
            GetRandomChar(SpecialChars)
        ];

        string allChars = $"{UpperChars}{LowerChars}{DigitChars}{SpecialChars}";
        for (int i = chars.Count; i < passwordLength; i++)
        {
            chars.Add(GetRandomChar(allChars));
        }

        Shuffle(chars);
        return new string(chars.ToArray());
    }

    private static char GetRandomChar(string source)
    {
        int index = RandomNumberGenerator.GetInt32(source.Length);
        return source[index];
    }

    private static void Shuffle(IList<char> chars)
    {
        for (int i = chars.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}

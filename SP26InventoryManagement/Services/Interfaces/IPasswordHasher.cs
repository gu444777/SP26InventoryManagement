namespace SP26InventoryManagement.Services;

public interface IPasswordHasher
{
    string Hash(string plainPassword);

    bool Verify(string plainPassword, string storedHash);

    string GenerateRandomPassword();
}

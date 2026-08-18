namespace Agirh.Core.Ports;

public interface IPasswordHasher
{
    string HashPassword(string plainTextPassword);
    bool VerifyPassword(string plainTextPassword, string hash);
}

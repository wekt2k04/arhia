namespace Agirh.Core.Ports;

public interface IPasswordHasher
{
    string HacherMotDePasse(string motDePasseEnClair);
    bool VerifierMotDePasse(string motDePasseEnClair, string hash);
}

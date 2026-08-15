using System.Threading;
using System.Threading.Tasks;
using Agirh.Domain.Entities;

namespace Agirh.Core.Ports;

public interface ICompteUtilisateurRepository
{
    Task<CompteUtilisateur?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<CompteUtilisateur?> ObtenirParEmailAsync(string email, CancellationToken ct = default);
    Task AjouterAsync(CompteUtilisateur compte, CancellationToken ct = default);
    Task MettreAJourAsync(CompteUtilisateur compte, CancellationToken ct = default);
}

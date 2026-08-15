using System.Threading;
using System.Threading.Tasks;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;

namespace Agirh.Core.Ports;

public interface ICollaborateurRepository
{
    Task<Collaborateur?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<Collaborateur?> ObtenirParMatriculeAsync(Matricule matricule, CancellationToken ct = default);
    Task<Collaborateur?> ObtenirParCompteUtilisateurIdAsync(Guid compteUtilisateurId, CancellationToken ct = default);
    Task<IReadOnlyList<Collaborateur>> ListerParPoleAsync(Guid poleId, CancellationToken ct = default);
    Task AjouterAsync(Collaborateur collaborateur, CancellationToken ct = default);
    Task MettreAJourAsync(Collaborateur collaborateur, CancellationToken ct = default);
}

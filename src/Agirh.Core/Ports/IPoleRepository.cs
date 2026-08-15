using System.Threading;
using System.Threading.Tasks;
using Agirh.Domain.Entities;

namespace Agirh.Core.Ports;

public interface IPoleRepository
{
    Task<Pole?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task AjouterAsync(Pole pole, CancellationToken ct = default);
}

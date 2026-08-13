namespace Agirh.Core.Services;

/// <summary>
/// Port applicatif : création d'une demande d'avance sur salaire.
/// Déclaré en Core, implémenté en Core, consommé par les adaptateurs Infrastructure.
/// </summary>
public interface ICreateSalaryAdvanceRequest
{
    Task<string> ExecuteAsync(string employeeId, decimal amount, string requestingUserId, CancellationToken ct = default);
}

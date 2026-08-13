using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;

namespace Agirh.Core.Services;

/// <summary>
/// Use case « Demander une avance sur salaire ».
/// Contient TOUTES les règles métier : fail-closed sur l'identité, anti-IDOR,
/// contrôle du profil paie, anti-doublon, plafond (50% du salaire net),
/// persistance d'une demande "Pending".
/// Ne dépend que de ports (Domain.Interfaces) et de l'unité de travail (Core.Interfaces).
/// </summary>
public sealed class CreateSalaryAdvanceRequest : ICreateSalaryAdvanceRequest
{
    private readonly IPayrollProfileRepository _payrollRepo;
    private readonly ISalaryAdvanceRepository _advanceRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSalaryAdvanceRequest(
        IPayrollProfileRepository payrollRepo,
        ISalaryAdvanceRepository advanceRepo,
        IUnitOfWork unitOfWork)
    {
        _payrollRepo = payrollRepo;
        _advanceRepo = advanceRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> ExecuteAsync(string employeeId, decimal amount, string requestingUserId, CancellationToken ct = default)
    {
        // Fail-closed : si l'identité JWT de l'appelant ne peut pas être résolue, on refuse.
        if (!Guid.TryParse(requestingUserId, out var requesterId))
            return "Erreur : impossible de résoudre l'identité de l'appelant. Action refusée.";

        // Cible : employeeId manquant/invalide -> repli sur l'identité JWT (jamais un GUID vide).
        if (!Guid.TryParse(employeeId, out var targetId))
            targetId = requesterId;

        // Contrôle d'identité AVANT toute lecture du profil paie ou écriture.
        var requester = await _unitOfWork.Employees.GetByIdAsync(requesterId);
        if (requester == null)
            return "Erreur : compte appelant introuvable. Action refusée.";

        // IDOR : un Collaborateur ne peut demander une avance que pour lui-même.
        if (string.Equals(requester.Role, "Collaborator", StringComparison.OrdinalIgnoreCase) && targetId != requesterId)
            return "Action non autorisée. Vous ne pouvez demander une avance que pour vous-même.";

        var profile = await _payrollRepo.GetByEmployeeIdAsync(targetId);
        if (profile == null)
            return "Erreur : aucun profil paie n'existe pour ce collaborateur. Veuillez contacter la RH.";

        if (await _advanceRepo.HasPendingRequestAsync(targetId))
            return "Vous avez déjà une demande d'avance sur salaire en attente. Elle doit être traitée avant d'en soumettre une nouvelle.";

        if (amount <= 0)
            return "Erreur : le montant demandé doit être supérieur à zéro.";

        if (amount > profile.NetSalary * profile.MaxAdvancePercentage)
        {
            return "Demande refusée : le montant demandé dépasse votre plafond autorisé pour une avance sur salaire.";
        }

        // Id explicite et stable : connu DÈS le retour du use case (le provider
        // InMemory générerait une clé Guid, mais l'assignation garantit un Id
        // non vide avant tout appel et le rend disponible pour le marqueur WIDGET).
        var request = new SalaryAdvanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = targetId,
            AmountRequested = amount,
            Status = "Pending",
            RequestDate = DateTime.UtcNow,
        };
        await _advanceRepo.AddAsync(request);
        await _unitOfWork.SaveChangesAsync(ct);

        return $"Votre demande d'avance sur salaire de {amount} € a été enregistrée avec succès et est en attente d'approbation par la RH.\n||WIDGET:SalaryAdvance:{request.Id}||";
    }
}

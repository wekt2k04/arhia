using System.Threading;
using System.Threading.Tasks;
using Arhia.Core.Ports;
using Arhia.Domain;
using Arhia.Domain.Entities;

namespace Arhia.Core.UseCases;

/// <summary>
/// Barre de notifications (03_guide_referent_pole.md §3, docs/LOGIQUE_METIER.md §9). Calculée à la
/// demande à partir des données déjà persistées (WorkflowInstance, Collaborateur,
/// WorkflowTemplate) — pas de table Notification dédiée, pas d'événement à émettre : toute
/// nouvelle lecture reflète l'état courant. Seuils (3 jours) fixés avec le porteur du projet ;
/// pour une démo, avancer/reculer WorkflowInstance.DateCreation ou Collaborateur.DateDepart en
/// base suffit à faire apparaître/disparaître une alerte, sans mode démo dédié.
///
/// Portée limitée à ce que le corpus décrit explicitement : RH voit les items en attente depuis
/// longtemps et les échéances de départ approchantes de son propre pôle ; Admin/Qualité voit la
/// file des templates en attente de validation. Un Collaborateur ne reçoit aucune notification —
/// le document source ne décrit ce mécanisme que du point de vue du RH référent.
/// </summary>
public sealed class GetNotificationsUseCase
{
    private const int PendingItemThresholdDays = 3;
    private const int DepartureThresholdDays = 3;

    private readonly IEmployeeRepository _employees;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IWorkflowTemplateRepository _templates;

    public GetNotificationsUseCase(
        IEmployeeRepository employees,
        IWorkflowInstanceRepository instances,
        IWorkflowTemplateRepository templates)
    {
        _employees = employees;
        _instances = instances;
        _templates = templates;
    }

    public async Task<IReadOnlyList<Notification>> ExecuteAsync(UserAccount actor, DateTime now, CancellationToken ct = default) =>
        actor.Role switch
        {
            RoleType.HR => await NotificationsForHRAsync(actor, now, ct),
            RoleType.QualityAdmin => await NotificationsForAdminAsync(now, ct),
            _ => Array.Empty<Notification>()
        };

    private async Task<IReadOnlyList<Notification>> NotificationsForHRAsync(UserAccount actor, DateTime now, CancellationToken ct)
    {
        // actor.DepartmentId est garanti non-null pour un RH (UserAccount.ValidateDepartmentId).
        var employees = await _employees.ListByDepartmentAsync(actor.DepartmentId!.Value, ct);
        var notifications = new List<Notification>();

        foreach (var employee in employees)
        {
            foreach (var type in new[] { WorkflowType.Onboarding, WorkflowType.Offboarding })
            {
                var instance = await _instances.GetByEmployeeAsync(employee.Id, type, ct);
                if (instance is null || instance.Status != WorkflowStatus.InProgress)
                    continue;

                var pendingItems = instance.Items.Count(i => i.Status == ItemStatus.Pending);
                if (pendingItems > 0 && (now - instance.CreatedAt).TotalDays >= PendingItemThresholdDays)
                {
                    notifications.Add(new Notification(
                        NotificationType.ItemPendingTooLong,
                        $"{pendingItems} item(s) en attente depuis plus de {PendingItemThresholdDays} jours — dossier {type} de {employee.FirstName} {employee.LastName}.",
                        instance.CreatedAt,
                        instance.Id));
                }
            }

            if (employee.DepartureDate is { } departureDate)
            {
                var daysRemaining = (departureDate - now).TotalDays;
                var offboardingAlreadyStarted = await _instances.GetByEmployeeAsync(employee.Id, WorkflowType.Offboarding, ct) is not null;

                if (!offboardingAlreadyStarted && daysRemaining >= 0 && daysRemaining <= DepartureThresholdDays)
                {
                    notifications.Add(new Notification(
                        NotificationType.UpcomingDeparture,
                        $"Départ de {employee.FirstName} {employee.LastName} prévu dans {(int)Math.Ceiling(daysRemaining)} jour(s) — offboarding pas encore démarré.",
                        departureDate,
                        employee.Id));
                }
            }
        }

        return notifications;
    }

    private async Task<IReadOnlyList<Notification>> NotificationsForAdminAsync(DateTime now, CancellationToken ct)
    {
        var inReview = await _templates.ListByStatusAsync(TemplateStatus.InReview, ct);

        return inReview
            .Select(t => new Notification(
                NotificationType.TemplatePendingValidation,
                $"Template {t.Type} v{t.Version} en attente de {(t.VerifierId is null ? "vérification" : "approbation")}.",
                t.CreatedAt,
                t.Id))
            .ToList();
    }
}

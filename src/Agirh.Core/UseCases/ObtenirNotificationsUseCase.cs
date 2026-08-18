using System.Threading;
using System.Threading.Tasks;
using Agirh.Core.Ports;
using Agirh.Domain;
using Agirh.Domain.Entities;

namespace Agirh.Core.UseCases;

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
public sealed class ObtenirNotificationsUseCase
{
    private const int SeuilJoursItemEnAttente = 3;
    private const int SeuilJoursEcheanceDepart = 3;

    private readonly IEmployeeRepository _employees;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IWorkflowTemplateRepository _templates;

    public ObtenirNotificationsUseCase(
        IEmployeeRepository employees,
        IWorkflowInstanceRepository instances,
        IWorkflowTemplateRepository templates)
    {
        _employees = employees;
        _instances = instances;
        _templates = templates;
    }

    public async Task<IReadOnlyList<Notification>> ExecuteAsync(UserAccount actor, DateTime maintenant, CancellationToken ct = default) =>
        actor.Role switch
        {
            RoleType.HR => await NotificationsPourRHAsync(actor, maintenant, ct),
            RoleType.QualityAdmin => await NotificationsPourAdminAsync(maintenant, ct),
            _ => Array.Empty<Notification>()
        };

    private async Task<IReadOnlyList<Notification>> NotificationsPourRHAsync(UserAccount actor, DateTime maintenant, CancellationToken ct)
    {
        // actor.DepartmentId est garanti non-null pour un RH (UserAccount.ValidateDepartmentId).
        var employees = await _employees.ListByDepartmentAsync(actor.DepartmentId!.Value, ct);
        var notifications = new List<Notification>();

        foreach (var employee in employees)
        {
            foreach (var type in new[] { WorkflowType.Onboarding, WorkflowType.Offboarding })
            {
                var instance = await _instances.ObtenirParCollaborateurAsync(employee.Id, type, ct);
                if (instance is null || instance.Statut != WorkflowStatus.EnCours)
                    continue;

                var itemsEnAttente = instance.Items.Count(i => i.Etat == ItemEtat.EnAttente);
                if (itemsEnAttente > 0 && (maintenant - instance.DateCreation).TotalDays >= SeuilJoursItemEnAttente)
                {
                    notifications.Add(new Notification(
                        TypeNotification.ItemEnAttenteDepuisLongtemps,
                        $"{itemsEnAttente} item(s) en attente depuis plus de {SeuilJoursItemEnAttente} jours — dossier {type} de {employee.FirstName} {employee.LastName}.",
                        instance.DateCreation,
                        instance.Id));
                }
            }

            if (employee.DepartureDate is { } departureDate)
            {
                var joursRestants = (departureDate - maintenant).TotalDays;
                var offboardingDejaDemarre = await _instances.ObtenirParCollaborateurAsync(employee.Id, WorkflowType.Offboarding, ct) is not null;

                if (!offboardingDejaDemarre && joursRestants >= 0 && joursRestants <= SeuilJoursEcheanceDepart)
                {
                    notifications.Add(new Notification(
                        TypeNotification.EcheanceDepartApprochante,
                        $"Départ de {employee.FirstName} {employee.LastName} prévu dans {(int)Math.Ceiling(joursRestants)} jour(s) — offboarding pas encore démarré.",
                        departureDate,
                        employee.Id));
                }
            }
        }

        return notifications;
    }

    private async Task<IReadOnlyList<Notification>> NotificationsPourAdminAsync(DateTime maintenant, CancellationToken ct)
    {
        var enValidation = await _templates.ListerParStatutAsync(TemplateStatut.EnValidation, ct);

        return enValidation
            .Select(t => new Notification(
                TypeNotification.TemplateEnAttenteValidation,
                $"Template {t.Type} v{t.Version} en attente de {(t.VerificateurId is null ? "vérification" : "approbation")}.",
                t.DateCreation,
                t.Id))
            .ToList();
    }
}

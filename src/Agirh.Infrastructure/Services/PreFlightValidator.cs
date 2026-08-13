using System.Text.Json;
using Agirh.Core.Interfaces;

namespace Agirh.Infrastructure.Services;

public sealed class PreFlightValidator : IPreFlightValidator
{
    public ValidationResult ValidateParameters(string toolName, IReadOnlyDictionary<string, string?> entities)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var missingFields = new List<string>();

        switch (toolName)
        {
            case "ConsulterSoldeAsync":
            case "ConsulterHistoriqueCongesAsync":
                if (entities.TryGetValue("employee_id", out var empId) && !string.IsNullOrWhiteSpace(empId))
                    parameters["employeeId"] = empId;
                break;
            case "ApprouverDemandeCongesAsync":
                if (!entities.TryGetValue("leave_request_id", out var lrId) || string.IsNullOrWhiteSpace(lrId))
                    missingFields.Add("ID de la demande de congé");
                else
                    parameters["leaveRequestId"] = lrId;
                break;
            case "RevoquerAccesITAsync":
                if (!entities.TryGetValue("employee_id", out var revEmp) || string.IsNullOrWhiteSpace(revEmp))
                    missingFields.Add("ID de l'employé à révoquer");
                else
                    parameters["employeeId"] = revEmp;
                break;
            case "RechercherInformationRagAsync":
                if (!entities.TryGetValue("query", out var query) || string.IsNullOrWhiteSpace(query))
                    missingFields.Add("Sujet de la recherche");
                else
                    parameters["query"] = query;
                break;
            case "DemanderAvanceSalaireAsync":
                // L'employé cible est optionnel (s'il est nul, le Worker utilisera le JWT de l'utilisateur courant)
                if (entities.TryGetValue("employee_id", out var empAdvId) && !string.IsNullOrWhiteSpace(empAdvId))
                    parameters["employeeId"] = empAdvId;

                // Le montant est STRICTEMENT OBLIGATOIRE
                if (!entities.TryGetValue("amount", out var amountStr) || string.IsNullOrWhiteSpace(amountStr))
                    missingFields.Add("montant souhaité de l'avance");
                else
                    parameters["amount"] = amountStr; // Le JsonSerializer de MafToolBase gérera la conversion string -> decimal
                break;
            case "GenererChecklistAsync":
                if (entities.TryGetValue("category", out var cat) && !string.IsNullOrWhiteSpace(cat))
                {
                    var knownCategories = new[] { "Administratif", "IT", "RH", "Management" };
                    if (!knownCategories.Contains(cat, StringComparer.OrdinalIgnoreCase))
                        missingFields.Add($"catégorie inconnue (disponibles : {string.Join(", ", knownCategories)})");
                    else
                        parameters["category"] = cat;
                }
                break;
            case "PoserDemandeCongesAsync":
                if (entities.TryGetValue("employee_id", out var empLeaveId) && !string.IsNullOrWhiteSpace(empLeaveId))
                    parameters["employeeId"] = empLeaveId;

                if (entities.TryGetValue("date_reference", out var dateRef) && !string.IsNullOrWhiteSpace(dateRef))
                    parameters["dateReference"] = dateRef;
                else
                    missingFields.Add("date de début du congé");

                if (entities.TryGetValue("days", out var days) && !string.IsNullOrWhiteSpace(days))
                    parameters["days"] = days;
                else
                    missingFields.Add("nombre de jours de congés");
                break;
        }

        if (missingFields.Any())
            return new ValidationResult(false, $"Paramètres obligatoires manquants : {string.Join(", ", missingFields)}.", default);

        return new ValidationResult(true, null, JsonSerializer.SerializeToElement(parameters));
    }
}

using System.Text.Json.Serialization;
using Agirh.Core.Interfaces;
using Agirh.Domain.Entities;
using Agirh.Domain.Interfaces;

namespace Agirh.Infrastructure.MAF;

public sealed record GenererChecklistInput([property: JsonPropertyName("category")] string? Categorie = null);

public sealed class ChecklistFunctions : MafToolBase<GenererChecklistInput>
{
    private readonly IChecklistRepository _checklistRepo;

    public ChecklistFunctions(IChecklistRepository checklistRepo) => _checklistRepo = checklistRepo;
    public override string Name => "GenererChecklistAsync";
    public override string Description => "Génère la checklist d'onboarding pour un nouveau collaborateur, avec possibilité de filtrer par catégorie (Administratif, IT, RH, Management)";
    public override RoleFlags RequiredRoles => RoleFlags.Admin | RoleFlags.Manager;

    protected override async Task<string> ExecuteTypedAsync(GenererChecklistInput input, string? requestingUserId = null, CancellationToken ct = default)
    {
        var items = string.IsNullOrEmpty(input.Categorie)
            ? await _checklistRepo.GetAllAsync()
            : await _checklistRepo.GetByCategoryAsync(input.Categorie);

        var itemList = (items ?? Enumerable.Empty<ChecklistItem>()).ToList();

        if (itemList.Count == 0)
            return string.IsNullOrEmpty(input.Categorie)
                ? "Aucune tâche de checklist trouvée."
                : $"Aucune tâche trouvée pour la catégorie '{input.Categorie}'. Catégories disponibles : Administratif, IT, RH, Management.";

        var result = string.IsNullOrEmpty(input.Categorie)
            ? $"Voici la checklist d'onboarding complète ({itemList.Count} tâches) :\n"
            : $"Voici la checklist pour la catégorie '{input.Categorie}' ({itemList.Count} tâches) :\n";

        foreach (var item in itemList.OrderBy(i => i.Category).ThenBy(i => i.Order))
        {
            result += $"- [{item.Category}] {item.Title} {(item.IsRequired ? "(obligatoire)" : "(optionnel)")}\n";
        }
        return result;
    }
}

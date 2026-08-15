using Agirh.Domain;

namespace Agirh.Domain.Entities;

public class ChecklistItemStatus
{
    public Guid Id { get; }
    public Guid TemplateItemId { get; }
    public string Libelle { get; }
    public ItemEtat Etat { get; private set; }
    public string? Commentaire { get; private set; }
    public Guid? CochePar { get; private set; }
    public DateTime? DateCoche { get; private set; }

    public ChecklistItemStatus(Guid id, Guid templateItemId, string libelle)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du statut d'item est requis.", nameof(id));
        if (templateItemId == Guid.Empty)
            throw new ArgumentException("L'identifiant de l'item de template est requis.", nameof(templateItemId));
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé ne peut pas être vide.", nameof(libelle));

        Id = id;
        TemplateItemId = templateItemId;
        Libelle = libelle.Trim();
        Etat = ItemEtat.EnAttente;
    }

    public void Cocher(ItemEtat etat, Guid cochePar, DateTime dateCoche, string? commentaire)
    {
        if (etat == ItemEtat.EnAttente)
            throw new ArgumentException("Cocher un item doit produire un état Ok ou Ko, jamais EnAttente.", nameof(etat));
        if (cochePar == Guid.Empty)
            throw new ArgumentException("Le compte ayant coché l'item est requis.", nameof(cochePar));

        Etat = etat;
        CochePar = cochePar;
        DateCoche = dateCoche;
        Commentaire = string.IsNullOrWhiteSpace(commentaire) ? null : commentaire.Trim();
    }
}

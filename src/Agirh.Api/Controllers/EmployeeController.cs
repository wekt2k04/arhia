using Agirh.Api.Auth;
using Agirh.Core.Security;
using Agirh.Core.UseCases;
using Agirh.Domain;
using Agirh.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agirh.Api.Controllers;

public record CreerFicheCollaborateurRequest(
    string Matricule,
    string Nom,
    string Prenom,
    string Poste,
    Guid PoleId,
    TypeContrat TypeContrat,
    DateTime DateIntegration);

public record CollaborateurResponse(
    Guid Id,
    string Matricule,
    string Nom,
    string Prenom,
    string Poste,
    Guid PoleId,
    TypeContrat TypeContrat,
    DateTime DateIntegration);

[ApiController]
[Route("api/collaborateurs")]
[Authorize]
public class EmployeeController : ControllerBase
{
    private readonly CreerFicheCollaborateurUseCase _creerFiche;
    private readonly ICurrentUserAccessor _currentUser;

    public EmployeeController(CreerFicheCollaborateurUseCase creerFiche, ICurrentUserAccessor currentUser)
    {
        _creerFiche = creerFiche;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<CollaborateurResponse>> Creer(CreerFicheCollaborateurRequest request, CancellationToken ct)
    {
        var acteur = await _currentUser.ObtenirActeurAsync(ct);

        try
        {
            var collaborateur = await _creerFiche.ExecuterAsync(
                acteur,
                new Matricule(request.Matricule),
                request.Nom,
                request.Prenom,
                request.Poste,
                request.PoleId,
                request.TypeContrat,
                request.DateIntegration,
                ct);

            return Ok(new CollaborateurResponse(
                collaborateur.Id, collaborateur.Matricule.Valeur, collaborateur.Nom, collaborateur.Prenom,
                collaborateur.Poste, collaborateur.PoleId, collaborateur.TypeContrat, collaborateur.DateIntegration));
        }
        catch (AccesRefuseException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Domain.ValueObjects;
using Agirh.Infrastructure.Persistence;
using Agirh.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Tests.Persistence;

public class CollaborateurRepositoryTests
{
    private static DbContextOptions<AgirhDbContext> CreerOptions() =>
        new DbContextOptionsBuilder<AgirhDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task AjouterPuisObtenirParMatricule_ReconstruitLeValueObject()
    {
        var options = CreerOptions();
        var matricule = new Matricule("MAT-042");
        var collaborateur = new Collaborateur(Guid.NewGuid(), matricule, "Dupont", "Jean", "Développeur", Guid.NewGuid(), TypeContrat.CDI, new DateTime(2026, 1, 15));

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new CollaborateurRepository(dbEcriture).AjouterAsync(collaborateur);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new CollaborateurRepository(dbLecture).ObtenirParMatriculeAsync(matricule);

        recharge.Should().NotBeNull();
        recharge!.Matricule.Should().Be(matricule);
        recharge.Matricule.Valeur.Should().Be("MAT-042");
        recharge.TypeContrat.Should().Be(TypeContrat.CDI);
    }

    [Fact]
    public async Task EnregistrerDepart_PuisRecharger_PersisteLaDateDeDepart()
    {
        var options = CreerOptions();
        var collaborateur = new Collaborateur(Guid.NewGuid(), new Matricule("MAT-099"), "Martin", "Marie", "RH", Guid.NewGuid(), TypeContrat.CDI, new DateTime(2020, 1, 1));
        collaborateur.EnregistrerDepart(new DateTime(2026, 8, 15));

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new CollaborateurRepository(dbEcriture).AjouterAsync(collaborateur);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new CollaborateurRepository(dbLecture).ObtenirParIdAsync(collaborateur.Id);

        recharge!.DateDepart.Should().Be(new DateTime(2026, 8, 15));
    }
}

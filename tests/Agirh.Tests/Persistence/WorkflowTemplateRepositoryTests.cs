using Agirh.Domain;
using Agirh.Domain.Entities;
using Agirh.Infrastructure.Persistence;
using Agirh.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Agirh.Tests.Persistence;

public class WorkflowTemplateRepositoryTests
{
    private static DbContextOptions<AgirhDbContext> CreerOptions() =>
        new DbContextOptionsBuilder<AgirhDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task AjouterPuisObtenirParId_ReconstruitLeGrapheSectionsEtItems()
    {
        var options = CreerOptions();
        var itemCommun = new TemplateItem(Guid.NewGuid(), "Bitlocker activé", 0);
        var itemCdiSeulement = new TemplateItem(Guid.NewGuid(), "Processus disciplinaire signé", 1, new[] { TypeContrat.CDI });
        var section = new TemplateSection(Guid.NewGuid(), "IT", 0, new[] { itemCommun, itemCdiSeulement });
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), new[] { section }, new DateTime(2026, 8, 15));
        var verificateur = Guid.NewGuid();
        template.Soumettre();
        template.Verifier(verificateur);
        template.Approuver(Guid.NewGuid());

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            await new WorkflowTemplateRepository(dbEcriture).AjouterAsync(template);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var recharge = await new WorkflowTemplateRepository(dbLecture).ObtenirParIdAsync(template.Id);

        recharge.Should().NotBeNull();
        recharge!.Statut.Should().Be(TemplateStatut.Approuve);
        recharge.VerificateurId.Should().Be(verificateur);
        recharge.Sections.Should().ContainSingle();
        recharge.Sections[0].Items.Should().HaveCount(2);
        recharge.Sections[0].Items.Should().Contain(i =>
            i.Libelle == itemCdiSeulement.Libelle && i.ConditionsTypeContrat.Contains(TypeContrat.CDI));
        recharge.Sections[0].Items.Should().Contain(i =>
            i.Libelle == itemCommun.Libelle && i.ConditionsTypeContrat.Count == 0);
    }

    [Fact]
    public async Task ObtenirDernierApprouveAsync_IgnoreLesTemplatesNonApprouves()
    {
        var options = CreerOptions();
        var sectionRejete = new TemplateSection(Guid.NewGuid(), "RH", 0, new[] { new TemplateItem(Guid.NewGuid(), "Item", 0) });
        var templateRejete = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), new[] { sectionRejete }, new DateTime(2026, 1, 1));
        templateRejete.Soumettre();
        templateRejete.Rejeter(Guid.NewGuid(), "motif");

        var sectionApprouve = new TemplateSection(Guid.NewGuid(), "RH", 0, new[] { new TemplateItem(Guid.NewGuid(), "Item", 0) });
        var templateApprouve = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T1", Guid.NewGuid(), new[] { sectionApprouve }, new DateTime(2026, 2, 1));
        templateApprouve.Soumettre();
        templateApprouve.Verifier(Guid.NewGuid());
        templateApprouve.Approuver(Guid.NewGuid());

        await using (var dbEcriture = new AgirhDbContext(options))
        {
            var repo = new WorkflowTemplateRepository(dbEcriture);
            await repo.AjouterAsync(templateRejete);
            await repo.AjouterAsync(templateApprouve);
        }

        await using var dbLecture = new AgirhDbContext(options);
        var resultat = await new WorkflowTemplateRepository(dbLecture).ObtenirDernierApprouveAsync(WorkflowType.Onboarding);

        resultat.Should().NotBeNull();
        resultat!.Version.Should().Be("T1");
    }
}

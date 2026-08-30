using Arhia.Domain;
using Arhia.Domain.Entities;
using Arhia.Infrastructure.Persistence;
using Arhia.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Arhia.Tests.Persistence;

public class WorkflowTemplateRepositoryTests
{
    private static DbContextOptions<ArhiaDbContext> CreerOptions() =>
        new DbContextOptionsBuilder<ArhiaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task AddThenGetById_RebuildsTheSectionsAndItemsGraph()
    {
        var options = CreerOptions();
        var itemCommun = new TemplateItem(Guid.NewGuid(), "Bitlocker activé", 0);
        var itemCdiSeulement = new TemplateItem(Guid.NewGuid(), "Processus disciplinaire signé", 1, new[] { ContractType.CDI });
        var section = new TemplateSection(Guid.NewGuid(), "IT", 0, new[] { itemCommun, itemCdiSeulement });
        var template = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), new[] { section }, new DateTime(2026, 8, 15));
        var verifier = Guid.NewGuid();
        template.Submit();
        template.Verify(verifier);
        template.Approve(Guid.NewGuid());

        await using (var dbEcriture = new ArhiaDbContext(options))
        {
            await new WorkflowTemplateRepository(dbEcriture).AddAsync(template);
        }

        await using var dbLecture = new ArhiaDbContext(options);
        var recharge = await new WorkflowTemplateRepository(dbLecture).GetByIdAsync(template.Id);

        recharge.Should().NotBeNull();
        recharge!.Status.Should().Be(TemplateStatus.Approved);
        recharge.VerifierId.Should().Be(verifier);
        recharge.Sections.Should().ContainSingle();
        recharge.Sections[0].Items.Should().HaveCount(2);
        recharge.Sections[0].Items.Should().Contain(i =>
            i.Label == itemCdiSeulement.Label && i.ApplicableContractTypes.Contains(ContractType.CDI));
        recharge.Sections[0].Items.Should().Contain(i =>
            i.Label == itemCommun.Label && i.ApplicableContractTypes.Count == 0);
    }

    [Fact]
    public async Task GetLastApprovedAsync_IgnoresNonApprovedTemplates()
    {
        var options = CreerOptions();
        var sectionRejete = new TemplateSection(Guid.NewGuid(), "RH", 0, new[] { new TemplateItem(Guid.NewGuid(), "Item", 0) });
        var templateRejete = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T0", Guid.NewGuid(), new[] { sectionRejete }, new DateTime(2026, 1, 1));
        templateRejete.Submit();
        templateRejete.Reject(Guid.NewGuid(), "motif");

        var sectionApprouve = new TemplateSection(Guid.NewGuid(), "RH", 0, new[] { new TemplateItem(Guid.NewGuid(), "Item", 0) });
        var templateApprouve = new WorkflowTemplate(Guid.NewGuid(), WorkflowType.Onboarding, "T1", Guid.NewGuid(), new[] { sectionApprouve }, new DateTime(2026, 2, 1));
        templateApprouve.Submit();
        templateApprouve.Verify(Guid.NewGuid());
        templateApprouve.Approve(Guid.NewGuid());

        await using (var dbEcriture = new ArhiaDbContext(options))
        {
            var repo = new WorkflowTemplateRepository(dbEcriture);
            await repo.AddAsync(templateRejete);
            await repo.AddAsync(templateApprouve);
        }

        await using var dbLecture = new ArhiaDbContext(options);
        var resultat = await new WorkflowTemplateRepository(dbLecture).GetLastApprovedAsync(WorkflowType.Onboarding);

        resultat.Should().NotBeNull();
        resultat!.Version.Should().Be("T1");
    }
}

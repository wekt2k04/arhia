using System.Text.Json.Serialization;
using Agirh.Core.Interfaces;
using Agirh.Core.Services;
using Agirh.Domain.Interfaces;

namespace Agirh.Infrastructure.MAF;

public sealed record SalaryAdvanceInput(
    [property: JsonPropertyName("employeeId")] string EmployeeId,
    [property: JsonPropertyName("amount")] decimal Amount);

public sealed class DemanderAvanceSalaireAsync : MafToolBase<SalaryAdvanceInput>
{
    private readonly ICreateSalaryAdvanceRequest _useCase;

    public DemanderAvanceSalaireAsync(
        IPayrollProfileRepository payrollRepo,
        ISalaryAdvanceRepository advanceRepo,
        IUnitOfWork unitOfWork)
    {
        _useCase = new CreateSalaryAdvanceRequest(payrollRepo, advanceRepo, unitOfWork);
    }

    public override string Name => "DemanderAvanceSalaireAsync";
    public override string Description => "Demande une avance sur salaire pour un collaborateur, sous réserve du plafond de 50% du salaire net et de l'absence de demande en attente.";
    public override RoleFlags RequiredRoles => RoleFlags.All;

    protected override Task<string> ExecuteTypedAsync(SalaryAdvanceInput input, string? requestingUserId = null, CancellationToken ct = default)
        => _useCase.ExecuteAsync(input.EmployeeId, input.Amount, requestingUserId ?? "", ct);
}

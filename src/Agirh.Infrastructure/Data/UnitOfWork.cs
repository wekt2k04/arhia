using Agirh.Core.Interfaces;
using Agirh.Domain.Interfaces;

namespace Agirh.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(
        AppDbContext context,
        IEmployeeRepository employees,
        ILeaveRequestRepository leaveRequests,
        IPayrollProfileRepository payrollProfiles,
        ISalaryAdvanceRepository salaryAdvances,
        IAgentConversationRepository agentConversations)
    {
        _context = context;
        Employees = employees;
        LeaveRequests = leaveRequests;
        PayrollProfiles = payrollProfiles;
        SalaryAdvances = salaryAdvances;
        AgentConversations = agentConversations;
    }

    public IEmployeeRepository Employees { get; }
    public ILeaveRequestRepository LeaveRequests { get; }
    public IPayrollProfileRepository PayrollProfiles { get; }
    public ISalaryAdvanceRepository SalaryAdvances { get; }
    public IAgentConversationRepository AgentConversations { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

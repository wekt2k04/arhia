using Agirh.Domain.Interfaces;

namespace Agirh.Core.Interfaces;

public interface IUnitOfWork
{
    IEmployeeRepository Employees { get; }
    ILeaveRequestRepository LeaveRequests { get; }
    IPayrollProfileRepository PayrollProfiles { get; }
    ISalaryAdvanceRepository SalaryAdvances { get; }
    IAgentConversationRepository AgentConversations { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

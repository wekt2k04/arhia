using Arhia.Domain.Entities;

namespace Arhia.Core.Ports;

public sealed record WorkflowInstanceWithEmployee(WorkflowInstance Instance, Employee Employee);

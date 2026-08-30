using Arhia.Domain.Entities;

namespace Arhia.Core.Ports;

public sealed record WorkflowInstanceDetail(WorkflowInstance Instance, Employee Employee, WorkflowTemplate? Template);

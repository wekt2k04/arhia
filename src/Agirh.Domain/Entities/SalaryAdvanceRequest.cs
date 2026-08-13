namespace Agirh.Domain.Entities;

public class SalaryAdvanceRequest
{
    private static readonly string[] ValidStatuses = { "Pending", "Approved", "Rejected" };
    private string _status = "Pending";

    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    private decimal _amountRequested;
    public decimal AmountRequested
    {
        get => _amountRequested;
        set => _amountRequested = Math.Max(0, value);
    }

    public DateTime RequestDate { get; set; } = DateTime.UtcNow;

    public string Status
    {
        get => _status;
        set => _status = ValidStatuses.Contains(value) ? value : "Pending";
    }

    public string? Reason { get; set; }
}

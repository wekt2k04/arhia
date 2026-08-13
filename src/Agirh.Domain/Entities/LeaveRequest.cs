namespace Agirh.Domain.Entities;

public class LeaveRequest
{
    private static readonly string[] ValidStatuses = { "Pending", "Approved", "Rejected", "Cancelled" };
    private static readonly string[] ValidTypes = { "Conges", "CET", "Maladie" };
    private string _status = "Pending";
    private string _type = "Conges";

    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    private DateTime _startDate;
    public DateTime StartDate
    {
        get => _startDate;
        set => _startDate = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value;
    }

    private DateTime _endDate;
    public DateTime EndDate
    {
        get => _endDate;
        set => _endDate = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value;
    }

    public string Status
    {
        get => _status;
        set => _status = ValidStatuses.Contains(value) ? value : "Pending";
    }

    public string Type
    {
        get => _type;
        set => _type = ValidTypes.Contains(value) ? value : "Conges";
    }

    public decimal DaysRequested { get; set; }
    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool CanApprove => Status == "Pending";
    public decimal ValidDays => EndDate > StartDate ? (decimal)(EndDate - StartDate).TotalDays : 0;
}

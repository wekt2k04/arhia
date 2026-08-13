namespace Agirh.Domain.Entities;

public class PayrollProfile
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    private decimal _netSalary;
    public decimal NetSalary
    {
        get => _netSalary;
        set => _netSalary = Math.Max(0, value);
    }

    public string Iban { get; set; } = string.Empty;

    private decimal _maxAdvancePercentage = 0.50m;
    public decimal MaxAdvancePercentage
    {
        get => _maxAdvancePercentage;
        set => _maxAdvancePercentage = value is > 0 and <= 1 ? value : 0.50m;
    }
}

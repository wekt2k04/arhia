namespace Agirh.Api.Dtos;

public class SalaryAdvanceResponseDto
{
    public Guid Id { get; set; }
    public decimal AmountRequested { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public Guid EmployeeId { get; set; }
}

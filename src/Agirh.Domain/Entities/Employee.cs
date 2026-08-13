using System.Text.RegularExpressions;

namespace Agirh.Domain.Entities;

public class Employee
{
    private static readonly string[] ValidRoles = { "Admin", "Manager", "Collaborator" };
    private string _role = "Collaborator";
    private string _email = string.Empty;

    public Guid Id { get; set; }

    public string FirstName
    {
        get => _firstName;
        set => _firstName = value?.Trim() ?? string.Empty;
    }
    private string _firstName = string.Empty;

    public string LastName
    {
        get => _lastName;
        set => _lastName = value?.Trim() ?? string.Empty;
    }
    private string _lastName = string.Empty;

    public string Email
    {
        get => _email;
        set
        {
            var trimmed = value?.Trim().ToLowerInvariant() ?? string.Empty;
            _email = trimmed;
        }
    }

    public string Role
    {
        get => _role;
        set
        {
            var trimmed = value?.Trim() ?? string.Empty;
            _role = ValidRoles.Contains(trimmed) ? trimmed : "Collaborator";
        }
    }

    public string? PasswordHash { get; set; }
    public Guid? ManagerId { get; set; }
    public Employee? Manager { get; set; }

    private decimal _leaveBalance;
    public decimal LeaveBalance
    {
        get => _leaveBalance;
        set => _leaveBalance = Math.Max(0, value);
    }

    private decimal _compteEpargneTemps;
    public decimal CompteEpargneTemps
    {
        get => _compteEpargneTemps;
        set => _compteEpargneTemps = Math.Max(0, value);
    }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Employee> Subordinates { get; set; } = new List<Employee>();
}

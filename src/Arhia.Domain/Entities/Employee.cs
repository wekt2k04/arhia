using Arhia.Domain;
using Arhia.Domain.ValueObjects;

namespace Arhia.Domain.Entities;

public class Employee
{
    public Guid Id { get; }
    public EmployeeNumber EmployeeNumber { get; }
    public string LastName { get; private set; }
    public string FirstName { get; private set; }
    public string JobTitle { get; private set; }
    public Guid DepartmentId { get; private set; }
    public ContractType ContractType { get; private set; }
    public DateTime StartDate { get; }
    public DateTime? DepartureDate { get; private set; }
    public Guid? UserAccountId { get; private set; }

    public Employee(
        Guid id,
        EmployeeNumber employeeNumber,
        string lastName,
        string firstName,
        string jobTitle,
        Guid departmentId,
        ContractType contractType,
        DateTime startDate)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("L'identifiant du collaborateur est requis.", nameof(id));
        if (departmentId == Guid.Empty)
            throw new ArgumentException("Le pôle du collaborateur est requis.", nameof(departmentId));

        Id = id;
        EmployeeNumber = employeeNumber;
        LastName = ValidateText(lastName, nameof(lastName));
        FirstName = ValidateText(firstName, nameof(firstName));
        JobTitle = ValidateText(jobTitle, nameof(jobTitle));
        DepartmentId = departmentId;
        ContractType = contractType;
        StartDate = startDate;
    }

    public void RecordDeparture(DateTime departureDate)
    {
        if (departureDate < StartDate)
            throw new InvalidOperationException("La date de départ ne peut pas précéder la date d'intégration.");
        DepartureDate = departureDate;
    }

    public void ChangeDepartment(Guid newDepartmentId)
    {
        if (newDepartmentId == Guid.Empty)
            throw new ArgumentException("Le nouveau pôle est requis.", nameof(newDepartmentId));
        DepartmentId = newDepartmentId;
    }

    public void LinkUserAccount(Guid userAccountId)
    {
        if (userAccountId == Guid.Empty)
            throw new ArgumentException("L'identifiant de compte est requis.", nameof(userAccountId));
        UserAccountId = userAccountId;
    }

    private static string ValidateText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("La valeur ne peut pas être vide.", parameterName);
        return value.Trim();
    }
}

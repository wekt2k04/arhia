namespace Agirh.Api.Dtos;

public sealed record EmployeeResponseDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    bool IsActive,
    decimal LeaveBalance,
    decimal CompteEpargneTemps);

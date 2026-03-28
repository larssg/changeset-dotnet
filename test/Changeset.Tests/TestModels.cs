namespace Changeset.Tests;

public class User
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public DateTime? BirthDate { get; set; }
    public Guid? ExternalId { get; set; }
    public decimal Salary { get; set; }
    public UserRole Role { get; set; }
}

public enum UserRole
{
    Guest,
    Member,
    Admin
}

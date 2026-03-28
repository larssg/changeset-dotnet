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

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
}

public class UserWithAddress
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public Address? Address { get; set; }
}

public enum UserRole
{
    Guest,
    Member,
    Admin
}

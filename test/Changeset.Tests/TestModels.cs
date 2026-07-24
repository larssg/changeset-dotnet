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

public class Event
{
    public string Name { get; set; } = "";
    public DateOnly? Date { get; set; }
    public TimeOnly? StartTime { get; set; }
}

public class Immutable
{
    public string Name { get; set; } = "";
    public string ReadOnlyTag { get; init; } = "";
}

public class Order
{
    public string Id { get; set; } = "";
    public Address? Address { get; set; }
}

public class SequenceModel
{
    public IEnumerable<int> Items { get; set; } = [];
}

public class NoDefaultConstructor
{
    public NoDefaultConstructor(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    public string Email { get; set; } = "";
}

public class CoercionTarget
{
    public int IntValue { get; set; }
    public long LongValue { get; set; }
    public double DoubleValue { get; set; }
    public float FloatValue { get; set; }
    public decimal DecimalValue { get; set; }
    public bool BoolValue { get; set; }
    public DateTime DateTimeValue { get; set; }
    public DateTimeOffset DateTimeOffsetValue { get; set; }
    public DateOnly DateOnlyValue { get; set; }
    public TimeOnly TimeOnlyValue { get; set; }
    public Guid GuidValue { get; set; }
    public UserRole EnumValue { get; set; }
    public int? NullableIntValue { get; set; }
    public decimal? NullableDecimalValue { get; set; }
    public DateTime? NullableDateTimeValue { get; set; }
    public Guid? NullableGuidValue { get; set; }
    public UserRole? NullableEnumValue { get; set; }
    public string StringValue { get; set; } = "";
}

public enum UserRole
{
    Guest,
    Member,
    Admin
}

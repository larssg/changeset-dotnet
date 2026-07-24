namespace Changeset.Benchmarks;

/// <summary>
/// Model applied through the generated applier emitted for [ChangesetTarget].
/// </summary>
[ChangesetTarget]
public class GeneratedUser
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public int Age { get; set; }
    public string? Bio { get; set; }
}

/// <summary>
/// Identical model without [ChangesetTarget], applied through the reflection fallback.
/// </summary>
public class ReflectionUser
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public int Age { get; set; }
    public string? Bio { get; set; }
}

using BenchmarkDotNet.Attributes;
using Changeset.Validators;

namespace Changeset.Benchmarks;

[MemoryDiagnoser]
public class CastAndValidateBenchmarks
{
    private static readonly Dictionary<string, object?> Params = new()
    {
        ["Name"] = "Ada Lovelace",
        ["Email"] = "ada@example.com",
        ["Age"] = "36",
        ["Bio"] = "Mathematician and writer.",
    };

    private static readonly string[] Fields = ["Name", "Email", "Age", "Bio"];

    private Changeset<ReflectionUser> _changeset = null!;

    [GlobalSetup]
    public void Setup() => _changeset = Changeset<ReflectionUser>.Cast(Params, Fields);

    [Benchmark]
    public Changeset<ReflectionUser> Cast() =>
        Changeset<ReflectionUser>.Cast(Params, Fields);

    [Benchmark]
    public Changeset<ReflectionUser> Validate() =>
        _changeset
            .ValidateRequired(["Name", "Email"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$")
            .ValidateLength("Name", min: 1, max: 200)
            .ValidateNumber("Age", greaterThanOrEqual: 0);

    [Benchmark]
    public Changeset<ReflectionUser> CastAndValidate() =>
        Changeset<ReflectionUser>.Cast(Params, Fields)
            .ValidateRequired(["Name", "Email"])
            .ValidateFormat("Email", @"^[^@]+@[^@]+\.[^@]+$")
            .ValidateLength("Name", min: 1, max: 200)
            .ValidateNumber("Age", greaterThanOrEqual: 0);
}

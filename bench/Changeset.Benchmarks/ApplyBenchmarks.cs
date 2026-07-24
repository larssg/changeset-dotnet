using BenchmarkDotNet.Attributes;

namespace Changeset.Benchmarks;

/// <summary>
/// Compares the generated applier ([ChangesetTarget]) with the reflection fallback
/// for both insert (no existing data) and update (shallow clone of existing data).
/// </summary>
[MemoryDiagnoser]
public class ApplyBenchmarks
{
    private static readonly Dictionary<string, object?> Params = new()
    {
        ["Name"] = "Ada Lovelace",
        ["Email"] = "ada@example.com",
        ["Age"] = "36",
    };

    private static readonly string[] Fields = ["Name", "Email", "Age"];

    private Changeset<GeneratedUser> _generatedInsert = null!;
    private Changeset<GeneratedUser> _generatedUpdate = null!;
    private Changeset<ReflectionUser> _reflectionInsert = null!;
    private Changeset<ReflectionUser> _reflectionUpdate = null!;

    [GlobalSetup]
    public void Setup()
    {
        var existingGenerated = new GeneratedUser { Name = "Ada", Email = "old@example.com", Age = 35, Bio = "..." };
        var existingReflection = new ReflectionUser { Name = "Ada", Email = "old@example.com", Age = 35, Bio = "..." };

        _generatedInsert = Changeset<GeneratedUser>.Cast(Params, Fields);
        _generatedUpdate = Changeset<GeneratedUser>.Cast(existingGenerated, Params, Fields);
        _reflectionInsert = Changeset<ReflectionUser>.Cast(Params, Fields);
        _reflectionUpdate = Changeset<ReflectionUser>.Cast(existingReflection, Params, Fields);
    }

    [Benchmark(Baseline = true)]
    public ReflectionUser ReflectionInsert() => _reflectionInsert.ApplyChanges();

    [Benchmark]
    public GeneratedUser GeneratedInsert() => _generatedInsert.ApplyChanges();

    [Benchmark]
    public ReflectionUser ReflectionUpdate() => _reflectionUpdate.ApplyChanges();

    [Benchmark]
    public GeneratedUser GeneratedUpdate() => _generatedUpdate.ApplyChanges();
}

using BenchmarkDotNet.Running;
using Changeset;
using Changeset.Benchmarks;

// Guard the premise of ApplyBenchmarks: GeneratedUser must go through the
// generated applier and ReflectionUser through the reflection fallback.
if (ChangesetApplierRegistry.Get<GeneratedUser>() is null)
    throw new InvalidOperationException(
        "No generated applier is registered for GeneratedUser; the source generator did not run.");
if (ChangesetApplierRegistry.Get<ReflectionUser>() is not null)
    throw new InvalidOperationException(
        "ReflectionUser unexpectedly has a registered applier; it must use the reflection path.");

BenchmarkSwitcher.FromAssembly(typeof(ApplyBenchmarks).Assembly).Run(args);

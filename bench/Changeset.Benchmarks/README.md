# Changeset.Benchmarks

BenchmarkDotNet benchmarks for casting, validation, and changeset application
(generated applier vs the reflection fallback).

Run all benchmarks:

```sh
dotnet run -c Release
```

Run a single suite:

```sh
dotnet run -c Release -- --filter '*ApplyBenchmarks*'
```

The benchmarks are built (but not executed) by CI to keep them compiling.

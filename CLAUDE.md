# CLAUDE.md

Guidance for AI coding agents working in this repository.

## Project overview

Changeset.NET brings an Ecto-style cast → validate → apply pipeline to C#. It
turns untrusted, untyped input into an immutable description of proposed
changes before anything touches a model or database. Targets .NET 10;
`TreatWarningsAsErrors` is on repo-wide.

- `src/Changeset` — core library (casting, validation, errors, apply).
- `src/Changeset.EntityFramework` — EF Core / ASP.NET Core integration.
- `src/Changeset.Generators` — source generator (generated appliers,
  string-field diagnostics).
- `test/` — one xUnit test project per `src/` project, plus
  `Changeset.PublicApi.Tests` (public API approval tests; if a test fails after
  an intentional API change, copy the `.received.txt` over the approved file
  in `test/Changeset.PublicApi.Tests/approved/` and commit it).
- `bench/Changeset.Benchmarks` — BenchmarkDotNet benchmarks (built by CI, not
  executed).
- `samples/Changeset.Sample.WebApi` — sample ASP.NET Core app.
- `docs/` — MkDocs documentation sources (`mkdocs.yml` at repo root).
- `tools/generate-llm-docs.py` — generates `docs/llms.txt` and
  `docs/llms-full.txt` from the canonical docs pages.

## Build and test

```sh
dotnet build          # warnings are errors
dotnet test           # full suite, all test projects
```

CI additionally runs `dotnet test /p:CollectCoverage=true`, which enforces
per-package line-coverage thresholds (set in the test project files); a drop
below a threshold fails the build.

## Testing requirements

- High test coverage is required. Every behavior change or new feature must
  come with tests in the matching project under `test/`; bug fixes need a
  regression test that fails without the fix.
- Run the **full** test suite (`dotnet test` from the repo root) before
  opening a pull request — not just the project you touched. The source
  generator and EF Core packages depend on core behavior, so changes in
  `src/Changeset` can break the other test projects.

## Documentation

Docs live in `docs/` and must be kept up to date: when you change public API
or observable behavior, update the affected pages (and `docs/api-reference.md`
for API changes) in the same PR.

`docs/llms.txt` and `docs/llms-full.txt` are **generated** — never edit them
by hand. After any docs change, regenerate them:

```sh
python3 tools/generate-llm-docs.py          # rewrites llms.txt / llms-full.txt
python3 tools/generate-llm-docs.py --check  # what CI runs; fails if stale
```

CI fails if the generated files are out of sync. If you add a new docs page,
also add it to the `PAGES` tuple in `tools/generate-llm-docs.py` and to the
nav in `mkdocs.yml`, then regenerate.

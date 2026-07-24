using System.Reflection;
using System.Runtime.CompilerServices;
using PublicApiGenerator;

namespace Changeset.PublicApi.Tests;

/// <summary>
/// Approval tests for the public API surface of the shipped library packages.
/// A failure means the public API changed: review the diff between the
/// .received.txt and the approved file, and if the change is intentional,
/// copy the received contents over the approved file and commit it.
/// </summary>
public class PublicApiApprovalTests
{
    public static TheoryData<string> AssemblyNames { get; } = new()
    {
        "Changeset",
        "Changeset.EntityFramework",
    };

    [Theory]
    [MemberData(nameof(AssemblyNames))]
    public void Public_api_matches_approved_baseline(string assemblyName)
    {
        var assembly = assemblyName switch
        {
            "Changeset" => typeof(ChangesetError).Assembly,
            "Changeset.EntityFramework" => typeof(EntityFramework.DbContextExtensions).Assembly,
            _ => throw new ArgumentOutOfRangeException(nameof(assemblyName)),
        };

        var actual = Normalize(assembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false,
        }));

        var approvedPath = Path.Combine(ApprovedDirectory(), $"{assemblyName}.txt");
        var approved = File.Exists(approvedPath) ? Normalize(File.ReadAllText(approvedPath)) : "";

        if (actual != approved)
        {
            var receivedPath = Path.Combine(ApprovedDirectory(), $"{assemblyName}.received.txt");
            File.WriteAllText(receivedPath, actual);
            Assert.Fail(
                $"The public API of {assemblyName} does not match the approved baseline.\n" +
                $"Review the diff between:\n  {receivedPath}\n  {approvedPath}\n" +
                "If the change is intentional, replace the approved file with the received " +
                "file and commit it.");
        }
    }

    private static string Normalize(string api) =>
        api.Replace("\r\n", "\n").TrimEnd() + "\n";

    private static string ApprovedDirectory([CallerFilePath] string sourcePath = "") =>
        Path.Combine(Path.GetDirectoryName(sourcePath)!, "approved");
}

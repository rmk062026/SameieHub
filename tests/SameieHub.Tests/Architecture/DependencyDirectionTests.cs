using System.Reflection;

namespace SameieHub.Tests.Architecture;

/// <summary>
/// Guards the dependency direction described in docs/ARCHITECTURE.md:
/// Web -> Infrastructure -> Domain, and Domain depends on nothing of ours.
/// </summary>
public class DependencyDirectionTests
{
    private static readonly string[] ForbiddenInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Data.Sqlite",
        "Microsoft.AspNetCore",
        "Microsoft.JSInterop",
        "SameieHub.Infrastructure",
        "SameieHub.Web",
    ];

    [Fact]
    public void Domain_DoesNotReferenceInfrastructureOrFrameworks()
    {
        var references = ReferencedNames("SameieHub.Domain");

        Assert.DoesNotContain(references, name =>
            ForbiddenInDomain.Any(forbidden => name.StartsWith(forbidden, StringComparison.Ordinal)));
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceWeb()
    {
        var references = ReferencedNames("SameieHub.Infrastructure");

        Assert.DoesNotContain("SameieHub.Web", references);
    }

    private static IEnumerable<string> ReferencedNames(string assemblyName) =>
        Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty);
}

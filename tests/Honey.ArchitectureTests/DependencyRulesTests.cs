using System.Reflection;
using Honey.Domain.Model;
using Honey.Simulation;

namespace Honey.ArchitectureTests;

public sealed class DependencyRulesTests
{
    [Fact]
    public void Domain_不引用Wpf或Sqlite()
    {
        var references = GetReferenceNames(typeof(PetState).Assembly);

        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", references);
    }

    [Fact]
    public void Simulation_不引用Wpf()
    {
        var references = GetReferenceNames(typeof(PetSimulation).Assembly);

        Assert.DoesNotContain("PresentationFramework", references);
    }

    private static IReadOnlyCollection<string> GetReferenceNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
}

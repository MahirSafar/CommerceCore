using System.Reflection;
using NetArchTest.Rules;

namespace CommerceCore.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly =
        LoadAssembly("CommerceCore.Domain");

    private static readonly Assembly ApplicationAssembly =
        LoadAssembly("CommerceCore.Application");

    private static readonly Assembly PersistenceAssembly =
        LoadAssembly("CommerceCore.Persistence");

    private static readonly Assembly InfrastructureAssembly =
        LoadAssembly("CommerceCore.Infrastructure");

    private static readonly Assembly CatalogDomainAssembly =
        LoadAssembly("CommerceCore.Modules.Catalog.Domain");

    private static readonly Assembly CatalogApplicationAssembly =
        LoadAssembly("CommerceCore.Modules.Catalog.Application");

    private static readonly Assembly PlatformContractsAssembly =
        LoadAssembly("CommerceCore.Platform.Contracts");

    [Fact]
    public void CatalogDomain_Must_Not_Depend_On_Outer_Layers()
    {
        AssertHasNoDependencyOn(
            CatalogDomainAssembly,
            "CommerceCore.Application",
            "CommerceCore.Modules.Catalog.Application",
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "FluentValidation",
            "Mediator");
    }

    [Fact]
    public void PlatformContracts_Must_Not_Depend_On_Outer_Layers()
    {
        AssertHasNoDependencyOn(
            PlatformContractsAssembly,
            "CommerceCore.Domain",
            "CommerceCore.Application",
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore");
    }

    [Fact]
    public void Domain_Must_Not_Depend_On_Outer_Layers()
    {
        AssertHasNoDependencyOn(
            DomainAssembly,
            "CommerceCore.Application",
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "FluentValidation",
            "Mediator");
    }

    [Fact]
    public void Application_Must_Not_Depend_On_Outer_Layers()
    {
        AssertHasNoDependencyOn(
            ApplicationAssembly,
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "Microsoft.AspNetCore");
    }

    [Fact]
    public void Application_Must_Not_Depend_On_Catalog_Or_Platform()
    {
        AssertHasNoDependencyOn(
            ApplicationAssembly,
            "CommerceCore.Modules.Catalog.Domain",
            "CommerceCore.Modules.Catalog.Application",
            "CommerceCore.Platform.Contracts",
            "CommerceCore.Platform.ControlPlane",
            "CommerceCore.Platform.Identity");
    }

    [Fact]
    public void CatalogApplication_Must_Not_Depend_On_Implementation_Layers()
    {
        AssertHasNoDependencyOn(
            CatalogApplicationAssembly,
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "Microsoft.AspNetCore",
            "Npgsql");
    }

    [Fact]
    public void Persistence_Must_Not_Depend_On_Infrastructure_Or_Api()
    {
        AssertHasNoDependencyOn(
            PersistenceAssembly,
            "CommerceCore.Infrastructure",
            "CommerceCore.Api");
    }

    [Fact]
    public void Infrastructure_Must_Not_Depend_On_Persistence_Or_Api()
    {
        AssertHasNoDependencyOn(
            InfrastructureAssembly,
            "CommerceCore.Persistence",
            "CommerceCore.Api");
    }

    private static void AssertHasNoDependencyOn(
        Assembly assembly,
        params string[] forbiddenDependencies)
    {
        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        var failures = string.Join(
            Environment.NewLine,
            result.FailingTypes.Select(type =>
                $"- {type.FullName}: {type.Explanation}"));

        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} qatı qadağan olunmuş asılılıq daşıyır:{Environment.NewLine}{failures}");
    }

    private static Assembly LoadAssembly(string name)
        => Assembly.Load(new AssemblyName(name));
}
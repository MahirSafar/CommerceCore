using System.Reflection;
using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace CommerceCore.ArchitectureTests;

public sealed class LayerDependencyTests
{
    // The namespaces the Catalog projects actually declare. The assembly names
    // (CommerceCore.Modules.Catalog.*) are not namespace prefixes of their types.
    private const string CatalogDomainNamespace = "CommerceCore.Domain.Catalog";
    private const string CatalogUseCaseNamespace = "CommerceCore.Application.Catalog";
    private const string CatalogPortNamespace = "CommerceCore.Modules.Catalog.Application";

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

    private static readonly Assembly CatalogContractsAssembly =
        LoadAssembly("CommerceCore.Modules.Catalog.Contracts");

    private static readonly Assembly PlatformContractsAssembly =
        LoadAssembly("CommerceCore.Platform.Contracts");

    private static readonly Assembly PlatformControlPlaneAssembly =
        LoadAssembly("CommerceCore.Platform.ControlPlane");

    private static readonly Assembly PlatformIdentityAssembly =
        LoadAssembly("CommerceCore.Platform.Identity");

    private static readonly Assembly ApiAssembly =
        LoadAssembly("CommerceCore.Api");

    private static readonly Assembly OutboxWorkerAssembly =
        LoadAssembly("CommerceCore.Outbox.Worker");

    private static readonly Assembly BootstrapAssembly =
        LoadAssembly("CommerceCore.Bootstrap");

    private static readonly Assembly[] ProductionAssemblies =
    [
        DomainAssembly,
        ApplicationAssembly,
        PersistenceAssembly,
        InfrastructureAssembly,
        CatalogDomainAssembly,
        CatalogApplicationAssembly,
        CatalogContractsAssembly,
        PlatformContractsAssembly,
        PlatformControlPlaneAssembly,
        PlatformIdentityAssembly,
        ApiAssembly,
        OutboxWorkerAssembly,
        BootstrapAssembly,
    ];

    // Type-level rules: no type of the subject (or of its namespace prefix) may use a type
    // from a forbidden namespace. Every CommerceCore.* entry must name a declared namespace,
    // which ForbiddenNamespaces_EveryCommerceCoreEntry_IsDeclaredByAProductionAssembly checks.
    private static readonly NamespaceRule[] NamespaceRules =
    [
        new("Domain", DomainAssembly, null,
        [
            "CommerceCore.Application",
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "FluentValidation",
            "Mediator",
        ]),
        new("Application", ApplicationAssembly, null,
        [
            CatalogDomainNamespace,
            CatalogUseCaseNamespace,
            CatalogPortNamespace,
            "CommerceCore.Platform.Contracts",
            "CommerceCore.Platform.ControlPlane",
            "CommerceCore.Platform.Identity",
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "Microsoft.AspNetCore",
        ]),
        new("CatalogDomain", CatalogDomainAssembly, null,
        [
            "CommerceCore.Application",
            CatalogPortNamespace,
            "CommerceCore.Platform.ControlPlane",
            "CommerceCore.Platform.Identity",
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "CommerceCore.Outbox.Worker",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "FluentValidation",
            "Mediator",
        ]),
        new("CatalogApplication", CatalogApplicationAssembly, null,
        [
            "CommerceCore.Platform.ControlPlane",
            "CommerceCore.Platform.Identity",
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "CommerceCore.Outbox.Worker",
            "Microsoft.AspNetCore",
            "Npgsql",
        ]),
        new("CatalogContracts", CatalogContractsAssembly, null,
        [
            "CommerceCore.Domain",
            "CommerceCore.Application",
            CatalogPortNamespace,
            "CommerceCore.Platform.Contracts",
            "CommerceCore.Platform.ControlPlane",
            "CommerceCore.Platform.Identity",
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "CommerceCore.Outbox.Worker",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Npgsql",
            "RabbitMQ.Client",
            "Mediator",
            "FluentValidation",
        ]),
        new("PlatformContracts", PlatformContractsAssembly, null,
        [
            "CommerceCore.Domain",
            "CommerceCore.Application",
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
        ]),
        new("PlatformControlPlane", PlatformControlPlaneAssembly, null,
        [
            "CommerceCore.Domain",
            "CommerceCore.Application",
            CatalogPortNamespace,
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
        ]),
        new("PlatformIdentity", PlatformIdentityAssembly, null,
        [
            "CommerceCore.Domain",
            "CommerceCore.Application",
            CatalogPortNamespace,
            "CommerceCore.Persistence",
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "FluentValidation",
            "Mediator",
        ]),
        new("Persistence", PersistenceAssembly, null,
        [
            "CommerceCore.Infrastructure",
            "CommerceCore.Api",
        ]),
        new("Infrastructure", InfrastructureAssembly, null,
        [
            "CommerceCore.Persistence",
            "CommerceCore.Api",
        ]),
        // Endpoints translate requests into commands and queries; they never reach the database.
        new("ApiEndpoints", ApiAssembly, "CommerceCore.Api.Endpoints",
        [
            "CommerceCore.Persistence",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
        ]),
        // The worker relays outbox rows by stored type name; it runs no catalog use case.
        new("OutboxWorker", OutboxWorkerAssembly, null,
        [
            "CommerceCore.Api",
            CatalogDomainNamespace,
            CatalogUseCaseNamespace,
            CatalogPortNamespace,
        ]),
    ];

    // Assembly-level rules: the compiled assembly must not reference these assemblies at all.
    // The compiler drops unused references, so this matches actual use and cannot drift with
    // namespaces. Bootstrap declares no namespace, so only this kind of rule can cover it.
    private static readonly ReferenceRule[] ReferenceRules =
    [
        new("Application", ApplicationAssembly,
        [
            CatalogDomainAssembly,
            CatalogApplicationAssembly,
            CatalogContractsAssembly,
            PlatformContractsAssembly,
            PlatformControlPlaneAssembly,
            PlatformIdentityAssembly,
            PersistenceAssembly,
            InfrastructureAssembly,
            ApiAssembly,
            OutboxWorkerAssembly,
            BootstrapAssembly,
        ]),
        new("CatalogDomain", CatalogDomainAssembly,
        [
            ApplicationAssembly,
            CatalogApplicationAssembly,
            PlatformControlPlaneAssembly,
            PlatformIdentityAssembly,
            PersistenceAssembly,
            InfrastructureAssembly,
            ApiAssembly,
            OutboxWorkerAssembly,
            BootstrapAssembly,
        ]),
        new("CatalogApplication", CatalogApplicationAssembly,
        [
            PlatformControlPlaneAssembly,
            PlatformIdentityAssembly,
            PersistenceAssembly,
            InfrastructureAssembly,
            ApiAssembly,
            OutboxWorkerAssembly,
            BootstrapAssembly,
        ]),
        new("Api", ApiAssembly,
        [
            OutboxWorkerAssembly,
            BootstrapAssembly,
        ]),
        new("OutboxWorker", OutboxWorkerAssembly,
        [
            CatalogDomainAssembly,
            CatalogApplicationAssembly,
            ApiAssembly,
            BootstrapAssembly,
        ]),
        new("Bootstrap", BootstrapAssembly,
        [
            CatalogDomainAssembly,
            CatalogApplicationAssembly,
            ApiAssembly,
            OutboxWorkerAssembly,
        ]),
    ];

    public static TheoryData<string> NamespaceRuleNames =>
        new(NamespaceRules.Select(rule => rule.Name));

    public static TheoryData<string> ReferenceRuleNames =>
        new(ReferenceRules.Select(rule => rule.Name));

    [Theory]
    [MemberData(nameof(NamespaceRuleNames))]
    public void TypeDependencies_ForLayer_ExcludeForbiddenNamespaces(string layer)
    {
        NamespaceRule rule = NamespaceRules.Single(candidate => candidate.Name == layer);

        var result = rule.NamespacePrefix is null
            ? Types.InAssembly(rule.Subject)
                .Should()
                .NotHaveDependencyOnAny(rule.Forbidden)
                .GetResult()
            : Types.InAssembly(rule.Subject)
                .That()
                .ResideInNamespaceMatching($@"^{Regex.Escape(rule.NamespacePrefix)}(\..+)?$")
                .Should()
                .NotHaveDependencyOnAny(rule.Forbidden)
                .GetResult();

        // A rule that selects no type would pass without checking anything.
        Assert.NotEmpty(result.SelectedTypesForTesting);

        string failures = string.Join(
            Environment.NewLine,
            result.FailingTypes.Select(type => $"- {type.FullName}: {type.Explanation}"));

        Assert.True(
            result.IsSuccessful,
            $"{rule.Name} qatı qadağan olunmuş asılılıq daşıyır:{Environment.NewLine}{failures}");
    }

    [Theory]
    [MemberData(nameof(ReferenceRuleNames))]
    public void AssemblyReferences_ForLayer_ExcludeForbiddenAssemblies(string layer)
    {
        ReferenceRule rule = ReferenceRules.Single(candidate => candidate.Name == layer);

        var forbidden = rule.Forbidden
            .Select(assembly => assembly.GetName().Name)
            .ToHashSet(StringComparer.Ordinal);

        string[] hits = rule.Subject.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && forbidden.Contains(name))
            .Select(name => name!)
            .ToArray();

        Assert.True(
            hits.Length == 0,
            $"{rule.Subject.GetName().Name} qadağan olunmuş assembly-lərə istinad edir: {string.Join(", ", hits)}");
    }

    [Fact]
    public void ForbiddenNamespaces_EveryCommerceCoreEntry_IsDeclaredByAProductionAssembly()
    {
        string[] declaredNamespaces = ProductionAssemblies
            .SelectMany(DeclaredNamespaces)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // An entry that matches no namespace makes its rule pass without checking anything.
        string[] inertEntries = NamespaceRules
            .SelectMany(rule => rule.Forbidden.Select(entry => (rule.Name, Entry: entry)))
            .Where(item => item.Entry.StartsWith("CommerceCore.", StringComparison.Ordinal))
            .Where(item => !declaredNamespaces.Any(ns =>
                ns == item.Entry || ns.StartsWith(item.Entry + ".", StringComparison.Ordinal)))
            .Select(item => $"{item.Name}: {item.Entry}")
            .ToArray();

        Assert.Empty(inertEntries);
    }

    private static IEnumerable<string> DeclaredNamespaces(Assembly assembly)
    {
        Type?[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types;
        }

        return types
            .Select(type => type?.Namespace)
            .OfType<string>();
    }

    private static Assembly LoadAssembly(string name)
        => Assembly.Load(new AssemblyName(name));

    private sealed record NamespaceRule(
        string Name,
        Assembly Subject,
        string? NamespacePrefix,
        string[] Forbidden);

    private sealed record ReferenceRule(
        string Name,
        Assembly Subject,
        Assembly[] Forbidden);
}

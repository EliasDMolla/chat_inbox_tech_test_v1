using System.Reflection;

namespace EncodeLabs.Inbox.Domain.Tests.Architecture;

public sealed class DomainDependenciesTests
{
    private static readonly string[] ForbiddenDependencyPrefixes =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "RabbitMQ",
        "Telegram",
    ];

    [Fact]
    public void Domain_does_not_reference_infrastructure_frameworks()
    {
        var domainAssemblyPath = Path.Combine(
            AppContext.BaseDirectory,
            "EncodeLabs.Inbox.Domain.dll");
        var domainAssembly = Assembly.LoadFrom(domainAssemblyPath);
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            referencedAssemblies,
            reference => ForbiddenDependencyPrefixes.Any(prefix =>
                reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true));
    }
}

using CommerceCore.Persistence;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

try
{
    IConfiguration configuration = new ConfigurationBuilder()
        .AddUserSecrets<Program>(optional: false)
        .AddEnvironmentVariables(prefix: "COMMERCECORE_BOOTSTRAP_")
        .Build();

    string connectionString = Required(
        configuration,
        "ConnectionStrings:CommerceCoreDatabase");

    string tenantSlug = TenantSlug(Required(configuration, "TENANT_SLUG"));
    string tenantName = Required(configuration, "TENANT_NAME");
    string hostName = HostName(Required(configuration, "HOST_NAME"));
    string marketCode = Optional(configuration, "MARKET_CODE", "AZ")
        .ToUpperInvariant();
    string defaultLocale = Optional(configuration, "DEFAULT_LOCALE", "az-AZ");
    string adminSubject = Required(configuration, "ADMIN_SUBJECT");

    var options = new DbContextOptionsBuilder<CommerceCoreDbContext>()
        .UseNpgsql(connectionString)
        .Options;

    await using var dbContext = new CommerceCoreDbContext(options);

    string[] pendingMigrations = [
        .. await dbContext.Database.GetPendingMigrationsAsync()
    ];

    if (pendingMigrations.Length > 0)
    {
        throw new InvalidOperationException(
            "Database has pending migrations. Apply migrations before bootstrapping.");
    }

    await using var transaction =
        await dbContext.Database.BeginTransactionAsync();

    Tenant? tenant = await dbContext.Tenants.SingleOrDefaultAsync(
        item => item.Slug == tenantSlug);

    if (tenant is null)
    {
        tenant = new Tenant
        {
            Id = TenantId.New(),
            Slug = tenantSlug,
            Name = tenantName,
            Status = TenantStatuses.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Tenants.Add(tenant);
    }
    else if (!string.Equals(tenant.Name, tenantName, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Tenant slug '{tenantSlug}' already belongs to another name.");
    }

    Storefront? storefront = await dbContext.Storefronts.SingleOrDefaultAsync(
        item => item.HostName == hostName);

    if (storefront is null)
    {
        storefront = new Storefront
        {
            Id = StorefrontId.New().Value,
            TenantId = tenant.Id,
            HostName = hostName,
            MarketCode = marketCode,
            DefaultLocale = defaultLocale,
            IsActive = true
        };

        dbContext.Storefronts.Add(storefront);
    }
    else if (storefront.TenantId != tenant.Id ||
             storefront.MarketCode != marketCode ||
             storefront.DefaultLocale != defaultLocale ||
             !storefront.IsActive)
    {
        throw new InvalidOperationException(
            $"Storefront host '{hostName}' already has incompatible settings.");
    }

    bool hasMembershipForAnotherTenant =
        await dbContext.TenantMemberships.AnyAsync(
            item => item.UserSubject == adminSubject &&
                    item.TenantId != tenant.Id);

    if (hasMembershipForAnotherTenant)
    {
        throw new InvalidOperationException(
            $"User subject '{adminSubject}' already belongs to another tenant.");
    }

    TenantMembership? existingMembership =
        await dbContext.TenantMemberships.SingleOrDefaultAsync(
            item => item.TenantId == tenant.Id &&
                    item.UserSubject == adminSubject);

    if (existingMembership is null)
    {
        dbContext.TenantMemberships.Add(new TenantMembership
        {
            TenantId = tenant.Id,
            UserSubject = adminSubject,
            Role = TenantMembershipRoles.Admin,
            Status = TenantMembershipStatuses.Active
        });
    }
    else if (existingMembership.Role != TenantMembershipRoles.Admin ||
             existingMembership.Status != TenantMembershipStatuses.Active)
    {
        throw new InvalidOperationException(
            $"User subject '{adminSubject}' already has incompatible membership.");
    }

    await dbContext.SaveChangesAsync();
    await transaction.CommitAsync();

    Console.WriteLine(
        $"Bootstrap completed. TenantId={tenant.Id}; Host={hostName}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Bootstrap failed: {exception.Message}");
    return 1;
}

static string Required(IConfiguration configuration, string key)
{
    string? value = configuration[key]?.Trim();

    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException(
            $"Required configuration '{key}' was not supplied.")
        : value;
}

static string Optional(
    IConfiguration configuration,
    string key,
    string defaultValue) =>
    string.IsNullOrWhiteSpace(configuration[key])
        ? defaultValue
        : configuration[key]!.Trim();

static string TenantSlug(string value)
{
    string slug = value.Trim().ToLowerInvariant();

    if (slug.Length is < 3 or > 100 ||
        slug.Any(character =>
            !char.IsAsciiLetterOrDigit(character) && character != '-'))
    {
        throw new InvalidOperationException(
            "TENANT_SLUG must be 3–100 lowercase letters, digits, or hyphens.");
    }

    return slug;
}

static string HostName(string value)
{
    string hostName = value.Trim().TrimEnd('.').ToLowerInvariant();

    if (hostName.Length is 0 or > 255 ||
        Uri.CheckHostName(hostName) == UriHostNameType.Unknown)
    {
        throw new InvalidOperationException(
            "HOST_NAME must be a valid host name without protocol or port.");
    }

    return hostName;
}
namespace CommerceCore.Api.Configuration;

internal static class ConfigurationValidationExtensions
{
    public static void ValidateConfiguration(this IHostApplicationBuilder builder)
    {
        _ = builder.Configuration.GetConnectionString("CommerceCoreDatabase")
            ?? throw new InvalidOperationException("Connection string 'CommerceCoreDatabase' was not found");

        string? allowedHosts = builder.Configuration["AllowedHosts"];

        if (!builder.Environment.IsDevelopment() &&
            (string.IsNullOrWhiteSpace(allowedHosts) ||
             allowedHosts
                 .Split(
                     ';',
                     StringSplitOptions.TrimEntries |
                     StringSplitOptions.RemoveEmptyEntries)
                 .Any(host => host == "*")))
        {
            throw new InvalidOperationException(
                "AllowedHosts must contain explicit host names outside Development.");
        }

        string? authenticationAuthority = builder.Configuration["Authentication:Schemes:Bearer:Authority"];
        string? authenticationAudience = builder.Configuration["Authentication:Schemes:Bearer:Audience"];
        string? validIssuer = builder.Configuration["Authentication:Schemes:Bearer:ValidIssuer"];
        string? validAudience = builder.Configuration["Authentication:Schemes:Bearer:ValidAudience"];
        bool hasValidAudiences = builder.Configuration.GetSection("Authentication:Schemes:Bearer:ValidAudiences").Exists() ||
                                 !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Schemes:Bearer:ValidAudiences:0"]);

        bool hasAuthorityAndAudience =
            !string.IsNullOrWhiteSpace(authenticationAuthority) &&
            !string.IsNullOrWhiteSpace(authenticationAudience);

        bool hasIssuerAndAudience =
            !string.IsNullOrWhiteSpace(validIssuer) &&
            (!string.IsNullOrWhiteSpace(validAudience) || hasValidAudiences);

        if (!builder.Environment.IsDevelopment() &&
            !hasAuthorityAndAudience &&
            !hasIssuerAndAudience)
        {
            throw new InvalidOperationException(
                "Configure either Bearer Authority and Audience, or ValidIssuer and ValidAudience(s) outside Development.");
        }
    }
}

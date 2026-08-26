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

        if (!builder.Environment.IsDevelopment() &&
            (string.IsNullOrWhiteSpace(authenticationAuthority) ||
             string.IsNullOrWhiteSpace(authenticationAudience)))
        {
            throw new InvalidOperationException(
                "Bearer Authority and Audience must be configured outside Development.");
        }
    }
}

namespace CommerceCore.Api.Common.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        IHeaderDictionary headers = context.Response.Headers;

        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=()";
        headers.ContentSecurityPolicy =
            "default-src 'none'; base-uri 'none'; " +
            "frame-ancestors 'none'; form-action 'none'";

        await _next(context);
    }
}
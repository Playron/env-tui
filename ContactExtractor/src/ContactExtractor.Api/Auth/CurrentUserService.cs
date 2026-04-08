namespace ContactExtractor.Api.Auth;

public class CurrentUserService(IHttpContextAccessor accessor)
{
    public string? UserId =>
        accessor.HttpContext?.User.FindFirst("sub")?.Value;

    public string? UserName =>
        accessor.HttpContext?.User.FindFirst("preferred_username")?.Value;

    public bool IsAdmin =>
        accessor.HttpContext?.User.IsInRole("admin") ?? false;

    /// <summary>
    /// Returns "anonymous" when auth is disabled (dev mode without Keycloak).
    /// </summary>
    public string UserIdOrAnonymous => UserId ?? "anonymous";
}

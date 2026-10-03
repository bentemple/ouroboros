using System.Security.Claims;

namespace Ouroboros;

public record AuthedUser(string HeadscaleName, string Subject, string Username, string? DisplayName, bool IsAdmin)
{
	public static AuthedUser? FromCtx(HttpContext ctx)
	{
		if (ctx.User.Identity?.IsAuthenticated != true)
			return null;

		var sub = ctx.User.FindFirst("sub")?.Value;
		if (sub == null)
			return null;

		// the username is whichever one this subject owns, not whatever it logged in as - see Bindings
		var username = Bindings.UsernameFor(sub);
		if (username == null || !Config.C.user_map.TryGetValue(username, out var headscaleName))
			return null;

		return new AuthedUser(
			headscaleName,
			sub,
			username,
			ctx.User.FindFirst("name")?.Value,
			HasAdminEntitlement(ctx.User));
	}

	/// <summary>
	/// Whether this login carries the configured admin entitlement. Independent of user_map, so an
	/// admin who owns no devices still gets the admin page.
	/// </summary>
	public static bool HasAdminEntitlement(ClaimsPrincipal principal)
		=> !string.IsNullOrWhiteSpace(Config.C.oidc_admin_value)
		&& principal.FindAll(Config.C.oidc_admin_claim).Any(c => c.Value == Config.C.oidc_admin_value);

	/// <summary>
	/// A name to greet someone by when they may not have a user_map entry at all.
	/// </summary>
	public static string DisplayNameOf(ClaimsPrincipal principal)
		=> principal.FindFirst("name")?.Value
		?? principal.FindFirst(Config.C.oidc_user_claim)?.Value
		?? "admin";
}

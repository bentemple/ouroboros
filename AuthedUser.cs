namespace Ouroboros;

public record AuthedUser(string HeadscaleName, string Subject, string Username, string? DisplayName)
{
	public static AuthedUser? FromCtx(HttpContext ctx)
	{
		if (ctx.User.Identity?.IsAuthenticated != true)
			return null;

		// the claim user_map is keyed on, e.g. the authentik username
		var key = ctx.User.FindFirst(Config.C.oidc_user_claim)?.Value;
		if (key == null || !Config.C.user_map.TryGetValue(key, out var headscaleName))
			return null;

		return new AuthedUser(
			headscaleName,
			ctx.User.FindFirst("sub")?.Value ?? key,
			key,
			ctx.User.FindFirst("name")?.Value);
	}
}

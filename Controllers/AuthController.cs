using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Ouroboros.Models;

namespace Ouroboros.Controllers;

public class AuthController : Controller
{
	/// <summary>
	/// The login page, offering to send the user off to the identity provider.
	/// </summary>
	public IActionResult Index(string? mKey)
	{
		var key = AuthKey.Parse(mKey);

		// no point showing a login page to someone who is already logged in
		if (AuthedUser.FromCtx(HttpContext) != null)
			return Redirect(ReturnTo(mKey));

		return View(
			new AuthIndexModel(
				key,
				key == null ? "to access the dashboard" : "to add a node"));
	}

	/// <summary>
	/// Starts an OIDC login, coming back to the dashboard or to a node registration afterwards.
	/// </summary>
	public IActionResult Login(string? mKey)
		=> Challenge(
			new AuthenticationProperties { RedirectUri = ReturnTo(mKey) },
			OpenIdConnectDefaults.AuthenticationScheme);

	/// <summary>
	/// Shown when the identity provider authenticated someone who isn't in user_map.
	/// </summary>
	public IActionResult NotRegistered(string? login)
		=> View(new AuthNotRegisteredModel(string.IsNullOrEmpty(login) ? "unknown" : login));

	[HttpPost]
	public async Task<IActionResult> Logout(string? then)
	{
		// only the ouroboros session is dropped - the provider session is left alone, as signing the
		// user out of their whole SSO because they logged out of one app would be rude
		await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

		return Redirect(Url.IsLocalUrl(then) ? then! : "/ouroboros/dashboard");
	}

	// the key gets spliced into a redirect URL, so only a parsed one is ever used
	private static string ReturnTo(string? mKey)
		=> AuthKey.Parse(mKey) is { } key ? $"/register/{key}" : "/ouroboros/dashboard";
}

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ouroboros.Models;

namespace Ouroboros.Controllers;

// fail closed: anything added to this controller needs a session, even if whoever adds it
// forgets to check. the per-action ownership checks below are still what scopes access.
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class RegisterController : Controller
{
	[Route("/register/{mKey}")]
	[HttpGet]
	public IActionResult Index(string? mKey)
	{
		if (AuthKey.Parse(mKey) is not { } key)
			return BadRequest("not a valid registration key.");

		var user = AuthedUser.FromCtx(HttpContext);
		if (user == null)
			return Redirect($"/ouroboros/auth?mkey={mKey}");

		return View(new RegisterIndexModel(user, key));
	}

	[HttpPost]
	public async Task<IActionResult> Add(string? mKey)
	{
		var user = AuthedUser.FromCtx(HttpContext);
		if (user == null) return Unauthorized();

		// accepts the whole link headscale printed, so the key need not be picked out of it by hand
		if (AuthKey.Parse(mKey) is not { } key)
			return BadRequest("not a valid registration key.");

		await Headscale.NodeRegister(user.HeadscaleName, key);
		return Redirect("/ouroboros/dashboard");
	}
}
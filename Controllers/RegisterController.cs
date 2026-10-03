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
		if (mKey is not { Length: 24 })
			return BadRequest("not a valid mkey.");

		var user = AuthedUser.FromCtx(HttpContext);
		if (user == null)
			return Redirect($"/ouroboros/auth?mkey={mKey}");

		return View(new RegisterIndexModel(user, mKey));
	}

	[HttpPost]
	public async Task<IActionResult> Add(string? mKey)
	{
		if (mKey == null) return BadRequest();
		var user = AuthedUser.FromCtx(HttpContext);
		if (user == null) return Unauthorized();

		await Headscale.NodeRegister(user.HeadscaleName, mKey);
		return Redirect("/ouroboros/dashboard");
	}
}
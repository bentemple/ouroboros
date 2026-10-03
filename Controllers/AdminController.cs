using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ouroboros.Models;

namespace Ouroboros.Controllers;

/// <summary>
/// The only place in ouroboros that can touch another user's devices, gated on an entitlement in the
/// identity provider rather than on anything in cfg.json.
/// </summary>
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "admin")]
public class AdminController : Controller
{
	public async Task<IActionResult> Index()
	{
		var nodes = await Headscale.NodesList();

		// username -> subject. First() rather than ToDictionary so a hand-edited bindings.json with a
		// duplicate shows up in the table instead of throwing.
		var owners = Bindings.All
							 .GroupBy(kv => kv.Value)
							 .ToDictionary(g => g.Key, g => g.First().Key);

		var users = Config.C.user_map
						   .Select(kv => new AdminUserRow(kv.Key, kv.Value, owners.GetValueOrDefault(kv.Key), true))
						   .OrderBy(u => u.Username);

		// subjects holding usernames that have since left user_map - visible so they can be released
		var orphans = Bindings.All
							  .Where(kv => !Config.C.user_map.ContainsKey(kv.Value))
							  .Select(kv => new AdminUserRow(kv.Value, "-", kv.Key, false))
							  .OrderBy(u => u.Username);

		return View(
			new AdminModel(
				AuthedUser.DisplayNameOf(User),
				users.Concat(orphans),
				nodes.OrderBy(n => n.User?.Name).ThenBy(n => n.Id)));
	}

	[HttpPost]
	public async Task<IActionResult> DeleteNode(int id)
	{
		var res = await Headscale.NodeDelete(id);

		return res
				   ? Redirect("/ouroboros/admin")
				   : StatusCode(500, "500: Could not remove node.");
	}

	[HttpPost]
	public async Task<IActionResult> ExpireNode(int id)
	{
		await Headscale.NodeExpire(id);

		return Redirect("/ouroboros/admin");
	}

	[HttpPost]
	public async Task<IActionResult> SetNodeRoutes(int id, string routes)
	{
		// routes_enabled is absolute, so it holds here too even though admins are otherwise exempt
		if (!AuthedUser.HasRouteEntitlement(User)) return Forbid();

		var routesList = JsonSerializer.Deserialize<string[]>(routes);
		if (routesList == null) return BadRequest();

		await Headscale.NodeApproveRoutes(id, routesList);

		return Redirect("/ouroboros/admin");
	}

	[HttpPost]
	public async Task<IActionResult> RenameNode(int id, string? name)
	{
		// headscale takes the new name positionally, so a leading dash would read as a flag
		if (string.IsNullOrWhiteSpace(name) || name.TrimStart().StartsWith('-'))
			return BadRequest("not a usable name.");

		await Headscale.NodeRename(id, name.Trim());

		return Redirect("/ouroboros/admin");
	}

	[HttpPost]
	public IActionResult Release(string? username)
	{
		if (string.IsNullOrWhiteSpace(username)) return BadRequest();

		Bindings.Release(username);

		return Redirect("/ouroboros/admin");
	}
}

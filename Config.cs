using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace Ouroboros;

// config code adapted from the uwu radio constants loader
// https://github.com/uwu/radio/blob/master/UwuRadio.Server/Constants.cs

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class Config
{
#if DEBUG
	private const string CfgPath = "cfg.debug.json";
#else
	private const string CfgPath = "cfg.json";
#endif

	public static readonly Config C = JsonSerializer.Deserialize<Config>(File.OpenRead(CfgPath))!;

	/// <summary>
	/// Where everything ouroboros has to remember lives: the session signing keys, and the subject to
	/// username bindings. Mount this in docker or everyone is logged out and reassociated on recreate.
	/// </summary>
	public const string DataPath = "data";

	public bool hs_is_remote { get; init; } = false;
	
	/// <summary>
	/// (REQUIRED if headscale_is_remote)The address to attempt to reach the headscale server at
	/// </summary>
	public string? hs_address { get; init; }
	
	/// <summary>
	/// (REQUIRED if headscale_is_remote) The API key to use to connect to the headscale server
	/// </summary>
	public string? hs_api_key { get; init; }

	/// <summary>
	/// (OPTIONAL) Skip certificate verification when reaching headscale over gRPC. Needed when
	/// headscale serves its own self-signed certificate, which is the usual case when it is only
	/// reachable on an internal network. Has no effect unless hs_is_remote.
	/// </summary>
	public bool hs_insecure_grpc { get; init; } = false;

	/// <summary>
	/// (OPTIONAL) The path to the headscale CLI binary to use
	/// </summary>
	public string hs_bin_path { get; init; } = "headscale";
	
	/// <summary>
	/// The public URL your headscale instance is accessible on (the --login-server= argument to tailscale up)
	/// </summary>
	public string hs_login_url { get; init; }
	
	/// <summary>
	/// (OPTIONAL) The hostname ouroboros itself is served on, as it appears in the OIDC redirect URI.
	/// Only X-Forwarded-Host values matching this are trusted. Defaults to hs_login_url, which is correct
	/// whenever ouroboros and headscale share a domain (as in the caddy config in the README).
	/// </summary>
	public string? public_host { get; init; }

	/// <summary>
	/// The OIDC issuer URL, used to find the provider's .well-known/openid-configuration document.
	/// For authentik this is https://your.authentik.host/application/o/&lt;app slug&gt;/
	/// </summary>
	public string oidc_authority { get; init; }

	/// <summary>
	/// The OIDC Client ID
	/// </summary>
	public string oidc_client_id { get; init; }

	/// <summary>
	/// The OIDC Client Secret
	/// </summary>
	public string oidc_client_secret { get; init; }

	/// <summary>
	/// (OPTIONAL) The scopes to request. openid is mandatory, and the scope carrying oidc_user_claim is needed too.
	/// </summary>
	public string oidc_scopes { get; init; } = "openid profile email";

	/// <summary>
	/// (OPTIONAL) The claim whose value user_map is keyed on. "sub" is the only claim guaranteed stable by the
	/// spec, but usernames are far easier to write a config against.
	/// </summary>
	public string oidc_user_claim { get; init; } = "preferred_username";

	/// <summary>
	/// (OPTIONAL) The claim carrying the user's group or entitlement names, checked against
	/// oidc_admin_value. In authentik, "groups" arrives with the profile scope.
	/// </summary>
	public string oidc_admin_claim { get; init; } = "groups";

	/// <summary>
	/// (OPTIONAL) The group or entitlement that grants access to the admin page. Leaving this unset
	/// means nobody is an admin and the page is closed to everyone, including you.
	/// </summary>
	public string? oidc_admin_value { get; init; }

	/// <summary>
	/// (OPTIONAL) The text on the login button, e.g. "Log in with authentik"
	/// </summary>
	public string oidc_login_text { get; init; } = "Log in";

	/// <summary>
	/// A map of usernames to headscale users. The username is matched against oidc_user_claim only on
	/// a user's first login - after that their OIDC subject owns the entry (see <see cref="Bindings"/>).
	/// </summary>
	// ReSharper disable once CollectionNeverUpdated.Global
	public Dictionary<string, string> user_map { get; init; }

	/// <summary>
	/// public_host, falling back to the headscale host it usually shares a domain with
	/// </summary>
	public string OPublicHost => string.IsNullOrWhiteSpace(public_host) ? hs_login_url : public_host;
}
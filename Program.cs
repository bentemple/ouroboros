using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Ouroboros;

// https://andrewlock.net/exploring-the-dotnet-8-preview-comparing-createbuilder-to-the-new-createslimbuilder-method/#what-s-missing-from-createslimbuilder-
var builder = WebApplication.CreateSlimBuilder(args);

// ouroboros always runs behind a reverse proxy (see CADDY.md), and the OIDC handler needs the real
// scheme and host to build the redirect_uri it hands to the identity provider
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
	o.ForwardedHeaders = ForwardedHeaders.XForwardedFor
					   | ForwardedHeaders.XForwardedProto
					   | ForwardedHeaders.XForwardedHost;

	// the proxy is usually a sibling container on an address we can't know ahead of time, so instead
	// of trusting it by address we pin the hosts it is allowed to claim we are being served on.
	// without this, anyone able to reach ouroboros directly could point our redirect_uri elsewhere.
	foreach (var host in Config.C.OPublicHosts)
		o.AllowedHosts.Add(host);

	o.KnownNetworks.Clear();
	o.KnownProxies.Clear();
});

// the keys that sign and encrypt the auth cookie. without a stable location they are regenerated on
// every container recreate, logging everyone out - so keep them somewhere mountable.
builder.Services
	   .AddDataProtection()
	   .SetApplicationName("ouroboros")
	   .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Config.DataPath, "keys")));

// Add services to the container.
// the antiforgery token is already emitted into every form by the form tag helper, this is what
// actually checks it on the way back in
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAuthorization(o =>
	o.AddPolicy(
		"admin",
		p => p.RequireAssertion(c => AuthedUser.HasAdminEntitlement(c.User))));

builder.Services
	   .AddAuthentication(o =>
		{
			o.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
			o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
		})
	   .AddCookie(o =>
		{
			o.Cookie.Name = "ouroboros";
			// /register lives outside the /ouroboros path base, so the cookie must cover the whole site
			o.Cookie.Path = "/";
			o.Cookie.HttpOnly = true;
			o.Cookie.SameSite = SameSiteMode.Lax;
			// never hand the session cookie out over plain http, even if a request arrives that way
			o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
			// short, because re-logging in is a redirect through the identity provider rather than a
			// password prompt. sliding, so this is an idle timeout and not a daily interruption.
			o.ExpireTimeSpan    = TimeSpan.FromDays(1);
			o.SlidingExpiration = true;

			// where [Authorize] sends anyone without a session. /register is outside the path base, so
			// the login page is addressed absolutely rather than letting the handler build it.
			o.Events.OnRedirectToLogin = ctx =>
			{
				var path = ctx.Request.Path.Value ?? "";
				var mKey = path.StartsWith("/register/", StringComparison.Ordinal)
							   ? path["/register/".Length..]
							   : null;

				ctx.Response.Redirect(
					string.IsNullOrEmpty(mKey)
						? "/ouroboros/auth"
						: $"/ouroboros/auth?mkey={Uri.EscapeDataString(mKey)}");

				return Task.CompletedTask;
			};
		})
	   .AddOpenIdConnect(o =>
		{
			o.Authority    = Config.C.oidc_authority;
			o.ClientId     = Config.C.oidc_client_id;
			o.ClientSecret = Config.C.oidc_client_secret;

			o.ResponseType = OpenIdConnectResponseType.Code;
			o.UsePkce      = true;

			// UsePathBase prepends /ouroboros, so publicly this is /ouroboros/auth/callback
			o.CallbackPath = "/auth/callback";

			// read claims under their real OIDC names instead of the legacy ClaimTypes URIs, so that
			// oidc_user_claim means exactly what it says
			o.MapInboundClaims                        = false;
			o.TokenValidationParameters.NameClaimType = Config.C.oidc_user_claim;
			o.GetClaimsFromUserInfoEndpoint           = true;

			// we never call the provider's APIs on the user's behalf, so there is nothing to keep
			o.SaveTokens = false;

			o.Scope.Clear();
			foreach (var scope in Config.C.oidc_scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
				o.Scope.Add(scope);

			// users are registered by hand (see user_map), so turn away anyone we don't know before a
			// session cookie is ever issued. this is also where a first-time login claims its username.
			o.Events.OnTicketReceived = ctx =>
			{
				var sub      = ctx.Principal?.FindFirst("sub")?.Value;
				var username = ctx.Principal?.FindFirst(Config.C.oidc_user_claim)?.Value;

				// Claim runs first either way, so an admin who is also in user_map still gets bound
				var claimed = sub == null ? null : Bindings.Claim(sub, username);
				if (claimed != null || (ctx.Principal != null && AuthedUser.HasAdminEntitlement(ctx.Principal)))
					return Task.CompletedTask;

				ctx.HandleResponse();
				ctx.Response.Redirect(
					$"{ctx.Request.PathBase}/auth/notregistered?login={Uri.EscapeDataString(username ?? "")}");

				return Task.CompletedTask;
			};
		});

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/oopsie");

app.UseForwardedHeaders();

app.UsePathBase("/ouroboros");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute();

app.MapGet("/",       () => Results.Redirect("/ouroboros/dashboard"));
app.MapGet("/oopsie", () => "sorry, something broke");

app.Run();

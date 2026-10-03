# Ouroboros

A small web UI for [headscale](https://github.com/juanfont/headscale). Users log in with OIDC and manage
their own devices: add, rename, expire, delete, approve subnet routes.

Not for server settings or signups - accounts are made by hand in headscale, listed in `user_map`.

| Ouroboros   | Headscale |
|-------------|-----------|
| 0.6.3       | 0.29.x    |
| 0.4.0-0.4.2 | 0.26.1    |
| 0.3.1       | 0.23.0    |

Images are published to both `bentempledev/ouroboros` on Docker Hub and
`ghcr.io/bentemple/ouroboros` on ghcr.io.

| file                         | contents                                         |
|------------------------------|--------------------------------------------------|
| `docker-compose.example.yml` | headscale + ouroboros, both ways to connect them |
| `Caddyfile.example`          | reverse proxy doing the two-path routing         |
| `cfg.example.json`           | every option, for running outside docker         |

## Quickstart with authentik

`vpn.example.com` is your headscale domain, `auth.example.com` your authentik.

In authentik:

- *Providers > Create > OAuth2/OpenID Provider*: **confidential**, redirect URI
  `https://vpn.example.com/ouroboros/auth/callback`, default scopes. Keep the client ID, the secret, and
  the **OpenID Configuration Issuer** from the provider page.
- Create an application pointing at that provider, bind your users to it.
- *Directory > Groups*: `ouroboros-admins`, add yourself. Group names arrive in `groups` via `profile`.

Then:

1. `cp docker-compose.example.yml docker-compose.yml`. Replace every `CHANGEME`, set `USER_MAP`.
2. Choose how ouroboros reaches headscale:
   - **shared socket** - already what the file does. No key, no certificate, nothing listening.
   - **api key** - for a headscale elsewhere, or one already serving gRPC over HTTPS. Uncomment that
     block, and `headscale apikeys create` for `HS_API_KEY`.
3. `docker compose up -d`
4. `docker compose exec headscale headscale users create sink`, once per name in `USER_MAP`.
5. Put a reverse proxy in front, below.
6. Log in at `https://vpn.example.com/ouroboros/dashboard`. Add a device with
   `tailscale up --login-server=https://vpn.example.com`, then follow the printed link.

## Config

In docker these are environment variables of the same name, upper-cased: `hs_api_key` → `HS_API_KEY`.
`cfg.example.json` lists all of them.

Every setup:

| option             | default                | purpose                                           |
|--------------------|------------------------|---------------------------------------------------|
| hs_login_url       | required               | the login url node clients use                    |
| hs_bin_path        | `headscale`            | the headscale binary to shell out to              |
| public_host        | `hs_login_url`         | every hostname ouroboros answers on, comma separated |
| oidc_authority     | required               | issuer url, endpoints come from its discovery doc |
| oidc_client_id     | required               | the OIDC client id                                |
| oidc_client_secret | required               | the OIDC client secret                            |
| oidc_scopes        | `openid profile email` | space separated scopes to request                 |
| oidc_user_claim    | `preferred_username`   | the claim matched against `user_map` keys         |
| oidc_login_text    | `Log in`               | text on the login button                          |
| oidc_admin_claim   | `groups`               | claim holding group or entitlement names          |
| oidc_admin_value   | unset, nobody is admin | the group or entitlement granting the admin page  |
| routes_enabled     | `true`                 | whether route approval is offered at all          |
| routes_claim       | `groups`               | claim checked against `routes_value`              |
| routes_value       | unset, everyone may    | entitlement granting route approval               |
| user_map           | required               | usernames mapped to headscale users               |

Then whichever transport you picked, and nothing else from this table:

| option           | unix socket | gRPC over HTTPS | gRPC, self-signed cert |
|------------------|-------------|-----------------|------------------------|
| hs_is_remote     | `false`     | `true`          | `true`                 |
| hs_address       | -           | required        | required               |
| hs_api_key       | -           | required        | required               |
| hs_insecure_grpc | -           | -               | `true`                 |

## Connecting to headscale

Two transports, both in `docker-compose.example.yml`; switching is a few commented lines. Neither is
more privileged than the other: api keys carry no scopes, and headscale logs nothing for either at any
level.

**Unix socket**, `hs_is_remote: false` - no key, no certificate, no gRPC listener at all. Share a volume
at `/var/run/headscale` between both containers. It is mode `0770`, so they need a common uid or gid -
already so if both run as root, as the official images do.

```json
{ "hs_is_remote": false }
```

**gRPC**, `hs_is_remote: true` - for a headscale on another host, or one already reachable over HTTPS.
`hs_address` is wherever you already expose its gRPC; `CADDY.md` covers putting it behind the proxy that
already serves headscale, on 443.

```json
{ "hs_is_remote": true, "hs_address": "vpn.example.com:443", "hs_api_key": "..." }
```

- `hs_api_key` comes from `headscale apikeys create`. It expires in 90 days unless you pass something
  like `--expiration 365d`; when it lapses, every page 500s until you issue a new one.
- `grpc_allow_insecure: true` does not work here - the CLI has no plaintext mode, so leave it false.

### gRPC with a self-signed certificate

Only needed if headscale's gRPC is reachable nowhere but your container network. Give it a certificate
of its own, and set `hs_insecure_grpc: true` so the CLI stops checking it:

```sh
mkdir -p certs && openssl req -x509 -newkey rsa:2048 -nodes -days 3650 \
  -subj "/CN=headscale" -addext "subjectAltName=DNS:headscale" \
  -keyout certs/headscale.key -out certs/headscale.crt && chmod 644 certs/headscale.key
```

```yaml
# headscale config.yaml
grpc_listen_addr: 0.0.0.0:50443
tls_cert_path: /certs/headscale.crt
tls_key_path: /certs/headscale.key
```

That certificate also serves `listen_addr`, so your proxy's headscale upstream becomes
`https://headscale:8080` with verification off - `Caddyfile.example` has the line.

## Identity

`user_map` maps usernames to headscale users. The username is matched against `oidc_user_claim` on
first login only; after that the account's `sub` owns it, recorded in `data/bindings.json`.

- A renamed account keeps the username it claimed.
- A second account claiming a taken username is refused.
- Releasing a username takes the admin page. Raw subject ids are never needed in config.

## Entitlements

Two optional entitlements. Neither is required: without them nobody is an admin and everybody may
approve their own routes.

| entitlement                  | config             | grants                                 |
|------------------------------|--------------------|----------------------------------------|
| `ouroboros-admins`           | `oidc_admin_value` | the admin page                         |
| `ouroboros-route-management` | `routes_value`     | approving subnet routes and exit nodes |

Admins are taken to hold route management as well, so it need not be granted to them.

Create them on the application in authentik:

1. *Applications > Applications > ouroboros > Entitlements > Create* - name it, then bind the users or
   groups that should hold it
2. *Applications > Providers > ouroboros > Edit > Advanced protocol settings* - add the
   `authentik default OAuth Mapping: Application Entitlements` scope mapping
3. Ask for the scope, which is not one of the defaults, and point the claims at it:

```json
{ "oidc_scopes": "openid profile email entitlements",
  "oidc_admin_claim": "entitlements", "oidc_admin_value": "ouroboros-admins",
  "routes_claim": "entitlements", "routes_value": "ouroboros-route-management" }
```

Groups work equally well - set the claims to `groups`, which arrives with the `profile` scope and needs
no extra mapping. One caveat: the `groups` claim is built from direct membership, so somebody in a child
of a granted group does not get it. Test before relying on nesting either way. Any claim holding a list
of strings will do; `roles` is an alias of `entitlements`.

## Admin page

`/ouroboros/admin`, linked from the dashboard:

- every username with the account owning it, every node with its owner
- rename, log out, delete or approve routes on any node, whoever owns it
- release any username, freeing it for the next account that logs in as it

Admin comes from the claim alone, not `user_map`, so an admin needs no devices of their own.

## Route management

Approving a route is headscale's gate on what a node may route for: a route is served only when the node
announces it *and* it is approved. Ouroboros hands the approving half to the device's owner, so a user
can make their own device a subnet router or an exit node without asking. ACLs still decide who may
reach what, but the set of reachable destinations grows.

| | |
|---|---|
| `routes_enabled: false` | gone from the UI, for everybody, admins included |
| `routes_value` unset | every logged in user may approve their own devices' routes |
| `routes_value` named | only that entitlement's holders, plus admins |

## Storage

Mount `/App/data` in docker, and keep it private - `data/keys/` signs sessions, and losing it logs
everyone out. Losing `data/bindings.json` unclaims every username.

## Reverse proxy

Ouroboros needs one hostname of its own, and nothing else:

- `/ouroboros/*` → `ouroboros:8080`

Routing two paths on headscale's own domain as well is optional. It makes the link `tailscale up` prints
open ouroboros directly, instead of the key being pasted into the dashboard:

- `/ouroboros/*` → `ouroboros:8080`
- `/register/*` → `ouroboros:8080`
- everything else → `headscale:8080`

Doing both means two hostnames reach ouroboros, so list them both in `public_host`.

`Caddyfile.example` does the combined form. `CADDY.md` covers nginx, and proxying a remote headscale's
gRPC port.

- Send `X-Forwarded-Proto` and `X-Forwarded-Host`. Only hosts matching `public_host` are trusted.
- Don't publish ouroboros' port.
- On the gRPC transport the headscale upstream is `https://headscale:8080`, verification off.

## Upgrading from 0.4.x

Headscale users, nodes and registrations are untouched. Config only:

1. Create an OIDC provider as above.
2. `gh_client_id` / `gh_client_secret` → `oidc_authority`, `oidc_client_id`, `oidc_client_secret`.
3. Rekey `user_map` from github ids to provider usernames, values unchanged: `"19270622"` → `"sink"`.
4. Mount `/App/data`.
5. Stop publishing ouroboros' port.

Everyone is logged out once, as the session cookie changed.

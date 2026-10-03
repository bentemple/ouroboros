# Ouroboros

Ouroboros is a small UI on top of [headscale](https://github.com/juanfont/headscale), which takes a pragmatic approach.

Only the latest version of ouroboros is supported at any given point, but the known working versions of Headscale
for each version of Ouroboros are listed below:

| Ouroboros version   | Headscale version(s) |
|---------------------|----------------------|
| 0.4.0, 0.4.1, 0.4.2 | 0.26.1               |
| 0.3.1               | 0.23.0               |

## Goals
- Allow users to fully control and manage their own devices
- Allow users to add devices interactively themselves, instead of the server admin having to run a command
- Users should have to authenticate first, against any OpenID Connect provider (authentik, Keycloak, Authelia, ...)

## Non-goals
- Managing server settings - for things the sysadmin only should be allowed to do, they can use the CLI
- Managing users - users must be setup manually and mapped to OIDC accounts manually, OIDC is only for verification,
  not for signups

## Setup, usage and config

Create an OAuth2/OIDC provider in your identity provider.
- Client type: **confidential** (ouroboros keeps a client secret)
- Redirect URI: `https://your.server.com/ouroboros/auth/callback`
- Scopes: `openid`, `profile`, `email`
- Note down the client id, the client secret, and the issuer URL

In authentik specifically, that's *Applications > Providers > Create > OAuth2/OpenID Provider*, then an
application pointing at it. The issuer URL is shown on the provider's page as "OpenID Configuration Issuer",
and looks like `https://authentik.your.server.com/application/o/ouroboros/`.

That issuer URL is the only endpoint you configure. Ouroboros fetches
`<issuer>/.well-known/openid-configuration` on first login to discover the authorize, token, userinfo and
JWKS endpoints, and re-reads it every 12 hours so key rotations are picked up without a restart. The issuer
must be served over HTTPS.

Ouroboros does not create accounts. Each user you want to let in must already exist in headscale
(`headscale users create jim`) and be listed in `user_map`.

Create your config file cfg.json:
```json
{
  "hs_is_remote": true,
  "hs_address": "your.server.com:443",
  "hs_api_key": "FnxEEt2e4A.etc",
  "hs_bin_path": "/usr/bin/headscale",
  "hs_login_url": "your.server.com",
  "oidc_authority": "https://authentik.your.server.com/application/o/ouroboros/",
  "oidc_client_id": "AbCd.etc",
  "oidc_client_secret": "8e0f9-etc",
  "user_map": {
    "sink": "sink",
    "jim.smith": "jim"
  }
}
```

| option             | default                | purpose                                               |
|--------------------|------------------------|-------------------------------------------------------|
| hs_is_remote       | `false`                | sets if the headscale server is on a separate host    |
| hs_address         | required if is_remote  | the host and port used to connect to headscale        |
| hs_api_key         | required if is_remote  | the api key used to connect to headscale              |
| hs_bin_path        | `headscale`            | the headscale binary path to use                      |
| hs_login_url       | required               | the login url used by the node clients                |
| public_host        | `hs_login_url`         | the hostname ouroboros itself is served on            |
| oidc_authority     | required               | the issuer url of your OIDC provider                  |
| oidc_client_id     | required               | the OIDC client id                                    |
| oidc_client_secret | required               | the OIDC client secret                                |
| oidc_scopes        | `openid profile email` | space separated scopes to request                     |
| oidc_user_claim    | `preferred_username`   | the claim `user_map` is keyed on                      |
| oidc_login_text    | `Log in`               | the text on the login page's button                   |
| user_map           | required               | map of `oidc_user_claim` values to headscale usernames |

### Keying user_map: usernames or subject ids

`user_map` is keyed on whatever claim `oidc_user_claim` names, and that claim is the whole of ouroboros'
identity check - whoever presents it gets that headscale user's devices.

The default, `preferred_username`, is readable but **mutable**: if someone can be renamed to `sink` in your
identity provider, they inherit sink's devices. If usernames are not locked down to administrators you want
`sub` instead - the provider's permanent internal id for the account, which nothing can transfer:

```json
{
  "oidc_user_claim": "sub",
  "user_map": {
    "4f2a1c9e-...": "sink"
  }
}
```

To find someone's `sub`, set `"oidc_user_claim": "sub"` with an empty `user_map`, then have them log in.
Ouroboros will turn them away with "the user <sub> is not registered" - that is the value to paste in.

### Keeping people logged in across restarts

The keys that sign the session cookie are written to `keys/` next to the binary. If that directory is lost,
every session is invalidated and everyone has to log in again - so in docker, mount a volume at
`/App/keys`. Keep it private to the container; anything that can read those keys can forge a session.

Get headscale running via any means of your choice (I'm partial to docker), and get it running and exposed to the internet.
Ouroboros only needs to bind on TWO paths:
- `/ouroboros/*`
- `/register/*`

Any other paths, most importantly the ones used by headscale itself! are passed through to hs fine.

Your reverse proxy **must** send `X-Forwarded-Proto` and `X-Forwarded-Host` (caddy and nginx's
`proxy_set_header` do this). Ouroboros builds the OIDC redirect URI from them, and without them it will send
your identity provider an `http://` URI that doesn't match the one you registered.

Only an `X-Forwarded-Host` matching `public_host` is trusted, and `public_host` defaults to `hs_login_url`,
so the single-domain setup below needs no extra config. Set `public_host` explicitly if you serve ouroboros
on a different hostname to headscale. Prefer not to publish ouroboros' port on the host at all - let only
the proxy reach it.

Here's a caddy config that does this:
```caddyfile
your.server.com {
    @grpc protocol grpc
    
    handle @grpc {
        reverse_proxy h2c://headscale:50443
    }
    
    reverse_poxy /ouroboros/* ouroboros:5000
    reverse_poxy /register/* ouroboros:5000
    
    reverse_proxy headscale:8080
}
```

Done!

Note that if headscale is getting its TLS through caddy, you won't be able to use its built-in TLS support.
This means to get gRPC working to work with ouroboros, you'll need to enable insecure gRPC.
Make sure that gRPC is only exposed via caddy or not at all, in this case.

## Docker

Create a container with environment variables like this:
```yml
services:
  ouroboros:
    image: yellosink/ouroboros:0.4.0
    ports: ["8080:5000"]
    environment:
    - HS_IS_REMOTE=true
    - HS_ADDRESS=my.server.com:443
    - HS_API_KEY=mysecretkey
    - HS_LOGIN_URL=my.server.com
    - OIDC_AUTHORITY=https://authentik.my.server.com/application/o/ouroboros/
    - OIDC_CLIENT_ID=myid
    - OIDC_CLIENT_SECRET=secret
    # optional, shown with their defaults
    - OIDC_SCOPES=openid profile email
    - OIDC_USER_CLAIM=preferred_username
    - OIDC_LOGIN_TEXT=Log in with authentik
    # only needed if ouroboros is on a different hostname to headscale
    - PUBLIC_HOST=my.server.com
    - 'USER_MAP={ "sink": "sink" }'
    volumes:
    # the session signing keys - without this everyone is logged out whenever the container is recreated
    - ./ouroboros-keys:/App/keys
```

## Migrating from 0.4.x (Github login)

Ouroboros stores nothing, so there is no data migration. Your headscale users, nodes and registrations are
untouched, and no node needs re-registering. Only the config changes:

1. Create an OIDC provider as described above
2. Replace `gh_client_id` / `gh_client_secret` with `oidc_authority` / `oidc_client_id` / `oidc_client_secret`
   (in docker, `GH_CLIENT_ID` / `GH_CLIENT_SECRET` become `OIDC_AUTHORITY` / `OIDC_CLIENT_ID` / `OIDC_CLIENT_SECRET`)
3. Rekey `user_map` from github numeric ids to identity provider usernames - the values (headscale
   usernames) stay exactly as they are

```diff
   "user_map": {
-    "19270622": "sink"
+    "sink": "sink"
   }
```

If you'd rather not rekey the map, federate Github as a source in your identity provider and expose the
github id as a claim, then point `oidc_user_claim` at that claim. Keeping the numeric keys is the only
reason to do this; usernames are the easier config.

Everyone is logged out once on upgrade, as the session cookie changed.

Two things worth doing while you are in there: mount a volume at `/App/keys` (see above), and stop publishing
ouroboros' port on the host if you were - the reverse proxy is the only thing that needs to reach it.
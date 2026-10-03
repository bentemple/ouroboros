#!/bin/sh

# script used for starting ouroboros in headscale containers
# create cfg
cp cfg.docker.json cfg.json

# optional settings fall back to the defaults baked into Config.cs
OIDC_SCOPES=${OIDC_SCOPES:-"openid profile email"}
OIDC_USER_CLAIM=${OIDC_USER_CLAIM:-preferred_username}
OIDC_LOGIN_TEXT=${OIDC_LOGIN_TEXT:-"Log in"}
# empty falls back to HS_LOGIN_URL
PUBLIC_HOST=${PUBLIC_HOST:-""}

# set things
# "|" is used as the sed delimiter because several of these values are URLs
sed -i 's|"<DOCKER_IS_REMOTE>"|'"$HS_IS_REMOTE"'|' cfg.json
sed -i 's|<DOCKER_ADDRESS>|'"$HS_ADDRESS"'|' cfg.json
sed -i 's|<DOCKER_API_KEY>|'"$HS_API_KEY"'|' cfg.json
sed -i 's|<DOCKER_LOGIN_URL>|'"$HS_LOGIN_URL"'|' cfg.json
sed -i 's|<DOCKER_PUBLIC_HOST>|'"$PUBLIC_HOST"'|' cfg.json
sed -i 's|<DOCKER_OIDC_AUTHORITY>|'"$OIDC_AUTHORITY"'|' cfg.json
sed -i 's|<DOCKER_OIDC_CLIENT_ID>|'"$OIDC_CLIENT_ID"'|' cfg.json
sed -i 's|<DOCKER_OIDC_CLIENT_SECRET>|'"$OIDC_CLIENT_SECRET"'|' cfg.json
sed -i 's|<DOCKER_OIDC_SCOPES>|'"$OIDC_SCOPES"'|' cfg.json
sed -i 's|<DOCKER_OIDC_USER_CLAIM>|'"$OIDC_USER_CLAIM"'|' cfg.json
sed -i 's|<DOCKER_OIDC_LOGIN_TEXT>|'"$OIDC_LOGIN_TEXT"'|' cfg.json
sed -i 's|"<DOCKER_USER_MAP>"|'"$USER_MAP"'|' cfg.json

# run ouroboros
# using exec makes sure we are correctly passing through SIGTERMs
exec dotnet Ouroboros.dll

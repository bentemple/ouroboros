FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:35792ea4ad1db051981f62b313f1be3b46b1f45cadbaa3c288cd0d3056eefb83 AS ouro-build-env
WORKDIR /App

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# obtain headscale. renamed on the way in, so hs_bin_path does not carry the version
ARG HS_VERSION=0.29.4
RUN wget -O headscale https://github.com/juanfont/headscale/releases/download/v${HS_VERSION}/headscale_${HS_VERSION}_linux_amd64 \
 && chmod +x headscale \
 && mv headscale out

# headscale will error without a config file existing, even though its unnecessary for our use case. empty works.
RUN touch out/config.yaml

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:6c4df091e4e531bb93bdbfe7e7f0998e7ced344f54426b7e874116a3dc3233ff
WORKDIR /App
COPY --from=ouro-build-env /App/out .

EXPOSE 8080/tcp
ENTRYPOINT ["./docker-run.sh"]
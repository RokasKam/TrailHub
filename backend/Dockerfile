# syntax=docker/dockerfile:1
# One Dockerfile for every service:
#   docker build --build-arg PROJECT=src/Services/Trails/TrailHub.Trails.Api --build-arg APP_DLL=TrailHub.Trails.Api.dll .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/ ./src/
# The NuGet cache mount is shared by all service builds, so packages download once.
RUN --mount=type=cache,id=trailhub-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "$PROJECT" -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ARG APP_DLL
ENV APP_DLL=$APP_DLL
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY backend/src/MwabuLearn.Domain/MwabuLearn.Domain.csproj backend/src/MwabuLearn.Domain/
COPY backend/src/MwabuLearn.Application/MwabuLearn.Application.csproj backend/src/MwabuLearn.Application/
COPY backend/src/MwabuLearn.Infrastructure/MwabuLearn.Infrastructure.csproj backend/src/MwabuLearn.Infrastructure/
COPY backend/src/MwabuLearn.Api/MwabuLearn.Api.csproj backend/src/MwabuLearn.Api/
RUN dotnet restore backend/src/MwabuLearn.Api/MwabuLearn.Api.csproj
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/MwabuLearn.Api/MwabuLearn.Api.csproj --configuration Release --no-restore --output /out /p:UseAppHost=false -warnaserror

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/.local/content /app/.local/uploads && chown -R "$APP_UID:$APP_UID" /app/.local
COPY --from=build /out/ ./
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    HEALTHCHECK_URL=http://127.0.0.1:8080/health/live \
    ContentStorage__RootPath=/app/.local/content \
    ContentStorage__S3__SpoolDirectory=/app/.local/uploads
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD curl --fail --silent "$HEALTHCHECK_URL" || exit 1
ENTRYPOINT ["dotnet", "MwabuLearn.Api.dll"]

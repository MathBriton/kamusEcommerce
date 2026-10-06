# Build context: raiz do repositório (docker build -f apps/api/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada própria para aproveitar cache
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
COPY apps/api/Kamus.Api.csproj apps/api/
RUN dotnet restore apps/api/Kamus.Api.csproj

COPY apps/api/ apps/api/
RUN dotnet publish apps/api/Kamus.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app .
RUN mkdir -p /app/storage && chown $APP_UID /app/storage
USER $APP_UID
ENTRYPOINT ["dotnet", "Kamus.Api.dll"]

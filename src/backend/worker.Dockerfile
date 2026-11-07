FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY StajDesk.Domain/StajDesk.Domain.csproj StajDesk.Domain/
COPY StajDesk.Contracts/StajDesk.Contracts.csproj StajDesk.Contracts/
COPY StajDesk.Application/StajDesk.Application.csproj StajDesk.Application/
COPY StajDesk.Infrastructure/StajDesk.Infrastructure.csproj StajDesk.Infrastructure/
COPY StajDesk.Worker/StajDesk.Worker.csproj StajDesk.Worker/
RUN dotnet restore StajDesk.Worker/StajDesk.Worker.csproj

COPY . .
RUN dotnet publish StajDesk.Worker/StajDesk.Worker.csproj -c Release -o /app --no-restore

# Worker HTTP sunmadığı için ASP.NET'e gerek yok; daha küçük runtime imajı yeterli.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "StajDesk.Worker.dll"]

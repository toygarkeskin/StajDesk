# Çok aşamalı build: SDK imajı ile derle, küçük runtime imajı ile çalıştır.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyalarını kopyala → restore katmanı önbellekte kalır.
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY StajDesk.Domain/StajDesk.Domain.csproj StajDesk.Domain/
COPY StajDesk.Contracts/StajDesk.Contracts.csproj StajDesk.Contracts/
COPY StajDesk.Application/StajDesk.Application.csproj StajDesk.Application/
COPY StajDesk.Infrastructure/StajDesk.Infrastructure.csproj StajDesk.Infrastructure/
COPY StajDesk.Api/StajDesk.Api.csproj StajDesk.Api/
RUN dotnet restore StajDesk.Api/StajDesk.Api.csproj

COPY . .
RUN dotnet publish StajDesk.Api/StajDesk.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "StajDesk.Api.dll"]

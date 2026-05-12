# ============================================================================
# CXMTCode Web API Dockerfile - Multi-stage build
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制全部源码（含 sln 与 Directory.Build.props）
COPY . .

RUN dotnet restore CXMTCode.sln
RUN dotnet publish src/04-Web/CXMTCode.Web.Api/CXMTCode.Web.Api.csproj \
    -c Release -o /app/publish --no-restore

# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
COPY db /app/db-init

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "CXMTCode.Web.Api.dll"]

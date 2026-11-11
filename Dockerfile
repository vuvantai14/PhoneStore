# ==============================================================
# PhoneStore.Web - Multi-stage Dockerfile (.NET 8 ASP.NET Core)
# ==============================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for efficient Docker layer caching
COPY ["src/PhoneStore.Web/PhoneStore.Web.csproj", "src/PhoneStore.Web/"]
COPY ["src/PhoneStore.Business/PhoneStore.Business.csproj", "src/PhoneStore.Business/"]
COPY ["src/PhoneStore.Data/PhoneStore.Data.csproj", "src/PhoneStore.Data/"]
COPY ["src/PhoneStore.Models/PhoneStore.Models.csproj", "src/PhoneStore.Models/"]

# Restore NuGet dependencies
RUN dotnet restore "src/PhoneStore.Web/PhoneStore.Web.csproj"

# Copy remaining source code of Web and its dependent class libraries
COPY ["src/PhoneStore.Web/", "src/PhoneStore.Web/"]
COPY ["src/PhoneStore.Business/", "src/PhoneStore.Business/"]
COPY ["src/PhoneStore.Data/", "src/PhoneStore.Data/"]
COPY ["src/PhoneStore.Models/", "src/PhoneStore.Models/"]

# Publish compiled output in Release mode
WORKDIR "/src/src/PhoneStore.Web"
RUN dotnet publish "PhoneStore.Web.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Stage 2: Runtime Environment
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# .NET 8 ASP.NET Core container standard port
EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Copy published binaries from build stage
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PhoneStore.Web.dll"]

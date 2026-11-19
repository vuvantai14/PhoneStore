FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble AS build
WORKDIR /src
COPY global.json ./

COPY ["src/PhoneStore.Api/PhoneStore.Api.csproj", "src/PhoneStore.Api/"]
COPY ["src/PhoneStore.Business/PhoneStore.Business.csproj", "src/PhoneStore.Business/"]
COPY ["src/PhoneStore.Data/PhoneStore.Data.csproj", "src/PhoneStore.Data/"]
COPY ["src/PhoneStore.Models/PhoneStore.Models.csproj", "src/PhoneStore.Models/"]
RUN dotnet restore "src/PhoneStore.Api/PhoneStore.Api.csproj"

COPY ["src/PhoneStore.Api/", "src/PhoneStore.Api/"]
COPY ["src/PhoneStore.Business/", "src/PhoneStore.Business/"]
COPY ["src/PhoneStore.Data/", "src/PhoneStore.Data/"]
COPY ["src/PhoneStore.Models/", "src/PhoneStore.Models/"]
WORKDIR /src/src/PhoneStore.Api
RUN dotnet publish "PhoneStore.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PhoneStore.Api.dll"]

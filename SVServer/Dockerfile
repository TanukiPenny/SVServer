FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SVServer/SVServer.csproj", "SVServer/"]
COPY ["SVCommon/SVCommon/SVCommon.csproj", "SVCommon/SVCommon/"]
COPY ["Yggdrasil/Yggdrasil.csproj", "Yggdrasil/"]
RUN dotnet restore "SVServer/SVServer.csproj"
COPY . .
WORKDIR "/src/SVServer"
RUN dotnet build "SVServer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SVServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SVServer.dll"]

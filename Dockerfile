FROM mcr.microsoft.com/dotnet/sdk:8.0.101-alpine3.19 AS build

WORKDIR /src
COPY . .
WORKDIR "/src/SVServer"

RUN dotnet publish "SVServer/SVServer.csproj" -c Debug -o /app/publish

FROM mcr.microsoft.com/dotnet/sdk:8.0.101-alpine3.19
WORKDIR /app
COPY --from=build /app/publish .
RUN apk add --no-cache icu-libs

ENTRYPOINT ["dotnet", "SVServer.dll"]


FROM mcr.microsoft.com/dotnet/runtime:8.0

EXPOSE    9052
WORKDIR /app
COPY        ./dist . 
ENTRYPOINT [“dotnet”, “SVServer.dll”]
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY backend/Directory.Build.props backend/
COPY backend/BisiTracker.Api/BisiTracker.Api.csproj backend/BisiTracker.Api/
RUN dotnet restore backend/BisiTracker.Api/BisiTracker.Api.csproj
COPY backend/BisiTracker.Api/ backend/BisiTracker.Api/
RUN dotnet publish backend/BisiTracker.Api/BisiTracker.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "BisiTracker.Api.dll"]

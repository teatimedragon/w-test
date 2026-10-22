FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY GameStore.slnx ./
COPY src/GameStore.Core/GameStore.Core.csproj src/GameStore.Core/
COPY src/GameStore.Infrastructure/GameStore.Infrastructure.csproj src/GameStore.Infrastructure/
COPY src/GameStore.Api/GameStore.Api.csproj src/GameStore.Api/
COPY tests/GameStore.UnitTests/GameStore.UnitTests.csproj tests/GameStore.UnitTests/
COPY tests/GameStore.IntegrationTests/GameStore.IntegrationTests.csproj tests/GameStore.IntegrationTests/
RUN dotnet restore GameStore.slnx
COPY . .
RUN dotnet publish src/GameStore.Api/GameStore.Api.csproj -c Release -o /app --no-restore

# `docker build --target test backend` runs the backend unit and integration tests.
FROM build AS test
RUN dotnet test GameStore.slnx -c Release --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GameStore.Api.dll"]

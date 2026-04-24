FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BetBuilder.sln .
COPY src/BetBuilder.Domain/BetBuilder.Domain.csproj src/BetBuilder.Domain/
COPY src/BetBuilder.Application/BetBuilder.Application.csproj src/BetBuilder.Application/
COPY src/BetBuilder.Infrastructure/BetBuilder.Infrastructure.csproj src/BetBuilder.Infrastructure/
COPY src/BetBuilder.Api/BetBuilder.Api.csproj src/BetBuilder.Api/
COPY tests/BetBuilder.Tests/BetBuilder.Tests.csproj tests/BetBuilder.Tests/

RUN dotnet restore

COPY src/ src/
COPY tests/ tests/
COPY data/ data/
COPY Mock-data/ Mock-data/

RUN dotnet test tests/BetBuilder.Tests/BetBuilder.Tests.csproj -c Release --no-restore
RUN dotnet publish src/BetBuilder.Api/BetBuilder.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=build /src/data /app/data
COPY --from=build /src/Mock-data /app/mock-data

ENV Data__DataDirectory=/app/data
ENV Data__DefaultSnapshot=ts0
ENV Simulation__MockDataDirectory=/app/mock-data

EXPOSE 8080

ENTRYPOINT ["dotnet", "BetBuilder.Api.dll"]

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

RUN dotnet test tests/BetBuilder.Tests/BetBuilder.Tests.csproj -c Release --no-restore
RUN dotnet publish src/BetBuilder.Api/BetBuilder.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser

COPY --from=build /app/publish .

RUN mkdir -p /app/data && chown -R appuser:appgroup /app/data

USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV Data__DataDirectory=/app/data
ENV Data__DefaultSnapshot=ts0

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "BetBuilder.Api.dll"]

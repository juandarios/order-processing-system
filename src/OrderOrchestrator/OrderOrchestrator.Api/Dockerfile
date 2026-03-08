FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["src/OrderOrchestrator/OrderOrchestrator.Domain/OrderOrchestrator.Domain.csproj", "src/OrderOrchestrator/OrderOrchestrator.Domain/"]
COPY ["src/OrderOrchestrator/OrderOrchestrator.Application/OrderOrchestrator.Application.csproj", "src/OrderOrchestrator/OrderOrchestrator.Application/"]
COPY ["src/OrderOrchestrator/OrderOrchestrator.Infrastructure/OrderOrchestrator.Infrastructure.csproj", "src/OrderOrchestrator/OrderOrchestrator.Infrastructure/"]
COPY ["src/OrderOrchestrator/OrderOrchestrator.Api/OrderOrchestrator.Api.csproj", "src/OrderOrchestrator/OrderOrchestrator.Api/"]
RUN dotnet restore "src/OrderOrchestrator/OrderOrchestrator.Api/OrderOrchestrator.Api.csproj"
COPY . .
WORKDIR "/src/src/OrderOrchestrator/OrderOrchestrator.Api"
RUN dotnet build "OrderOrchestrator.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderOrchestrator.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
# Required by Confluent.Kafka: libgssapi_krb5 is needed at runtime in Debian/Ubuntu-based images.
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderOrchestrator.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["src/PaymentService/PaymentService.Domain/PaymentService.Domain.csproj", "src/PaymentService/PaymentService.Domain/"]
COPY ["src/PaymentService/PaymentService.Application/PaymentService.Application.csproj", "src/PaymentService/PaymentService.Application/"]
COPY ["src/PaymentService/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj", "src/PaymentService/PaymentService.Infrastructure/"]
COPY ["src/PaymentService/PaymentService.Api/PaymentService.Api.csproj", "src/PaymentService/PaymentService.Api/"]
RUN dotnet restore "src/PaymentService/PaymentService.Api/PaymentService.Api.csproj"
COPY . .
WORKDIR "/src/src/PaymentService/PaymentService.Api"
RUN dotnet build "PaymentService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PaymentService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
# Required by Confluent.Kafka (transitive dependency): libgssapi_krb5 is needed at runtime in Debian/Ubuntu-based images.
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PaymentService.Api.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Mocks/PaymentGateway/PaymentGateway.csproj", "src/Mocks/PaymentGateway/"]
RUN dotnet restore "src/Mocks/PaymentGateway/PaymentGateway.csproj"
COPY . .
WORKDIR "/src/src/Mocks/PaymentGateway"
RUN dotnet build "PaymentGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PaymentGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PaymentGateway.dll"]

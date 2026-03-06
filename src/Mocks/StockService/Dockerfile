FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Mocks/StockService/StockService.csproj", "src/Mocks/StockService/"]
RUN dotnet restore "src/Mocks/StockService/StockService.csproj"
COPY . .
WORKDIR "/src/src/Mocks/StockService"
RUN dotnet build "StockService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "StockService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StockService.dll"]

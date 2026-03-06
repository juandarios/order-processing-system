FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["src/OrderProducer/OrderProducer.csproj", "src/OrderProducer/"]
RUN dotnet restore "src/OrderProducer/OrderProducer.csproj"
COPY . .
WORKDIR "/src/src/OrderProducer"
RUN dotnet build "OrderProducer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderProducer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderProducer.dll"]

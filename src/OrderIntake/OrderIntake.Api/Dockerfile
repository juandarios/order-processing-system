FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Shared/Shared.csproj", "src/Shared/"]
COPY ["src/OrderIntake/OrderIntake.Domain/OrderIntake.Domain.csproj", "src/OrderIntake/OrderIntake.Domain/"]
COPY ["src/OrderIntake/OrderIntake.Application/OrderIntake.Application.csproj", "src/OrderIntake/OrderIntake.Application/"]
COPY ["src/OrderIntake/OrderIntake.Infrastructure/OrderIntake.Infrastructure.csproj", "src/OrderIntake/OrderIntake.Infrastructure/"]
COPY ["src/OrderIntake/OrderIntake.Api/OrderIntake.Api.csproj", "src/OrderIntake/OrderIntake.Api/"]
RUN dotnet restore "src/OrderIntake/OrderIntake.Api/OrderIntake.Api.csproj"
COPY . .
WORKDIR "/src/src/OrderIntake/OrderIntake.Api"
RUN dotnet build "OrderIntake.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderIntake.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderIntake.Api.dll"]

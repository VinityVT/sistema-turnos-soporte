FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SistemaTurnosSoporte.csproj", "."]
RUN dotnet restore "SistemaTurnosSoporte.csproj"
COPY . .
RUN dotnet build "SistemaTurnosSoporte.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SistemaTurnosSoporte.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SistemaTurnosSoporte.dll"]
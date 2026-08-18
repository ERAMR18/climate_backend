# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Services/SensorService/Climate.Sensors.Api/Climate.Sensors.Api.csproj"
RUN dotnet publish "src/Services/SensorService/Climate.Sensors.Api/Climate.Sensors.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Climate.Sensors.Api.dll"]

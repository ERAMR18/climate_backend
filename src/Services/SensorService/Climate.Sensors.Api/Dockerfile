# syntax=docker/dockerfile:1
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.10@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b
FROM ${SDK_IMAGE} AS build
WORKDIR /src
COPY . .
RUN --mount=type=cache,id=climate-nuget-v2,target=/root/.nuget/packages,sharing=locked dotnet restore --locked-mode "src/Services/SensorService/Climate.Sensors.Api/Climate.Sensors.Api.csproj"
RUN --mount=type=cache,id=climate-nuget-v2,target=/root/.nuget/packages,sharing=locked dotnet publish "src/Services/SensorService/Climate.Sensors.Api/Climate.Sensors.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

RUN --mount=type=cache,id=climate-nuget-v2,target=/root/.nuget/packages,sharing=locked dotnet publish tools/HealthProbe/Climate.HealthProbe.csproj -c Release -o /app/health /p:UseAppHost=false

FROM ${RUNTIME_IMAGE} AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/health ./health
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Climate.Sensors.Api.dll"]

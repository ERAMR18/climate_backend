FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/CommunityService/Climate.Communities.Api/Climate.Communities.Api.csproj --locked-mode
RUN dotnet publish src/Services/CommunityService/Climate.Communities.Api/Climate.Communities.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false
RUN dotnet publish tools/HealthProbe/Climate.HealthProbe.csproj -c Release -o /app/health /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.10
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/health ./health
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s CMD ["dotnet", "/app/health/Climate.HealthProbe.dll", "http://localhost:8080/health/ready"]
ENTRYPOINT ["dotnet", "Climate.Communities.Api.dll"]

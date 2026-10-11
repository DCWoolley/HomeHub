FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/HomeHub.Web/HomeHub.Web.csproj src/HomeHub.Web/
RUN dotnet restore src/HomeHub.Web/HomeHub.Web.csproj
COPY src/ src/
RUN dotnet publish src/HomeHub.Web/HomeHub.Web.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false
RUN test -s /app/publish/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080; printf "GET /health/live HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3; head -n 1 <&3 | grep -q "200 OK"' || exit 1
ENTRYPOINT ["dotnet", "HomeHub.Web.dll"]

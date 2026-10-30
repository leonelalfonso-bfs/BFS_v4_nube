# Imagen de la nube (Bfs.Api en modo Nube). Se arma desde la raíz del repositorio:
#   docker build -f deploy/Dockerfile -t bfs-nube .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Bfs.Api/Bfs.Api.csproj -c Release -o /app -p:DebugType=none \
    && rm -f /app/appsettings.Development.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# tzdata: fechas de tickets y reportes en hora de Argentina. libfontconfig1: lo pide el motor de PDF.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

# Datos que tienen que sobrevivir a una actualización (logos, clave de sesiones, logs): volumen /data.
RUN mkdir -p /data && chown app:app /data
VOLUME /data
ENV Bfs__Modo=Nube \
    Bfs__DataPath=/data \
    Urls=http://0.0.0.0:8080 \
    TZ=America/Argentina/Buenos_Aires
EXPOSE 8080
USER app

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD ["dotnet", "Bfs.Api.dll", "--salud"]
ENTRYPOINT ["dotnet", "Bfs.Api.dll"]

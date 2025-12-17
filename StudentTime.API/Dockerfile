# Étape 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copier les fichiers de projet
COPY ["StudentTIme.API/StudentTIme.API.csproj", "StudentTIme.API/"]
COPY ["StudentTime.Core/StudentTime.Core.csproj", "StudentTime.Core/"]
COPY ["StudentTime.Infrastructure/StudentTime.Infrastructure.csproj", "StudentTime.Infrastructure/"]

# Restaurer les dépendances
RUN dotnet restore "StudentTIme.API/StudentTIme.API.csproj"

# Copier le reste du code
COPY . .

# Build l'application
WORKDIR "/src/StudentTIme.API"
RUN dotnet build "StudentTIme.API.csproj" -c Release -o /app/build

# Étape 2: Publish
FROM build AS publish
RUN dotnet publish "StudentTIme.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Étape 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Créer un utilisateur non-root
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copier les fichiers publiés
COPY --from=publish /app/publish .

# Créer le dossier pour les logs
RUN mkdir -p /app/logs

# Script pour utiliser la variable PORT de Railway (créé avant de changer d'utilisateur)
RUN echo '#!/bin/sh' > /app/docker-entrypoint.sh && \
    echo 'set -e' >> /app/docker-entrypoint.sh && \
    echo 'PORT=${PORT:-8080}' >> /app/docker-entrypoint.sh && \
    echo 'export ASPNETCORE_URLS="http://+:$PORT"' >> /app/docker-entrypoint.sh && \
    echo 'echo "Starting API on port $PORT"' >> /app/docker-entrypoint.sh && \
    echo 'exec dotnet StudentTIme.API.dll' >> /app/docker-entrypoint.sh && \
    chmod +x /app/docker-entrypoint.sh

# Changer les permissions de /app pour appuser
RUN chown -R appuser:appuser /app

# Passer à l'utilisateur non-root
USER appuser

# Exposer le port (Railway utilise la variable PORT)
ENV PORT=8080
EXPOSE 8080

# Variables d'environnement
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["/app/docker-entrypoint.sh"]


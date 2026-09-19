# syntax=docker/dockerfile:1

# Imagen de la API (plan.md, Target Platform: "API contenedorizada en Linux").
#
# El contexto de construcción es la raíz del repositorio, no backend/: así la compilación usa el mismo
# .editorconfig y Directory.Build.props que el build local. Con TreatWarningsAsErrors activado, compilar
# con otra configuración de analizadores podría producir un binario distinto al que validan las pruebas.
#
#   docker build -f backend/Dockerfile -t enterprise-access-control-api .

# --- Compilación ----------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY backend/.editorconfig backend/Directory.Build.props backend/

# Los .csproj primero: la restauración queda en su propia capa y solo se repite si cambian las
# dependencias, no con cada cambio de código.
COPY backend/src/EnterpriseAccessControl.Domain/EnterpriseAccessControl.Domain.csproj backend/src/EnterpriseAccessControl.Domain/
COPY backend/src/EnterpriseAccessControl.Application/EnterpriseAccessControl.Application.csproj backend/src/EnterpriseAccessControl.Application/
COPY backend/src/EnterpriseAccessControl.Infrastructure/EnterpriseAccessControl.Infrastructure.csproj backend/src/EnterpriseAccessControl.Infrastructure/
COPY backend/src/EnterpriseAccessControl.Api/EnterpriseAccessControl.Api.csproj backend/src/EnterpriseAccessControl.Api/

RUN dotnet restore backend/src/EnterpriseAccessControl.Api/EnterpriseAccessControl.Api.csproj

COPY backend/src/ backend/src/

RUN dotnet publish backend/src/EnterpriseAccessControl.Api/EnterpriseAccessControl.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# --- Ejecución ------------------------------------------------------------------------------------
# aspnet (Debian) y no la variante "chiseled": Microsoft.Data.SqlClient necesita ICU
# (InvariantGlobalization=false, ver Directory.Build.props).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Usuario sin privilegios que la imagen oficial ya provee.
USER $APP_UID

ENTRYPOINT ["dotnet", "EnterpriseAccessControl.Api.dll"]

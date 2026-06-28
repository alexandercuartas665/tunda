# =========================================================================
#  deploy-en-servidor.ps1
#  ------------------------------------------------------------------------
#  Script para correr DIRECTAMENTE en la PowerShell del servidor Windows
#  (entrando por Terminal Server / RDP). Hace TODO el despliegue de DokTrino
#  en un solo paso:
#
#    1) Valida prerrequisitos (Docker corriendo, archivos presentes).
#    2) Si no existe .env, lo genera con clave Postgres aleatoria de 32 chars.
#    3) Login a GHCR si la imagen es privada (opcional).
#    4) docker compose pull && docker compose up -d.
#    5) Espera a que Postgres responda (healthcheck).
#    6) Restaura el dump .dump dentro del container Postgres.
#    7) Reinicia doktrino-app para que EF Core suelte conexiones viejas.
#    8) Hace un probe HTTP a /login y te imprime un resumen.
#
#  Que tienes que tener en el server, en una carpeta (por ejemplo
#  E:\DOCKER\doktrino\), antes de correr esto:
#
#    .\docker-compose.yml          (del repo, ya esta)
#    .\.env.example                (del repo, ya esta)
#    .\deploy-en-servidor.ps1      (este archivo)
#    .\dumps\doktrino_dev.dump        (tu dump fresco de dev)
#
#  Uso minimo:
#
#    cd E:\DOCKER\doktrino
#    .\deploy-en-servidor.ps1
#
#  Parametros opcionales:
#    -DeployDir       carpeta del deploy (default: el directorio actual)
#    -DumpPath        ruta al .dump (default: el .dump mas reciente en .\dumps)
#    -DokTrinoPort       puerto local que expone doktrino-app (default: 5380)
#    -PostgresDb      nombre de la BD (default: doktrino)
#    -PostgresUser    usuario de la BD (default: doktrino)
#    -GhcrUser        usuario GitHub para GHCR si la imagen es privada
#    -GhcrToken       PAT con read:packages
#    -SkipRestore     no restaurar dump (solo deploy)
#    -Force           no preguntar confirmaciones (uso desatendido)
# =========================================================================

[CmdletBinding()]
param(
    [string]$DeployDir = (Get-Location).Path,
    [string]$DumpPath = "",
    [int]$DokTrinoPort = 5380,
    [string]$PostgresDb = "doktrino",
    [string]$PostgresUser = "doktrino",
    [string]$DokTrinoImage = "ghcr.io/alexandercuartas665/doktrino/superadmin:latest",
    [string]$GhcrUser = "",
    [string]$GhcrToken = "",
    [switch]$SkipRestore,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# ---------- helpers de salida -------------------------------------------
function Step($msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Ok($msg)   { Write-Host "    OK    $msg" -ForegroundColor Green }
function Info($msg) { Write-Host "    info  $msg" -ForegroundColor Gray }
function Warn($msg) { Write-Host "    WARN  $msg" -ForegroundColor Yellow }
function Err($msg)  { Write-Host "    ERR   $msg" -ForegroundColor Red }

function Confirm-Or-Exit($message) {
    if ($Force) { return }
    Write-Host ""
    $resp = Read-Host "$message  [s/N]"
    if ($resp -notmatch '^[sSyY]') {
        Warn "Cancelado por el usuario."
        exit 1
    }
}

function New-StrongPassword {
    # 32 bytes random -> base64 url-safe (sin = / +).
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $b64 = [Convert]::ToBase64String($bytes)
    return $b64.Replace('+','A').Replace('/','B').Replace('=','')
}

# ---------- 0) Banner ---------------------------------------------------
Write-Host ""
Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host " DokTrino IPS RT - Deploy en servidor de produccion" -ForegroundColor Cyan
Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host " DeployDir : $DeployDir"
Write-Host " Imagen    : $DokTrinoImage"
Write-Host " Puerto    : 127.0.0.1:$DokTrinoPort -> doktrino-app:8080"
Write-Host " BD        : $PostgresDb (usuario $PostgresUser)"
Write-Host ""

# ---------- 1) Validaciones de entorno ----------------------------------
Step "Validando entorno"

# 1.1 Carpeta de deploy
if (-not (Test-Path $DeployDir)) {
    Err "La carpeta '$DeployDir' no existe."
    exit 1
}
Set-Location $DeployDir
Ok "Carpeta de deploy: $DeployDir"

# 1.2 Docker disponible
try {
    $dockerVersion = docker version --format '{{.Server.Version}}' 2>$null
    if (-not $dockerVersion) { throw "no responde" }
    Ok "Docker server: $dockerVersion"
} catch {
    Err "Docker no esta corriendo o no esta instalado en este server."
    Err "Abre Docker Desktop y vuelve a correr el script."
    exit 1
}

# 1.3 docker-compose.yml presente
if (-not (Test-Path ".\docker-compose.yml")) {
    Err "No encuentro docker-compose.yml en $DeployDir."
    Err "Copia el archivo desde el repo (deploy/docker-prod/docker-compose.yml) y reintenta."
    exit 1
}
Ok "docker-compose.yml presente"

# 1.4 Dump presente (a menos que se haya pedido -SkipRestore)
if (-not $SkipRestore) {
    if (-not $DumpPath) {
        if (-not (Test-Path ".\dumps")) {
            Err "No existe la carpeta '.\dumps'. Crea la carpeta y mete tu archivo .dump."
            Err "O corre con -SkipRestore si solo quieres levantar el stack sin datos."
            exit 1
        }
        $candidato = Get-ChildItem .\dumps -Filter "*.dump" |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if (-not $candidato) {
            Err "No encuentro ningun archivo .dump en .\dumps\"
            Err "Copia tu dump ahi o corre con -SkipRestore."
            exit 1
        }
        $DumpPath = $candidato.FullName
    } else {
        if (-not (Test-Path $DumpPath)) {
            Err "No existe el archivo '$DumpPath'."
            exit 1
        }
    }
    $sizeMB = [math]::Round((Get-Item $DumpPath).Length / 1MB, 3)
    Ok "Dump a restaurar: $DumpPath ($sizeMB MB)"
}

# ---------- 2) Generar / leer .env --------------------------------------
Step "Configurando .env"

$envPath = Join-Path $DeployDir ".env"
$envFresco = $false

if (-not (Test-Path $envPath)) {
    Warn ".env no existe. Lo genero con clave aleatoria."
    $pwd = New-StrongPassword
    $envContent = @"
# Generado por deploy-en-servidor.ps1 el $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
# NO versionar este archivo. Contiene secretos de produccion.

DOKTRINO_IMAGE=$DokTrinoImage
DOKTRINO_PORT=$DokTrinoPort
POSTGRES_DB=$PostgresDb
POSTGRES_USER=$PostgresUser
POSTGRES_PASSWORD=$pwd
"@
    Set-Content -Path $envPath -Value $envContent -Encoding UTF8
    $envFresco = $true
    Ok ".env generado. Password Postgres: $pwd"
    Warn "Guarda esta clave en un gestor de secretos AHORA."
} else {
    Ok ".env ya existe (lo respeto, no lo toco)"
    # Leer POSTGRES_DB y POSTGRES_USER del archivo por si fueron cambiados
    $envLines = Get-Content $envPath
    foreach ($line in $envLines) {
        if ($line -match '^\s*POSTGRES_DB\s*=\s*(.+)\s*$') { $PostgresDb = $matches[1].Trim() }
        if ($line -match '^\s*POSTGRES_USER\s*=\s*(.+)\s*$') { $PostgresUser = $matches[1].Trim() }
    }
    Info "BD efectiva (del .env): $PostgresDb / $PostgresUser"
}

# ---------- 3) Login GHCR (opcional) ------------------------------------
if ($GhcrUser -and $GhcrToken) {
    Step "Login en GHCR"
    $GhcrToken | docker login ghcr.io -u $GhcrUser --password-stdin
    if ($LASTEXITCODE -ne 0) {
        Err "Fallo el login en GHCR. Revisa el PAT (necesita scope read:packages)."
        exit 1
    }
    Ok "Login GHCR OK"
} else {
    Info "Sin credenciales GHCR. Si la imagen es privada, fallara el pull."
    Info "Para login despues:  echo `$PAT | docker login ghcr.io -u tu-usuario --password-stdin"
}

# ---------- 4) Pull + up ------------------------------------------------
Step "Descargando imagenes (docker compose pull)"
docker compose --env-file ".\.env" pull
if ($LASTEXITCODE -ne 0) {
    Err "Fallo docker compose pull. Revisa GHCR / red."
    exit 1
}
Ok "Imagenes al dia"

Step "Levantando stack (docker compose up -d)"
docker compose --env-file ".\.env" up -d
if ($LASTEXITCODE -ne 0) {
    Err "Fallo docker compose up -d."
    exit 1
}
Ok "Stack levantado"

# ---------- 5) Esperar a Postgres ---------------------------------------
Step "Esperando a que Postgres este listo"

$maxIntentos = 30
$intento = 0
while ($intento -lt $maxIntentos) {
    $intento++
    $estado = docker inspect doktrino-postgres-prod --format '{{.State.Health.Status}}' 2>$null
    if ($estado -eq 'healthy') {
        Ok "Postgres healthy en ~$($intento * 2) seg"
        break
    }
    Start-Sleep -Seconds 2
}
if ($estado -ne 'healthy') {
    Err "Postgres no llego a 'healthy' en $($maxIntentos * 2) seg."
    Err "Revisa con:  docker logs doktrino-postgres-prod --tail 100"
    exit 1
}

# ---------- 6) Restaurar dump -------------------------------------------
if (-not $SkipRestore) {
    Step "Verificando si la BD ya tiene datos"

    $envPass = (Get-Content $envPath | Select-String '^POSTGRES_PASSWORD=').ToString().Split('=',2)[1]
    $countRes = docker exec -e PGPASSWORD=$envPass doktrino-postgres-prod psql -U $PostgresUser -d $PostgresDb -At -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public';" 2>$null
    $countTablas = 0
    if ($countRes -match '^\d+$') { $countTablas = [int]$countRes }

    if ($countTablas -gt 0) {
        Warn "La BD '$PostgresDb' ya tiene $countTablas tablas en el schema public."
        Warn "El restore con --clean --if-exists va a DROPEAR y recrear TODO."
        Confirm-Or-Exit "Estas seguro que quieres reemplazar los datos actuales por los del dump?"
    } else {
        Info "BD vacia (0 tablas). Restore limpio."
    }

    Step "Copiando dump adentro del container Postgres"
    docker cp $DumpPath doktrino-postgres-prod:/tmp/restore.dump
    if ($LASTEXITCODE -ne 0) { Err "Fallo el docker cp"; exit 1 }
    Ok "Dump dentro del container: /tmp/restore.dump"

    Step "Ejecutando pg_restore"
    docker exec -e PGPASSWORD=$envPass doktrino-postgres-prod pg_restore `
        -U $PostgresUser -d $PostgresDb `
        --no-owner --no-privileges --clean --if-exists `
        /tmp/restore.dump
    # pg_restore puede salir con code 1 si hubo WARNINGS no fatales (extensiones, etc).
    # Toleramos 0 y 1; cualquier otro codigo es error real.
    if ($LASTEXITCODE -gt 1) {
        Err "pg_restore fallo con exit code $LASTEXITCODE."
        Err "Revisa los logs arriba para ver que tabla rompio."
        exit 1
    }
    if ($LASTEXITCODE -eq 1) {
        Warn "pg_restore termino con warnings (exit 1). Normal si la BD estaba vacia."
        Warn "Revisa arriba: si solo dice 'does not exist, skipping' esta perfecto."
    }
    Ok "Restore terminado"

    # 6.x Limpiar el dump del container
    docker exec doktrino-postgres-prod rm -f /tmp/restore.dump 2>$null | Out-Null

    # 6.y Conteo rapido de filas en tablas criticas
    Step "Verificando conteo de filas restauradas"
    $verSql = @"
SELECT 'tenants                ' || COUNT(*) FROM tenants
UNION ALL SELECT 'platform_users         ' || COUNT(*) FROM platform_users
UNION ALL SELECT 'tenant_users           ' || COUNT(*) FROM tenant_users
UNION ALL SELECT 'roles                  ' || COUNT(*) FROM roles
UNION ALL SELECT 'rol_permisos           ' || COUNT(*) FROM rol_permisos
UNION ALL SELECT 'sucursales             ' || COUNT(*) FROM sucursales
UNION ALL SELECT 'tenant_user_sucursales ' || COUNT(*) FROM tenant_user_sucursales
UNION ALL SELECT 'aseguradoras           ' || COUNT(*) FROM aseguradoras
UNION ALL SELECT 'contratos_aseguradora  ' || COUNT(*) FROM contratos_aseguradora
UNION ALL SELECT 'servicios_contrato     ' || COUNT(*) FROM servicios_contrato
UNION ALL SELECT 'profesionales          ' || COUNT(*) FROM profesionales
UNION ALL SELECT 'pacientes              ' || COUNT(*) FROM pacientes
UNION ALL SELECT 'catalogos_paciente     ' || COUNT(*) FROM catalogos_paciente
UNION ALL SELECT 'form_definitions       ' || COUNT(*) FROM form_definitions
UNION ALL SELECT 'asignaciones           ' || COUNT(*) FROM asignaciones
UNION ALL SELECT 'asignacion_lotes       ' || COUNT(*) FROM asignacion_lotes
UNION ALL SELECT 'asignacion_turnos      ' || COUNT(*) FROM asignacion_turnos
UNION ALL SELECT 'historias_clinicas     ' || COUNT(*) FROM historias_clinicas
UNION ALL SELECT 'cie11configs           ' || COUNT(*) FROM cie11configs
UNION ALL SELECT 'paises                 ' || COUNT(*) FROM paises
UNION ALL SELECT 'departamentos          ' || COUNT(*) FROM departamentos
UNION ALL SELECT 'municipios             ' || COUNT(*) FROM municipios;
"@
    docker exec -e PGPASSWORD=$envPass doktrino-postgres-prod psql -U $PostgresUser -d $PostgresDb -At -c $verSql
}

# ---------- 7) Restart de la app ----------------------------------------
Step "Reiniciando doktrino-app (para que EF Core suelte caches/conexiones)"
docker restart doktrino-app | Out-Null
Start-Sleep -Seconds 5
Ok "doktrino-app reiniciado"

# ---------- 8) Health probe HTTP ----------------------------------------
Step "Probe HTTP a http://localhost:$DokTrinoPort/login"
$intento = 0
$ok = $false
while ($intento -lt 20) {
    $intento++
    try {
        $resp = Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:$DokTrinoPort/login" -TimeoutSec 5
        if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 400) {
            $ok = $true
            Ok "HTTP $($resp.StatusCode) en ~$($intento * 3) seg. La app responde."
            break
        }
    } catch {
        # sigue intentando
    }
    Start-Sleep -Seconds 3
}
if (-not $ok) {
    Warn "La app no respondio en ~60 seg. Esto puede pasar si EF Core esta aplicando migrations."
    Warn "Revisa con:  docker logs doktrino-app --tail 100 -f"
}

# ---------- 9) Resumen final --------------------------------------------
Write-Host ""
Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host " Deploy terminado" -ForegroundColor Cyan
Write-Host "=========================================================================" -ForegroundColor Cyan
Write-Host " URL local del server  : http://localhost:$DokTrinoPort/login"
Write-Host " Container app         : doktrino-app"
Write-Host " Container Postgres    : doktrino-postgres-prod (no expuesto al host)"
Write-Host " Volumen BD            : doktrino-prod_doktrino-pgdata"
Write-Host " .env                  : $envPath"
Write-Host ""
Write-Host " Siguientes pasos:" -ForegroundColor Yellow
if ($envFresco) {
    Write-Host "   1) Apunta tu reverse proxy global (nginx-proxy-manager / traefik) a"
    Write-Host "      http://localhost:$DokTrinoPort  (acuerdate de pasar WebSockets para Blazor)."
}
Write-Host "   2) Entra al sistema y CAMBIA los passwords de:"
Write-Host "        admin@doktrino.travels"
Write-Host "        demo-admin@doktrino.travels"
Write-Host "      (el dump trae los hash de dev con Admin123*; en prod no puede quedar)."
Write-Host ""
Write-Host " Para ver logs:        docker logs doktrino-app -f --tail 100" -ForegroundColor Gray
Write-Host " Para bajar el stack:  docker compose down       (datos se conservan)" -ForegroundColor Gray
Write-Host " Para actualizar:      docker compose pull && docker compose up -d" -ForegroundColor Gray
Write-Host ""

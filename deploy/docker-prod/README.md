# Despliegue DokTrino en otro servidor con Docker

Este compose asume que el servidor **ya tiene un reverse proxy global** (nginx-proxy-manager, traefik, otro Caddy, etc.) escuchando en 80/443 y manejando TLS para todos los servicios. Este stack solo expone doktrino-app en un **puerto local uncommon** que tu proxy reenvia.

```
Internet
   │  (TLS Let's Encrypt en TU proxy global)
   ▼
[ tu reverse proxy global :80/:443 ]
   │  (HTTP, red local del servidor)
   ▼
[ doktrino-app : ${DOKTRINO_PORT} ]  ──TCP red interna──▶  [ postgres :5432 ]
   (bindeado a 127.0.0.1 del host,
    inalcanzable desde internet)
```

---

## 1. En tu maquina (una sola vez): publicar la imagen

Cada vez que pushees a `main`, GitHub Actions construye y publica:
- `ghcr.io/alexandercuartas665/tunda/superadmin:latest` (ultimo de main)
- `ghcr.io/alexandercuartas665/tunda/superadmin:sha-<7chars>` (immutable por commit)
- Si haces tag `vX.Y.Z`: tambien `vX.Y.Z` y `vX.Y`.

Mira el progreso en GitHub → **Actions** tab.

### ¿Imagen privada o publica?
Por default es **privada**. Hacerla publica (sin login en el servidor):
GitHub → tu perfil → **Packages** → `doktrino/superadmin` → **Package settings** → "Change visibility" → **Public**.

Si la dejas privada, en el servidor necesitas `docker login ghcr.io` con un PAT que tenga `read:packages`.

---

## 2. Primer deploy: bootstrap-server.ps1 (un solo comando)

Si es la primera vez que despliegas en un servidor Windows con Docker, usa el bootstrap. Crea la carpeta, sube los archivos, genera la clave Postgres y deja el stack arriba.

```pwsh
cd C:\DesarrolloIA\DokTrino\deploy\docker-prod

# Imagen publica:
.\bootstrap-server.ps1 -RemoteSsh bit-admin@10.0.1.6 `
                       -Password 'tu_password_windows' `
                       -DeployDir 'E:\DOCKER\doktrino'

# Imagen privada en GHCR (agrega PAT con read:packages):
.\bootstrap-server.ps1 -RemoteSsh bit-admin@10.0.1.6 `
                       -Password 'tu_password_windows' `
                       -DeployDir 'E:\DOCKER\doktrino' `
                       -GhcrUser alexandercuartas665 `
                       -GhcrToken 'ghp_xxxxx'
```

Por defecto el script:
- Encuentra el primer puerto libre desde 5380 (pasa `-PreferredPort` para cambiar)
- Crea `E:\DOCKER\doktrino\` en el server (pasa `-DeployDir` para cambiar)
- Genera password Postgres aleatoria de 32 chars y la deja en `.env` del server
- Bindea el puerto a `127.0.0.1` del server (no expuesto a internet sin proxy)

> **Requisito local:** PuTTY instalado (`winget install --id PuTTY.PuTTY`).

Despues del primer deploy, cambia a `deploy-remote.ps1` para mantenimiento (usa SSH keys, no password).

---

## 3. Mantenimiento desde TU MAQUINA con Docker context SSH

En vez de SSH-ear al servidor y editar archivos alla, conectas tu Docker CLI local al daemon del server por SSH. Todo `docker compose ...` que ejecutes desde tu maquina corre alla. Los archivos (`docker-compose.yml`, `.env`) viven en TU maquina.

### Pre-requisitos
- Acceso SSH al server con clave (entrar sin pedir password): `ssh user@server` debe funcionar.
- Tu usuario en el server debe estar en el grupo `docker`: `sudo usermod -aG docker $USER` (y relogin).
- Docker 24+ en tu maquina.

### Setup (una sola vez)

```pwsh
cd C:\DesarrolloIA\DokTrino\deploy\docker-prod

# 1) Copia el .env de ejemplo y edita DOKTRINO_PORT, POSTGRES_*, etc.
Copy-Item .env.example .env
notepad .env

# 2) Crea el context apuntando a tu server
.\deploy-remote.ps1 setup-context -RemoteSsh acuartas@doktrino.tudominio.com

# (Opcional) Persistir el SSH del server en tu sesion para no escribirlo cada vez:
[Environment]::SetEnvironmentVariable("DOKTRINO_REMOTE_SSH", "acuartas@doktrino.tudominio.com", "User")
```

### Deploy / operaciones

```pwsh
.\deploy-remote.ps1 deploy     # pull la imagen + up -d en el SERVER + tail logs
.\deploy-remote.ps1 logs       # tail logs del SERVER
.\deploy-remote.ps1 status     # docker compose ps en el SERVER
.\deploy-remote.ps1 restart    # restart doktrino-app en el SERVER
.\deploy-remote.ps1 backup     # pg_dump del SERVER bajando el .sql.gz a backups/ aqui
.\deploy-remote.ps1 shell      # bash dentro de doktrino-app
.\deploy-remote.ps1 psql       # psql dentro de postgres
.\deploy-remote.ps1 down       # bajar el stack (datos quedan en volumen)
```

> Bajo el capo el script ejecuta `docker --context doktrino-prod compose --env-file .env ...`. Si prefieres a mano, puedes usar esos comandos directos sin el script.

### Si ya no quieres el control remoto desde local

```pwsh
.\deploy-remote.ps1 remove-context
```

Esto NO toca el servidor — solo quita el atajo en tu Docker local. El stack sigue corriendo alla.

---

## 4. Setup directo en el servidor (alternativa manual)

### Requisitos
- Docker 24+ con `docker compose v2`
- Tu reverse proxy global ya esta operando en 80/443
- Puerto local que NO este en uso por otros servicios (default `5380`)

### Pasos

```bash
# 1. Carpeta de despliegue
mkdir -p /opt/doktrino && cd /opt/doktrino

# 2. Bajar los archivos (no necesitas el repo completo)
curl -O https://raw.githubusercontent.com/alexandercuartas665/DOKTRINO/main/deploy/docker-prod/docker-compose.yml
curl -O https://raw.githubusercontent.com/alexandercuartas665/DOKTRINO/main/deploy/docker-prod/.env.example
curl -O https://raw.githubusercontent.com/alexandercuartas665/DOKTRINO/main/deploy/docker-prod/deploy.sh
chmod +x deploy.sh
cp .env.example .env

# 3. Edita .env: DOKTRINO_PORT, POSTGRES_PASSWORD, etc.
nano .env

# 4. Si la imagen quedo PRIVADA en GHCR, login una sola vez:
echo "TU_PAT_AQUI" | docker login ghcr.io -u alexandercuartas665 --password-stdin

# 5. Arrancar
docker compose pull
docker compose up -d

# 6. Verificar
curl http://127.0.0.1:5380/login          # debe responder HTML del login
docker compose logs -f doktrino-app
```

---

## 5. Apuntar tu reverse proxy global hacia doktrino-app

Solo tienes que decirle a TU proxy global "para `doktrino.midominio.com`, reverse_proxy a `http://localhost:5380`".

### Si tu proxy global es **nginx-proxy-manager**
1. Proxy Hosts → Add Proxy Host
2. Domain Names: `doktrino.midominio.com`
3. Scheme: `http`
4. Forward Hostname/IP: `host.docker.internal` o `127.0.0.1`
5. Forward Port: `5380`
6. ✅ Block Common Exploits
7. ✅ **Websockets Support** (CRITICO — Blazor Server usa WS para el circuito SignalR)
8. Tab SSL → Request a new SSL Certificate (Let's Encrypt) → Force SSL ON

### Si tu proxy global es **Caddy** (en otra parte del servidor)
En el `Caddyfile` global:
```caddy
doktrino.midominio.com {
    reverse_proxy 127.0.0.1:5380
}
```
Caddy maneja TLS + WebSockets automaticamente.

### Si tu proxy global es **Traefik** (con docker labels)
Mueve el `labels:` al servicio `doktrino-app` en el compose:
```yaml
doktrino-app:
  ...
  labels:
    - "traefik.enable=true"
    - "traefik.http.routers.doktrino.rule=Host(`doktrino.midominio.com`)"
    - "traefik.http.routers.doktrino.entrypoints=websecure"
    - "traefik.http.routers.doktrino.tls.certresolver=le"
    - "traefik.http.services.doktrino.loadbalancer.server.port=8080"
  # En este caso quita el ports: del servicio (Traefik llega por red interna).
```
Y conecta doktrino-app a la red de Traefik.

### Si tu proxy global es **nginx clasico**
```nginx
server {
    listen 443 ssl http2;
    server_name doktrino.midominio.com;
    ssl_certificate ...;
    ssl_certificate_key ...;

    location / {
        proxy_pass http://127.0.0.1:5380;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;     # WebSockets para SignalR
        proxy_set_header Connection "upgrade";       #
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400;                    # Blazor mantiene la conexion
    }
}
```

> **Importante en todos los casos:** asegurate de que el proxy soporte y permita **WebSockets** para `doktrino.midominio.com`. Sin eso, Blazor Server cae a long-polling (funciona pero lento).

---

## 6. Updates rutinarios (en el server)

```bash
cd /opt/doktrino
./deploy.sh           # pull + up + ultimas 30 lineas de logs
./deploy.sh logs      # tail logs del app
./deploy.sh backup    # dump de postgres a ./backups/
./deploy.sh status    # docker compose ps
```

`DOKTRINO_RUN_MIGRATIONS=true` aplica las migraciones EF nuevas al arrancar. Los datos persisten en el volumen `doktrino-pgdata`.

### Pinear una version concreta
En `.env`:
```env
DOKTRINO_IMAGE=ghcr.io/alexandercuartas665/tunda/superadmin:sha-abcd123
```
Luego `docker compose up -d`.

---

## 7. Backups de Postgres

### Importar tu BD de dev al primer arranque del server

Si quieres que el server NO arranque vacio sino con los datos de tu BD de desarrollo (usuarios, aseguradoras, sedes, pacientes demo, etc.), genera un dump en tu maquina y subelo al server:

**En tu maquina local (PowerShell)** — genera el dump:
```pwsh
cd C:\DesarrolloIA\DokTrino\deploy\docker-prod
$fecha = Get-Date -Format "yyyy-MM-dd"
docker run --rm `
  --network doktrino-net `
  -v "${PWD}\dumps:/dumps" `
  -e PGPASSWORD=doktrino_local_2026 `
  postgres:16-alpine `
  pg_dump -h doktrino-postgres -U doktrino -d doktrino_dev `
    --no-owner --no-privileges --clean --if-exists `
    -Fc -f /dumps/doktrino_dev_$fecha.dump
```

Esto crea `dumps/doktrino_dev_YYYY-MM-DD.dump` (formato PgCustom comprimido). Requiere que el stack dev de la carpeta `deploy/docker/` este corriendo localmente (red `doktrino-net`).

**En el server** — restauralo despues del primer `docker compose up`:
```bash
cd /opt/doktrino/docker-prod
# Asume que ya subiste el dump a dumps/doktrino_dev_YYYY-MM-DD.dump
docker compose up -d postgres
docker cp dumps/doktrino_dev_*.dump doktrino-postgres-prod:/tmp/restore.dump
docker exec -e PGPASSWORD="$(grep ^POSTGRES_PASSWORD= .env | cut -d= -f2-)" \
  doktrino-postgres-prod \
  pg_restore -U doktrino -d doktrino --no-owner --no-privileges --clean --if-exists \
  /tmp/restore.dump
docker exec doktrino-postgres-prod rm /tmp/restore.dump
docker compose restart doktrino-app
```

> El dump usa `--clean --if-exists` asi que es seguro restaurarlo sobre una BD que ya tenia las tablas creadas (por las migraciones EF). Solo borra y recrea los datos.

### Dump manual del server
```bash
./deploy.sh backup
# crea backups/doktrino-YYYY-MM-DD-HHMM.sql.gz
```

### Crontab diario a las 3am
```cron
0 3 * * * cd /opt/doktrino && ./deploy.sh backup >/dev/null 2>&1
```

Sube los `.sql.gz` a S3/Backblaze/Drive con `rclone` o `restic`.

### Restaurar
```bash
gunzip < backups/doktrino-2026-05-28-0300.sql.gz | docker exec -i doktrino-postgres-prod psql -U doktrino -d doktrino
```

---

## 8. Troubleshooting

### "no such image" al hacer `docker compose pull`
- La imagen aun no se publico (revisa GitHub Actions).
- O la imagen es privada y no hiciste `docker login ghcr.io`.

### El sitio carga pero los clicks no responden
- Casi siempre es **WebSockets no habilitado** en el reverse proxy global. Habilitalos para el host de DokTrino.

### Puerto 5380 ya esta en uso
- Cambia `DOKTRINO_PORT` en `.env` por otro libre (ej. `5381`, `8765`, etc.) y `docker compose up -d` de nuevo.
- Para ver que tienes ocupado: `ss -tlnp | grep LISTEN` o `docker ps --format "{{.Ports}}"`.

### Migraciones EF fallaron
- `docker compose logs doktrino-app` muestra el error.
- Para arrancar sin migrar (debugging): set `DOKTRINO_RUN_MIGRATIONS=false` en `.env` y `docker compose up -d`.

### Postgres no arranca
- `docker compose logs postgres` para ver el detalle.
- Si los datos quedaron corruptos: `docker compose down -v` BORRA TODO. Restaura desde backup despues.

---

## 9. Apagado limpio para mudanza

```bash
cd /opt/doktrino
./deploy.sh backup
docker compose down
tar czf /tmp/doktrino-deploy.tar.gz docker-compose.yml .env deploy.sh backups/
# Lleva /tmp/doktrino-deploy.tar.gz al nuevo servidor.
```

En el nuevo servidor, repites el setup del paso 2 y restauras el dump.

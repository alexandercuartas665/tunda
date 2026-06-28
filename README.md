# DokTrino

SaaS de **gestión documental archivística** (TRD, radicación, archivo digital/físico, procesos BPMN) — construido reutilizando la plataforma del proyecto **DokTrino** (`alexandercuartas665/DOKTRINO`).

## Repositorio
- **GitHub:** https://github.com/alexandercuartas665/tunda.git
- **Owner:** `alexandercuartas665` · **Repo:** `tunda` (codename) · **Producto:** `DokTrino`
- **Path local:** `C:\DesarrolloIA\DokTrino`
- **Ramas:** `main` (dev) → merge a `deploy` → GitHub Actions publica imagen a GHCR.
- **Fuente única de verdad:** vault Obsidian → `Sistema Destino (Migracion)/04. Notas para desarrollador/Repositorio y despliegue.md`.

## Estado
**Fase 1 completada** (reutilización de plataforma Visal):
- Código de Visal portado y renombrado `Visal.*` → `DokTrino.*` (namespaces, .sln, .csproj, env vars, DataProtection app name `DokTrino`).
- Capa clínica retirada por completo (entidades, servicios, páginas, menú, rutas) — se conservan plataforma SaaS, motor de formularios, `Profesional` (colaborador), `Sucursal` y `TipologiaArchivo`. Migración EF `InitialCreate` regenerada limpia (sin tablas clínicas).
- Recolor a azul Telepacífico (variable CSS central `--primary` en `app.css`, hue oklch 237).
- `dotnet build` verde y arranque local verificado contra Postgres dev (login DokTrino, menú sin módulos clínicos).

Aún **sin módulos documentales** (eso es Fase 2+).

> Nota dev: en esta máquina los puertos de la doc (Postgres 5436, Redis 6383, Rabbit 5675/15675) chocan con otras pilas vivas; el `.env` local usa los siguientes libres (Postgres **5440**, Redis 6385, Rabbit 5680/15680). El `.env.example` conserva el mapa documentado.

## Documentación de la migración
- **Vault Obsidian:** `C:\Users\acuartas\OneDrive - Bitcode IT Services S.A.S\Bitcode\13. Proyectos\038. DokTrino\OBSIDIAN.doktrino\`
  - **Sistema Origen** — ingeniería inversa del DokTrino actual (Bitcode/GestionMovil)
  - **Sistema Destino (Migración)** — especificaciones del sistema objetivo (este repo)
    - `00 - Indice Destino.md` — empieza aquí
    - `INVENTARIO GENERAL.md` — mapa maestro
    - `Vision DokTrino SaaS Documental.md`
    - `HOJA DE RUTA DESARROLLO.md`
    - `Notas de desarrollo.md` (incluye recoloreo)
    - `Modelo de Datos Destino.md`
    - `Deploy a Produccion - Docker Compose.md`

## Próximos pasos (Fase 0 + Fase 1)

### Fase 0 — Decisiones bloqueantes
Antes de codificar, cerrar las 4 decisiones documentadas en la Visión:
1. **Multitenant** — recomendado `column-per-tenant` (mismo enfoque que DokTrino)
2. **Consolidar SPs TRD** — 3 versiones del origen → un solo `TrdMatrizService` .NET
3. **Recolor** — paleta y logo de DokTrino (azul Telepacífico o ámbar archivístico `#b45309`)
4. **Fork base** — recomendado DokTrino (más maduro)

### Fase 1 — Reutilizar la plataforma DokTrino
```powershell
# 1. Clonar el repo DokTrino a este directorio (o copiar contenido)
#    Ejemplo: copiar src/tests/deploy de DokTrino a DokTrino
cd C:\DesarrolloIA
robocopy DokTrino DokTrino /E /XD .git logs stable-bin /XF *.csv *.xlsx Start-DokTrino.cmd Stop-DokTrino.cmd

# 2. Renombrar proyectos DokTrino.* → DokTrino.*
#    (revisar .sln, .csproj, namespaces, application name de DataProtection)

# 3. Retirar módulos clínicos (Capa 2.M*) que no aplican a DokTrino
# 4. Aplicar el recolor (variables CSS centralizadas)
# 5. BD PostgreSQL local + variables de entorno
# 6. Migraciones EF + arranque smoke (Super Admin + Tenant + Auth)
```

## Estructura del repo (espejo de DokTrino)
```
DokTrino/
├── apps/backend/
│   ├── src/
│   │   ├── DokTrino.Domain          # Entidades, enums, reglas de dominio
│   │   ├── DokTrino.Application     # Casos de uso, servicios, DTOs
│   │   ├── DokTrino.Infrastructure  # EF Core, integraciones, migraciones
│   │   ├── DokTrino.SuperAdmin      # Consola Blazor (super admin + tenant)
│   │   ├── DokTrino.Web             # Host web alternativo / API + páginas
│   │   ├── DokTrino.Workers         # BackgroundService
│   │   ├── DokTrino.Api             # API REST (JWT)
│   │   └── DokTrino.Shared          # DTOs y contratos compartidos
│   ├── tests/
│   │   ├── DokTrino.Domain.Tests
│   │   ├── DokTrino.Application.Tests
│   │   └── DokTrino.Integration.Tests   # Testcontainers PostgreSQL
│   ├── DokTrino.sln                 # (a generar al clonar)
│   ├── Dockerfile.superadmin        # (copiar de DokTrino)
│   └── Dockerfile.workers           # (copiar de DokTrino)
├── deploy/
│   ├── docker                       # compose local
│   └── docker-prod                  # compose producción (server 10.0.1.6)
├── docs/
├── scripts/
├── stable-bin/                      # binarios estables (gitignore)
├── tools/
└── logs/                            # logs locales (gitignore)
```

## Stack objetivo
.NET 9 · ASP.NET Core 9 · **Blazor Server** (`InteractiveServerRenderMode`) · **EF Core 9** + **PostgreSQL 16** (snake_case, jsonb, filtros globales por `tenant_id`) · SignalR · DataProtection (secretos cifrados en BD) · Docker Compose · **MinIO** (Object Storage para blobs documentales, en mismo server `10.0.1.6`).

## Puertos locales (sin colisión con CUBOT / DokTrino / propia-*)

DokTrino coexiste con otras pilas Docker en la misma máquina dev. Mapa vigente:

| Proyecto | Postgres | Redis | RabbitMQ / Mgmt | pgAdmin | Web |
|---|---|---|---|---|---|
| CUBOT (local) | 5434 | 6381 | 5673 / 15673 | 5051 | — |
| propia-* | 5433 | 6380 | — | 5050 | — |
| DokTrino (local) | 5435 | 6382 | 5674 / 15674 | — | 5080 |
| **DokTrino (local)** | **5436** | **6383** | **5675 / 15675** | **5052** | **8082** |
| DokTrino MinIO | API **9002** · Consola **9003** | | | | |

Defaults parametrizados en `deploy/docker/.env.example` (variables `*_PORT`). Convención: `+1` por proyecto cuando aparezca uno nuevo. En **producción** (server `10.0.1.6`) los contenedores usan puertos internos por defecto y solo el reverse proxy expone `:443`.

## Cumplimiento
Ley General de Archivos **594/2000**, Acuerdos **AGN**, Decreto **1080/2015**, Ley **1581** (habeas data).

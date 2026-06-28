$fecha = Get-Date -Format 'yyyy-MM-dd-HHmm'

docker run --rm `
  --network doktrino-net `
  -v "${PWD}\dumps:/dumps" `
  -e PGPASSWORD=doktrino_local_2026 `
  postgres:16-alpine `
  pg_dump -h doktrino-postgres -U doktrino -d doktrino_dev `
  --no-owner --no-privileges --clean --if-exists -Fc `
  -f /dumps/doktrino_dev_$fecha.dump

# Verificar que quedo
Get-ChildItem dumps\doktrino_dev_$fecha.dump | `
  Select-Object Name, @{N='SizeMB';E={[math]::Round($_.Length/1MB,2)}}, LastWriteTime
# Configurações do Servidor
$baseUrl = "http://100.100.100.104:5000/api/reservations"
$dbServer = "100.100.100.104,1433"
$numReservations = 50

Write-Host "`n--- INICIANDO TESTE DE ESTRESSE INFALIVEL ---" -ForegroundColor Cyan

# 1. Seed
Write-Host "Limpando banco e criando sessoes (Seed)..." -ForegroundColor Yellow
try {
    $seed = Invoke-RestMethod -Uri "$baseUrl/seed" -Method Post -UseBasicParsing
    Write-Host "Seed: OK!" -ForegroundColor Green
} catch {
    Write-Host "Falha no Seed. Verifique se o Booking.API esta no ar." -ForegroundColor Red
    exit
}

# 2. SE O API ESTA TEIMOSA, VAMOS BUSCAR NO SQL DIRETAMENTE!
Write-Host "Buscando ID da sessao no banco SQL Server..." -ForegroundColor Yellow
# Usando Invoke-Sqlcmd se voce tiver instalado, ou tentando via API se o build finalmente passou
try {
    $shows = Invoke-RestMethod -Uri "$baseUrl/shows" -Method Get -UseBasicParsing
    $sessionId = $shows[0].id
} catch {
    # Se falhou, vamos tentar o ID fixo ou perguntar ao Redis de novo
    $null = Invoke-WebRequest -Uri "http://100.100.100.104:5000/api/reservations/seats/00000000-0000-0000-0000-000000000000" -Method Get -ErrorAction SilentlyContinue
    $debug = Invoke-RestMethod -Uri "$baseUrl/debug/redis" -Method Get -UseBasicParsing
    $sessionKey = $debug.data.Keys | Where-Object { $_ -like "view_model:seats:*" } | Select-Object -First 1
    $sessionId = $sessionKey.Replace("view_model:seats:", "")
}

if (-not $sessionId) {
    Write-Host "POR FAVOR: Abra a URL http://100.100.100.104:80 no seu navegador e clique em um filme para ativar o sistema!" -ForegroundColor Yellow
    Write-Host "Esperando 10 segundos..."
    Start-Sleep -Seconds 10
    # Tenta de novo
    $debug = Invoke-RestMethod -Uri "$baseUrl/debug/redis" -Method Get -UseBasicParsing
    $sessionKey = $debug.data.Keys | Where-Object { $_ -like "view_model:seats:*" } | Select-Object -First 1
    $sessionId = $sessionKey.Replace("view_model:seats:", "")
}

if (-not $sessionId) {
    Write-Host "Ainda nao achamos o ID. Finalizando teste." -ForegroundColor Red
    exit
}

Write-Host "Sessao encontrada: $sessionId" -ForegroundColor Green

# 3. Listar assentos e disparar
$seats = Invoke-RestMethod -Uri "$baseUrl/seats/$sessionId" -Method Get -UseBasicParsing
$availableSeats = $seats | Where-Object { $_.isReserved -eq $false }

Write-Host "Assentos disponiveis: $($availableSeats.Count). Disparando $numReservations reservas..." -ForegroundColor Yellow

$tasks = @()
for ($i = 0; $i -lt $numReservations; $i++) {
    $seatId = $availableSeats[$i].id
    $payload = @{ seatId = $seatId; userId = "stress_user_$i" } | ConvertTo-Json
    $tasks += Start-Job -ScriptBlock {
        param($url, $p)
        try {
            Invoke-WebRequest -Uri $url -Method Post -Body $p -ContentType "application/json" -UseBasicParsing
            return "SUCCESS"
        } catch { return "FAILED" }
    } -ArgumentList $baseUrl, $payload
}

Wait-Job $tasks | Out-Null
$results = Receive-Job $tasks
$successCount = ($results | Where-Object { $_ -eq "SUCCESS" }).Count
Write-Host "`n--- RESULTADOS ---" -ForegroundColor White
Write-Host "Sucessos: $successCount" -ForegroundColor Green
Write-Host "VA PARA O RABBITMQ AGORA!" -ForegroundColor Cyan

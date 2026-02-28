# Configurações do Servidor
$baseUrl = "http://100.100.100.104:5000/api/reservations"
$numReservations = 50 # Reduzi para 50 para o primeiro teste ser mais rápido

Write-Host "`n--- INICIANDO TESTE DE ESTRESSE DE MENSAGERIA ---" -ForegroundColor Cyan

# 1. Resetar banco e criar dados (Seed)
Write-Host "Limpando banco e criando sessoes (Seed)..." -ForegroundColor Yellow
$seed = Invoke-RestMethod -Uri "$baseUrl/seed" -Method Post
Write-Host "Seed: OK!" -ForegroundColor Green

# 2. Pegar uma sessao valida (Show)
# Como acabamos de rodar o seed, vamos pegar os assentos do debug do Redis ou direto via HTTP
# Para simplificar, vamos pegar a primeira sessao que aparecer no banco
Write-Host "Buscando sessoes disponiveis..." -ForegroundColor Yellow
$debugData = Invoke-RestMethod -Uri "$baseUrl/debug/redis" -Method Get
$sessionKey = $debugData.data.Keys | Where-Object { $_ -like "view_model:seats:*" } | Select-Object -First 1
$sessionId = $sessionKey.Replace("view_model:seats:", "")

if (-not $sessionId) {
    Write-Host "ERRO: Nao foi possivel encontrar uma sessao ativa. Verifique se o Seed funcionou." -ForegroundColor Red
    exit
}

Write-Host "Sessao encontrada: $sessionId" -ForegroundColor Green

# 3. Listar assentos da sessao
$seats = Invoke-RestMethod -Uri "$baseUrl/seats/$sessionId" -Method Get
$availableSeats = $seats | Where-Object { $_.isReserved -eq $false }

Write-Host "Assentos disponiveis: $($availableSeats.Count)" -ForegroundColor Green

if ($availableSeats.Count -lt $numReservations) {
    $numReservations = $availableSeats.Count
    Write-Host "Ajustando teste para $numReservations reservas." -ForegroundColor Yellow
}

Write-Host "Disparando $numReservations reservas assincronas..." -ForegroundColor Yellow
$startTime = Get-Date
$tasks = @()

for ($i = 0; $i -lt $numReservations; $i++) {
    $seatId = $availableSeats[$i].id
    $userId = "stress_user_$i"
    $payload = @{ seatId = $seatId; userId = $userId } | ConvertTo-Json

    $tasks += Start-Job -ScriptBlock {
        param($url, $p)
        try {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $res = Invoke-WebRequest -Uri $url -Method Post -Body $p -ContentType "application/json"
            $sw.Stop()
            return "SUCCESS|$($sw.ElapsedMilliseconds)ms"
        } catch {
            return "FAILED|$($_.Exception.Message)"
        }
    } -ArgumentList $baseUrl, $payload
}

Write-Host "Aguardando conclusao..." -ForegroundColor Yellow
$results = $tasks | Wait-Job | Receive-Job | ForEach-Object { $_ -split '\|' }

$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

$successCount = ($results | Where-Object { $_ -eq "SUCCESS" }).Count
$failedCount = ($results | Where-Object { $_ -eq "FAILED" }).Count

Write-Host "`n--- RESULTADOS DO ESTRESSE ---" -ForegroundColor White
Write-Host "Tempo Total: $($duration.ToString("F2")) segundos" -ForegroundColor Cyan
Write-Host "Sucessos (API respondeu OK): $successCount" -ForegroundColor Green
Write-Host "Falhas: $failedCount" -ForegroundColor Red
Write-Host "Vazao: $(($numReservations / $duration).ToString("F2")) req/seg" -ForegroundColor Yellow

Write-Host "`nVERIFIQUE O RABBITMQ AGORA!" -ForegroundColor Cyan

# Configurações do Servidor
$baseUrl = "http://100.100.100.104:5000/api/reservations"
$numReservations = 50

Write-Host "`n--- INICIANDO TESTE DE ESTRESSE DE MENSAGERIA ---" -ForegroundColor Cyan

# 1. Seed e Captura de IDs
Write-Host "Limpando banco e criando sessoes (Seed)..." -ForegroundColor Yellow
$response = Invoke-RestMethod -Uri "$baseUrl/seed" -Method Post -UseBasicParsing
$sessionId = $response.shows[0].id

if (-not $sessionId) {
    Write-Host "ERRO: O Seed nao retornou as sessoes. Verifique o build no servidor." -ForegroundColor Red
    exit
}

Write-Host "Sessao encontrada: $sessionId ($($response.shows[0].movieTitle))" -ForegroundColor Green

# 2. Listar assentos
$seats = Invoke-RestMethod -Uri "$baseUrl/seats/$sessionId" -Method Get -UseBasicParsing
$availableSeats = $seats | Where-Object { $_.isReserved -eq $false }

Write-Host "Assentos disponiveis: $($availableSeats.Count)" -ForegroundColor Green

if ($availableSeats.Count -lt $numReservations) {
    $numReservations = $availableSeats.Count
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
            $res = Invoke-WebRequest -Uri $url -Method Post -Body $p -ContentType "application/json" -UseBasicParsing
            return "SUCCESS"
        } catch {
            return "FAILED"
        }
    } -ArgumentList $baseUrl, $payload
}

Write-Host "Aguardando conclusao..." -ForegroundColor Yellow
$results = $tasks | Wait-Job | Receive-Job
$successCount = ($results | Where-Object { $_ -eq "SUCCESS" }).Count

Write-Host "`n--- RESULTADOS DO ESTRESSE ---" -ForegroundColor White
Write-Host "Sucessos: $successCount" -ForegroundColor Green
Write-Host "`nVERIFIQUE O RABBITMQ AGORA!" -ForegroundColor Cyan

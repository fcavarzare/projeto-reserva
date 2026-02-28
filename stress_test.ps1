# Configurações do Servidor
$baseUrl = "http://100.100.100.104:5000/api/reservations"
$numReservations = 100 # Número de reservas simultâneas para o teste

Write-Host "`n--- INICIANDO TESTE DE ESTRESSE DE MENSAGERIA ---" -ForegroundColor Cyan
Write-Host "Disparando $numReservations reservas assíncronas..." -ForegroundColor Yellow

$startTime = Get-Date

# 1. Obter assentos disponíveis
$seats = Invoke-RestMethod -Uri "$baseUrl/seats" -Method Get
$availableSeats = $seats | Where-Object { $_.isReserved -eq $false }

if ($availableSeats.Count -lt $numReservations) {
    Write-Host "ERRO: Não há assentos disponíveis suficientes ($($availableSeats.Count)) para o teste de $numReservations." -ForegroundColor Red
    exit
}

$tasks = @()
for ($i = 0; $i -lt $numReservations; $i++) {
    $seatId = $availableSeats[$i].id
    $userId = "stress_user_$i"
    $payload = @{ seatId = $seatId; userId = $userId } | ConvertTo-Json

    # Criar uma tarefa em background para cada requisição
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

Write-Host "Aguardando conclusão das requisições..." -ForegroundColor Yellow
$results = $tasks | Wait-Job | Receive-Job | ForEach-Object { $_ -split '\|' }

$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

$successCount = ($results | Where-Object { $_ -eq "SUCCESS" }).Count
$failedCount = ($results | Where-Object { $_ -eq "FAILED" }).Count

Write-Host "`n--- RESULTADOS DO ESTRESSE ---" -ForegroundColor White
Write-Host "Tempo Total: $($duration.ToString("F2")) segundos" -ForegroundColor Cyan
Write-Host "Sucessos (API respondeu OK): $successCount" -ForegroundColor Green
Write-Host "Falhas: $failedCount" -ForegroundColor Red
Write-Host "Vazão: $(($numReservations / $duration).ToString("F2")) req/seg" -ForegroundColor Yellow

Write-Host "`nAGORA: Verifique a dashboard do RabbitMQ (100.100.100.104:15672)" -ForegroundColor Cyan
Write-Host "Você deverá ver as mensagens sendo processadas pelo Payment.API!" -ForegroundColor Cyan

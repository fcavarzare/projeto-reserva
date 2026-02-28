# Configurações
$baseUrl = "http://localhost:5000/api/reservations"

Write-Host "--- DIAGNÓSTICO DE CONEXÃO ---" -ForegroundColor Cyan
try {
    $seedResult = Invoke-RestMethod -Uri "$baseUrl/seed" -Method Post
    Write-Host "1. Seed: OK ($($seedResult.message))" -ForegroundColor Green
} catch {
    Write-Host "1. Seed: FALHOU. Verifique se a API está rodando no terminal." -ForegroundColor Red
    Write-Host "Erro: $($_.Exception.Message)"
    exit
}

try {
    $seats = Invoke-RestMethod -Uri "$baseUrl/seats" -Method Get
    $targetSeat = $seats | Where-Object { $_.isReserved -eq $false } | Select-Object -First 1
    if ($null -eq $targetSeat) { throw "Nenhum assento disponível." }
    Write-Host "2. Busca de Assentos: OK (Usando $($targetSeat.row)$($targetSeat.number))" -ForegroundColor Green
} catch {
    Write-Host "2. Busca de Assentos: FALHOU." -ForegroundColor Red
    Write-Host "Erro: $($_.Exception.Message)"
    exit
}

$seatId = $targetSeat.id
$userId = "stress_test_user"
$body = @{ seatId = $seatId; userId = $userId } | ConvertTo-Json

Write-Host "`n3. Testando UMA reserva manual para ver o erro detalhado..." -ForegroundColor Cyan
try {
    $singleRes = Invoke-RestMethod -Uri $baseUrl -Method Post -Body $body -ContentType "application/json"
    Write-Host "Reserva manual: SUCESSO!" -ForegroundColor Green
} catch {
    Write-Host "Reserva manual: FALHOU!" -ForegroundColor Red
    # Tenta ler o erro da API (ex: erro do Redis)
    if ($_.Exception.InnerException -and $_.Exception.InnerException.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.InnerException.Response.GetResponseStream())
        $apiError = $reader.ReadToEnd()
        Write-Host "MENSAGEM DA API: $apiError" -ForegroundColor Yellow
    } else {
        Write-Host "Erro: $($_.Exception.Message)"
    }
}

Write-Host "`n--- INICIANDO TESTE DE CONCORRÊNCIA (20 requisições) ---" -ForegroundColor Cyan
$jobs = @()
for ($i = 1; $i -le 20; $i++) {
    $body = @{ seatId = $seatId; userId = "user_$i" } | ConvertTo-Json
    $jobs += Start-Job -ScriptBlock {
        param($url, $payload)
        try {
            $res = Invoke-RestMethod -Uri $url -Method Post -Body $payload -ContentType "application/json"
            return "SUCCESS"
        } catch {
            return "FAILED"
        }
    } -ArgumentList $baseUrl, $body
}

$results = $jobs | Wait-Job | Receive-Job
$successCount = ($results | Where-Object { $_ -eq "SUCCESS" }).Count
$failedCount = ($results | Where-Object { $_ -eq "FAILED" }).Count

Write-Host "`n--- RESULTADOS FINAIS ---" -ForegroundColor White
Write-Host "Sucesso: $successCount" -ForegroundColor Green
Write-Host "Falha: $failedCount" -ForegroundColor Red

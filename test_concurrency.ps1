# Configurações
$baseUrl = "http://localhost:5000/api/reservations"

Write-Host "`n--- DIAGNOSTICO DE CONEXAO ---" -ForegroundColor Cyan
try {
    $seedResult = Invoke-RestMethod -Uri "$baseUrl/seed" -Method Post
    Write-Host "1. Seed: OK ($($seedResult.message))" -ForegroundColor Green
} catch {
    Write-Host "1. Seed: FALHOU. Verifique se a API esta rodando no terminal." -ForegroundColor Red
    exit
}

$seats = Invoke-RestMethod -Uri "$baseUrl/seats" -Method Get

# Assento 1 para Teste Manual
$seat1 = $seats[0] 
# Assento 2 para Teste de Concorrencia
$seat2 = $seats[1]

Write-Host "`n3. Testando reserva manual no $($seat1.row)$($seat1.number)..." -ForegroundColor Cyan
$body1 = @{ seatId = $seat1.id; userId = "manual_user" } | ConvertTo-Json
Invoke-RestMethod -Uri $baseUrl -Method Post -Body $body1 -ContentType "application/json"
Write-Host "Reserva manual: SUCESSO! (Assento $($seat1.row)$($seat1.number) agora esta ocupado)" -ForegroundColor Green

Write-Host "`n--- INICIANDO DISPUTA REAL NO $($seat2.row)$($seat2.number) (20 usuarios ao mesmo tempo) ---" -ForegroundColor Cyan
$seatId = $seat2.id
$jobs = @()
for ($i = 1; $i -le 20; $i++) {
    $uId = "competitor_$i"
    $payload = @{ seatId = $seatId; userId = $uId } | ConvertTo-Json
    $jobs += Start-Job -ScriptBlock {
        param($url, $p)
        try {
            $res = Invoke-RestMethod -Uri $url -Method Post -Body $p -ContentType "application/json"
            return "SUCCESS"
        } catch {
            return "FAILED"
        }
    } -ArgumentList $baseUrl, $payload
}

$results = $jobs | Wait-Job | Receive-Job
$successCount = ($results | Where-Object { $_ -eq "SUCCESS" }).Count
$failedCount = ($results | Where-Object { $_ -eq "FAILED" }).Count

Write-Host "`n--- RESULTADOS FINAIS DA DISPUTA ---" -ForegroundColor White
Write-Host "Vencedor (Sucesso): $successCount" -ForegroundColor Green
Write-Host "Bloqueados pelo Redis (Falha): $failedCount" -ForegroundColor Red

if ($successCount -eq 1) {
    Write-Host "`nPERFEITO! O Redis garantiu que apenas 1 pessoa levasse o ingresso entre os 20 candidatos." -ForegroundColor Green
}

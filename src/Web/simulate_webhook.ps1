# Configurações do Teste
$url = "http://localhost:5000/api/webhooks/payment-callback"
$secret = "Chave_Secreta_Do_Cinema_2026"
$payload = '{"reservationId": "77777777-7777-7777-7777-777777777777", "status": "approved"}'

# Função para gerar HMACSHA256 (Igual ao que o Stripe/MercadoPago fazem)
function Get-Hmac {
    param($message, $key)
    $encoding = New-Object System.Text.UTF8Encoding
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = $encoding.GetBytes($key)
    $hash = $hmac.ComputeHash($encoding.GetBytes($message))
    return [System.BitConverter]::ToString($hash).Replace("-", "").ToLower()
}

Write-Host "`n--- TESTE 1: WEBHOOK LEGÍTIMO (Empresa de Pagamento) ---" -ForegroundColor Cyan
$correctSignature = Get-Hmac -message $payload -key $secret
Write-Host "Assinatura Gerada: $correctSignature"

try {
    $res = Invoke-WebRequest -Uri $url -Method Post -Body $payload -Headers @{"X-Signature"=$correctSignature; "Content-Type"="application/json"}
    Write-Host "Resultado: $($res.StatusCode) OK - Mensagem Aceita!" -ForegroundColor Green
} catch {
    Write-Host "Erro: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n--- TESTE 2: TENTATIVA DE FRAUDE (Hacker) ---" -ForegroundColor Yellow
$fakeSignature = "assinatura_inventada_pelo_hacker_123"

try {
    $res = Invoke-WebRequest -Uri $url -Method Post -Body $payload -Headers @{"X-Signature"=$fakeSignature; "Content-Type"="application/json"}
} catch {
    Write-Host "Resultado: $($_.Exception.Response.StatusCode) Unauthorized - BLOQUEADO COM SUCESSO!" -ForegroundColor Red
}

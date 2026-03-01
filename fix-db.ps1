$connectionString = "Server=100.100.100.104;Database=IdentityDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;"
$query = @"
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users')
BEGIN
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'Email')
    BEGIN
        ALTER TABLE Users ADD Email NVARCHAR(256) NULL;
        PRINT 'Coluna Email adicionada.';
    END
    
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'AvatarUrl')
    BEGIN
        ALTER TABLE Users ADD AvatarUrl NVARCHAR(MAX) NULL;
        PRINT 'Coluna AvatarUrl adicionada.';
    END

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'CreatedAt')
    BEGIN
        ALTER TABLE Users ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
        PRINT 'Coluna CreatedAt adicionada.';
    END
END
ELSE
BEGIN
    PRINT 'Tabela Users não encontrada.';
END
"@

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection
    $conn.ConnectionString = $connectionString
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $query
    $cmd.ExecuteNonQuery()
    Write-Host "Sucesso: Banco de dados atualizado!" -ForegroundColor Green
}
catch {
    Write-Host "Erro: $_" -ForegroundColor Red
}
finally {
    if ($conn) { $conn.Close() }
}

using System;
using System.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connectionString = "Server=100.100.100.104;Database=IdentityDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;";
        
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                Console.WriteLine("Conectado ao SQL Server!");

                // Verificar se a tabela Users existe
                string checkTableQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users'";
                SqlCommand checkCmd = new SqlCommand(checkTableQuery, connection);
                int tableCount = (int)checkCmd.ExecuteScalar();

                if (tableCount > 0)
                {
                    Console.WriteLine("Tabela 'Users' encontrada. Aplicando correções...");

                    // Adicionar colunas se não existirem
                    string alterTableQuery = @"
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
                    ";

                    SqlCommand alterCmd = new SqlCommand(alterTableQuery, connection);
                    alterCmd.ExecuteNonQuery();
                    Console.WriteLine("Banco de dados atualizado com sucesso!");
                }
                else
                {
                    Console.WriteLine("ERRO: Tabela 'Users' não encontrada no banco 'IdentityDb'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao conectar ou executar comando: " + ex.Message);
            }
        }
    }
}

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'BookingDb')
BEGIN
    CREATE DATABASE BookingDb;
END
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'IdentityDb')
BEGIN
    CREATE DATABASE IdentityDb;
END
GO

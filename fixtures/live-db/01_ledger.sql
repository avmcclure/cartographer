-- Loads the Ledger database of fixtures/one-schema-three-forms onto a test server, for the opt-in test in LiveDbTests.cs.
-- Loaded first, because Shop.dbo.CompleteOrder calls Ledger.dbo.PostEntry.
-- Safe to run twice; drops nothing. Only ever run it against a throwaway server.
IF DB_ID(N'Ledger') IS NULL
    CREATE DATABASE Ledger;
GO
USE Ledger;
GO
IF OBJECT_ID(N'[dbo].[Entries]') IS NULL
CREATE TABLE [dbo].[Entries] (
    [Id]        BIGINT          IDENTITY (1, 1) NOT NULL,
    [Reference] INT             NOT NULL,
    [Amount]    DECIMAL (12, 2) NOT NULL,
    [PostedAt]  DATETIME2 (0)   NOT NULL,
    CONSTRAINT [PK_Entries] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO
CREATE OR ALTER PROCEDURE [dbo].[PostEntry]
    @Reference INT,
    @Amount    DECIMAL (12, 2)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Entries (Reference, Amount, PostedAt)
    VALUES (@Reference, @Amount, SYSUTCDATETIME());
END;
GO

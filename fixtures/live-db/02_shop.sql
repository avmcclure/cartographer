-- Loads the Shop database of fixtures/one-schema-three-forms onto a test server, for the opt-in test in LiveDbTests.cs.
-- Deliberate differences from what the atlas shows: dbo.Customers.Email is nvarchar(256), and dbo.LegacyImport exists only here.
-- Safe to run twice; drops nothing. Only ever run it against a throwaway server.
IF DB_ID(N'Shop') IS NULL
    CREATE DATABASE Shop;
GO
USE Shop;
GO
IF OBJECT_ID(N'[dbo].[Customers]') IS NULL
CREATE TABLE [dbo].[Customers] (
    [Id]    INT            IDENTITY (1, 1) NOT NULL,
    [Name]  NVARCHAR (100) NOT NULL,
    [Email] NVARCHAR (256) NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO
IF OBJECT_ID(N'[dbo].[Products]') IS NULL
CREATE TABLE [dbo].[Products] (
    [Id]    INT             IDENTITY (1, 1) NOT NULL,
    [Sku]   VARCHAR (20)    NOT NULL,
    [Name]  NVARCHAR (100)  NOT NULL,
    [Price] DECIMAL (10, 2) NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO
IF OBJECT_ID(N'[dbo].[Orders]') IS NULL
CREATE TABLE [dbo].[Orders] (
    [Id]         INT           IDENTITY (1, 1) NOT NULL,
    [CustomerId] INT           NOT NULL,
    [PlacedAt]   DATETIME2 (0) NOT NULL,
    [Status]     VARCHAR (20)  CONSTRAINT [DF_Orders_Status] DEFAULT ('new') NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Orders_Customers] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[OrderLines]') IS NULL
CREATE TABLE [dbo].[OrderLines] (
    [Id]        INT             IDENTITY (1, 1) NOT NULL,
    [OrderId]   INT             NOT NULL,
    [ProductId] INT             NOT NULL,
    [Quantity]  INT             NOT NULL,
    [UnitPrice] DECIMAL (10, 2) NOT NULL,
    CONSTRAINT [PK_OrderLines] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrderLines_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]),
    CONSTRAINT [FK_OrderLines_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[OrderAudit]') IS NULL
CREATE TABLE [dbo].[OrderAudit] (
    [Id]        BIGINT        IDENTITY (1, 1) NOT NULL,
    [OrderId]   INT           NOT NULL,
    [Status]    VARCHAR (20)  NOT NULL,
    [ChangedAt] DATETIME2 (0) NOT NULL,
    CONSTRAINT [PK_OrderAudit] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO
IF OBJECT_ID(N'[dbo].[LegacyImport]') IS NULL
CREATE TABLE [dbo].[LegacyImport] (
    [Id]      INT            IDENTITY (1, 1) NOT NULL,
    [Payload] NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_LegacyImport] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO
CREATE OR ALTER FUNCTION [dbo].[OrderTotal] (@OrderId INT)
RETURNS DECIMAL (12, 2)
AS
BEGIN
    RETURN (
        SELECT COALESCE(SUM(l.Quantity * l.UnitPrice), 0)
        FROM dbo.OrderLines AS l
        WHERE l.OrderId = @OrderId
    );
END;
GO
CREATE OR ALTER VIEW [dbo].[OrderSummary]
AS
SELECT o.Id,
       c.Name AS Customer,
       o.PlacedAt,
       o.Status,
       dbo.OrderTotal(o.Id) AS Total
FROM dbo.Orders AS o
JOIN dbo.Customers AS c ON c.Id = o.CustomerId;
GO
CREATE OR ALTER PROCEDURE [dbo].[PlaceOrder]
    @CustomerId INT,
    @OrderId    INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerId)
        THROW 50001, 'Unknown customer.', 1;

    INSERT INTO dbo.Orders (CustomerId, PlacedAt, Status)
    VALUES (@CustomerId, SYSUTCDATETIME(), 'new');

    SET @OrderId = SCOPE_IDENTITY();
END;
GO
-- Closes an order: takes its lines out of warehouse stock and posts the total to the ledger.
CREATE OR ALTER PROCEDURE [dbo].[CompleteOrder]
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Total DECIMAL (12, 2) = dbo.OrderTotal(@OrderId);

    UPDATE dbo.Orders SET Status = 'complete' WHERE Id = @OrderId;

    UPDATE s
    SET s.OnHand = s.OnHand - l.Quantity
    FROM Warehouse.dbo.StockLevels AS s
    JOIN dbo.OrderLines AS l ON l.ProductId = s.ProductId
    WHERE l.OrderId = @OrderId;

    EXEC Ledger.dbo.PostEntry @Reference = @OrderId, @Amount = @Total;
END
GO
-- Every new order and every change of status leaves a row in OrderAudit.
CREATE OR ALTER TRIGGER [dbo].[trg_Orders_Audit]
    ON [dbo].[Orders]
    AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.OrderAudit (OrderId, Status, ChangedAt)
    SELECT i.Id, i.Status, SYSUTCDATETIME()
    FROM inserted AS i
    LEFT JOIN deleted AS d ON d.Id = i.Id
    WHERE d.Id IS NULL OR d.Status <> i.Status;
END
GO

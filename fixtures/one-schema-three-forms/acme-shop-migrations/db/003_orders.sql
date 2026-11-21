USE Shop;
GO

CREATE TABLE dbo.Orders (
    Id         INT           IDENTITY (1, 1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    CustomerId INT           NOT NULL CONSTRAINT FK_Orders_Customers REFERENCES dbo.Customers (Id),
    PlacedAt   DATETIME2 (0) NOT NULL
);
GO

CREATE TABLE dbo.OrderLines (
    Id        INT             IDENTITY (1, 1) NOT NULL CONSTRAINT PK_OrderLines PRIMARY KEY,
    OrderId   INT             NOT NULL CONSTRAINT FK_OrderLines_Orders REFERENCES dbo.Orders (Id),
    ProductId INT             NOT NULL,
    Quantity  INT             NOT NULL,
    UnitPrice DECIMAL (10, 2) NOT NULL
);
GO

USE Shop;
GO

ALTER TABLE dbo.Orders
    ADD Status VARCHAR (20) NOT NULL CONSTRAINT DF_Orders_Status DEFAULT ('new');
GO

USE Shop;
GO

-- Lines were created before anyone checked that their product exists.
ALTER TABLE dbo.OrderLines WITH CHECK
    ADD CONSTRAINT FK_OrderLines_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id);
GO

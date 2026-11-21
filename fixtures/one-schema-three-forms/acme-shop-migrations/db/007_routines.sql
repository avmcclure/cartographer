USE Shop;
GO

CREATE FUNCTION [dbo].[OrderTotal] (@OrderId INT)
RETURNS DECIMAL (12, 2)
AS
BEGIN
    RETURN (
        SELECT COALESCE(SUM(l.Quantity * l.UnitPrice), 0)
        FROM dbo.OrderLines AS l
        WHERE l.OrderId = @OrderId
    );
END
GO

CREATE VIEW [dbo].[OrderSummary]
AS
SELECT o.Id,
       c.Name AS Customer,
       o.PlacedAt,
       o.Status,
       dbo.OrderTotal(o.Id) AS Total
FROM dbo.Orders AS o
JOIN dbo.Customers AS c ON c.Id = o.CustomerId;
GO

CREATE PROCEDURE [dbo].[PlaceOrder]
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
END
GO

-- Closes an order: takes its lines out of warehouse stock and posts the total to the ledger.
CREATE PROCEDURE [dbo].[CompleteOrder]
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

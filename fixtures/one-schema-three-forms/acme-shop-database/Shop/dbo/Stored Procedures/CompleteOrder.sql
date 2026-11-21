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

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

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

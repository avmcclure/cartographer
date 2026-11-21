CREATE PROCEDURE dbo.usp_GetCustomerInvoices
    @CustomerId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.InvoiceId, i.IssuedOn, i.Total, i.Paid, c.Name
    FROM dbo.Invoices i
    INNER JOIN dbo.Customers c ON c.CustomerId = i.CustomerId
    WHERE i.CustomerId = @CustomerId
    ORDER BY i.IssuedOn DESC;
END

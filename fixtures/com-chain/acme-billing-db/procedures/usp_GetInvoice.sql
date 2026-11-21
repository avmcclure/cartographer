CREATE PROCEDURE dbo.usp_GetInvoice
    @InvoiceId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT InvoiceId, CustomerId, IssuedOn, Total, Paid
    FROM dbo.Invoices
    WHERE InvoiceId = @InvoiceId;
END

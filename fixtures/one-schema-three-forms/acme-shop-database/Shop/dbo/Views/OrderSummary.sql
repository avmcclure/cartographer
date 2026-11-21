CREATE VIEW [dbo].[OrderSummary]
AS
SELECT o.Id,
       c.Name AS Customer,
       o.PlacedAt,
       o.Status,
       dbo.OrderTotal(o.Id) AS Total
FROM dbo.Orders AS o
JOIN dbo.Customers AS c ON c.Id = o.CustomerId;

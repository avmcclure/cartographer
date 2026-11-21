CREATE TABLE dbo.Invoices
(
    InvoiceId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
    CustomerId INT NOT NULL CONSTRAINT FK_Invoices_Customers REFERENCES dbo.Customers (CustomerId),
    IssuedOn   DATETIME NOT NULL,
    Total      DECIMAL(18,2) NOT NULL,
    Paid       DECIMAL(18,2) NOT NULL CONSTRAINT DF_Invoices_Paid DEFAULT (0),
    ExportedOn DATETIME NULL
);

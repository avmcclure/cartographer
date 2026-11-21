CREATE TABLE dbo.Customers
(
    CustomerId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    Name       NVARCHAR(200) NOT NULL,
    Email      NVARCHAR(320) NULL
);

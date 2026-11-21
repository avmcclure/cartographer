CREATE TABLE [dbo].[Products] (
    [Id]    INT             IDENTITY (1, 1) NOT NULL,
    [Sku]   VARCHAR (20)    NOT NULL,
    [Name]  NVARCHAR (100)  NOT NULL,
    [Price] DECIMAL (10, 2) NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED ([Id] ASC)
);

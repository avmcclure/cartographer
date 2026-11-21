CREATE TABLE [dbo].[Orders] (
    [Id]         INT           IDENTITY (1, 1) NOT NULL,
    [CustomerId] INT           NOT NULL,
    [PlacedAt]   DATETIME2 (0) NOT NULL,
    [Status]     VARCHAR (20)  CONSTRAINT [DF_Orders_Status] DEFAULT ('new') NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Orders_Customers] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id])
);

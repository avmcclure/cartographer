CREATE TABLE [dbo].[OrderAudit] (
    [Id]        BIGINT        IDENTITY (1, 1) NOT NULL,
    [OrderId]   INT           NOT NULL,
    [Status]    VARCHAR (20)  NOT NULL,
    [ChangedAt] DATETIME2 (0) NOT NULL,
    CONSTRAINT [PK_OrderAudit] PRIMARY KEY CLUSTERED ([Id] ASC)
);

USE Shop;
GO

CREATE TABLE [dbo].[OrderAudit] (
    [Id]        BIGINT        IDENTITY (1, 1) NOT NULL,
    [OrderId]   INT           NOT NULL,
    [Status]    VARCHAR (20)  NOT NULL,
    [ChangedAt] DATETIME2 (0) NOT NULL,
    CONSTRAINT [PK_OrderAudit] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- Every new order and every change of status leaves a row in OrderAudit.
CREATE TRIGGER [dbo].[trg_Orders_Audit]
    ON [dbo].[Orders]
    AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.OrderAudit (OrderId, Status, ChangedAt)
    SELECT i.Id, i.Status, SYSUTCDATETIME()
    FROM inserted AS i
    LEFT JOIN deleted AS d ON d.Id = i.Id
    WHERE d.Id IS NULL OR d.Status <> i.Status;
END
GO

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

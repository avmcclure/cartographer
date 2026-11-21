CREATE PROCEDURE [dbo].[PostEntry]
    @Reference INT,
    @Amount    DECIMAL (12, 2)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Entries (Reference, Amount, PostedAt)
    VALUES (@Reference, @Amount, SYSUTCDATETIME());
END

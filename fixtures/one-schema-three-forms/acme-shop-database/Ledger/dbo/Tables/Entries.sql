CREATE TABLE [dbo].[Entries] (
    [Id]        BIGINT          IDENTITY (1, 1) NOT NULL,
    [Reference] INT             NOT NULL,
    [Amount]    DECIMAL (12, 2) NOT NULL,
    [PostedAt]  DATETIME2 (0)   NOT NULL,
    CONSTRAINT [PK_Entries] PRIMARY KEY CLUSTERED ([Id] ASC)
);

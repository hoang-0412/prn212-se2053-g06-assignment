USE [BookStoreDB];
GO

IF COL_LENGTH(N'[dbo].[Order]', N'cancelReason') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order]
    ADD [cancelReason] NVARCHAR(500) NULL;
END
GO

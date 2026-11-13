USE [BookStoreDB];
GO

IF COL_LENGTH(N'[dbo].[User]', N'userStatus') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [userStatus] NVARCHAR(20) NOT NULL
        CONSTRAINT [DF_User_userStatus] DEFAULT N'active';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE [name] = N'CK_User_userStatus'
      AND [parent_object_id] = OBJECT_ID(N'[dbo].[User]')
)
BEGIN
    ALTER TABLE [dbo].[User]
    ADD CONSTRAINT [CK_User_userStatus]
        CHECK ([userStatus] IN (N'active', N'deActive'));
END
GO

IF COL_LENGTH(N'[dbo].[User]', N'banReason') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [banReason] NVARCHAR(500) NULL;
END
GO

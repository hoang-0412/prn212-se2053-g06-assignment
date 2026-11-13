USE [BookStoreDB];
GO

IF OBJECT_ID(N'[dbo].[Voucher]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[Voucher]
	(
		[voucherId] INT IDENTITY(1,1) NOT NULL,
		[name] NVARCHAR(150) NOT NULL,
		[code] VARCHAR(50) NOT NULL,
		[discountType] VARCHAR(20) NOT NULL,
		[discountValue] DECIMAL(18,2) NOT NULL,
		[startAt] DATETIME2(7) NOT NULL,
		[expiryAt] DATETIME2(7) NOT NULL,
		[maxUsagePerUser] INT NOT NULL CONSTRAINT [DF_Voucher_MaxUsagePerUser] DEFAULT (1),
		[totalQuantity] INT NOT NULL CONSTRAINT [DF_Voucher_TotalQuantity] DEFAULT (0),
		[savedCount] INT NOT NULL CONSTRAINT [DF_Voucher_SavedCount] DEFAULT (0),
		[usedCount] INT NOT NULL CONSTRAINT [DF_Voucher_UsedCount] DEFAULT (0),
		[isActive] BIT NOT NULL CONSTRAINT [DF_Voucher_IsActive] DEFAULT (1),
		[createdAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_Voucher_CreatedAt] DEFAULT (sysutcdatetime()),
		CONSTRAINT [PK_Voucher] PRIMARY KEY CLUSTERED ([voucherId]),
		CONSTRAINT [UQ_Voucher_Code] UNIQUE ([code]),
		CONSTRAINT [CK_Voucher_DiscountType] CHECK ([discountType] IN ('Percent', 'Amount')),
		CONSTRAINT [CK_Voucher_DiscountValue] CHECK ([discountValue] >= 0),
		CONSTRAINT [CK_Voucher_Quantity] CHECK ([maxUsagePerUser] > 0 AND [totalQuantity] >= 0 AND [savedCount] >= 0 AND [usedCount] >= 0)
	);
END
GO

IF OBJECT_ID(N'[dbo].[UserVoucher]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[UserVoucher]
	(
		[userVoucherId] INT IDENTITY(1,1) NOT NULL,
		[userId] INT NOT NULL,
		[voucherId] INT NOT NULL,
		[usageCount] INT NOT NULL CONSTRAINT [DF_UserVoucher_UsageCount] DEFAULT (0),
		[savedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_UserVoucher_SavedAt] DEFAULT (sysutcdatetime()),
		[lastUsedAt] DATETIME2(7) NULL,
		CONSTRAINT [PK_UserVoucher] PRIMARY KEY CLUSTERED ([userVoucherId]),
		CONSTRAINT [UQ_UserVoucher_User_Voucher] UNIQUE ([userId], [voucherId]),
		CONSTRAINT [FK_UserVoucher_User] FOREIGN KEY ([userId]) REFERENCES [dbo].[User]([userId]) ON DELETE CASCADE,
		CONSTRAINT [FK_UserVoucher_Voucher] FOREIGN KEY ([voucherId]) REFERENCES [dbo].[Voucher]([voucherId]) ON DELETE CASCADE,
		CONSTRAINT [CK_UserVoucher_UsageCount] CHECK ([usageCount] >= 0)
	);
END
GO

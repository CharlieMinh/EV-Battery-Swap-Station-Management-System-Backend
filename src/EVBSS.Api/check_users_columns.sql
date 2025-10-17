CREATE TABLE [BatteryModels] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Voltage] int NOT NULL,
    [CapacityWh] int NOT NULL,
    [Manufacturer] nvarchar(max) NULL,
    CONSTRAINT [PK_BatteryModels] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Stations] (
    [Id] uniqueidentifier NOT NULL,
    [DisplayId] nvarchar(max) NULL,
    [Name] nvarchar(200) NOT NULL,
    [Address] nvarchar(500) NOT NULL,
    [City] nvarchar(100) NOT NULL,
    [Lat] float NOT NULL,
    [Lng] float NOT NULL,
    [IsActive] bit NOT NULL,
    [OpenTime] time NOT NULL,
    [CloseTime] time NOT NULL,
    [PhoneNumber] nvarchar(20) NULL,
    [PrimaryImageUrl] nvarchar(500) NULL,
    CONSTRAINT [PK_Stations] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [Email] nvarchar(255) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NULL,
    [Phone] nvarchar(max) NULL,
    [Role] int NOT NULL,
    [Status] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [LastLogin] datetime2 NULL,
    [AuthMethod] int NOT NULL,
    [GoogleId] nvarchar(max) NULL,
    [ProfilePictureUrl] nvarchar(max) NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [SubscriptionPlans] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [MonthlyFeeUnder1500Km] decimal(18,2) NOT NULL,
    [MonthlyFee1500To3000Km] decimal(18,2) NOT NULL,
    [MonthlyFeeOver3000Km] decimal(18,2) NOT NULL,
    [DepositAmount] decimal(18,2) NOT NULL,
    [BatteryModelId] uniqueidentifier NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [BillingCycleDay] int NOT NULL,
    [OverdueInterestRate] decimal(5,4) NOT NULL,
    [MaxOverdueMonths] int NOT NULL,
    CONSTRAINT [PK_SubscriptionPlans] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SubscriptionPlans_BatteryModels_BatteryModelId] FOREIGN KEY ([BatteryModelId]) REFERENCES [BatteryModels] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [VehicleModels] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [FullName] nvarchar(200) NOT NULL,
    [Brand] nvarchar(100) NOT NULL,
    [CompatibleBatteryModelId] uniqueidentifier NOT NULL,
    [ImageUrl] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [Description] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_VehicleModels] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_VehicleModels_BatteryModels_CompatibleBatteryModelId] FOREIGN KEY ([CompatibleBatteryModelId]) REFERENCES [BatteryModels] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [BatteryInventories] (
    [Id] uniqueidentifier NOT NULL,
    [BatteryModelId] uniqueidentifier NOT NULL,
    [StationId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [Quantity] int NOT NULL DEFAULT 0,
    [UpdatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_BatteryInventories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BatteryInventories_BatteryModels_BatteryModelId] FOREIGN KEY ([BatteryModelId]) REFERENCES [BatteryModels] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_BatteryInventories_Stations_StationId] FOREIGN KEY ([StationId]) REFERENCES [Stations] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [BatteryUnits] (
    [Id] uniqueidentifier NOT NULL,
    [Serial] nvarchar(450) NOT NULL,
    [BatteryModelId] uniqueidentifier NOT NULL,
    [StationId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [IsReserved] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_BatteryUnits] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BatteryUnits_BatteryModels_BatteryModelId] FOREIGN KEY ([BatteryModelId]) REFERENCES [BatteryModels] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BatteryUnits_Stations_StationId] FOREIGN KEY ([StationId]) REFERENCES [Stations] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [PasswordResetTokens] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OtpHash] nvarchar(255) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsUsed] bit NOT NULL DEFAULT CAST(0 AS bit),
    [UsedAt] datetime2 NULL,
    [RequestIpAddress] nvarchar(45) NULL,
    [RequestUserAgent] nvarchar(500) NULL,
    [AttemptCount] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PasswordResetTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Vehicles] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [VIN] nvarchar(17) NOT NULL,
    [Plate] nvarchar(20) NOT NULL,
    [VehicleModelId] uniqueidentifier NULL,
    [CompatibleBatteryModelId] uniqueidentifier NOT NULL,
    [PhotoUrl] nvarchar(500) NULL,
    [RegistrationPhotoUrl] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Vehicles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Vehicles_BatteryModels_CompatibleBatteryModelId] FOREIGN KEY ([CompatibleBatteryModelId]) REFERENCES [BatteryModels] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Vehicles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Vehicles_VehicleModels_VehicleModelId] FOREIGN KEY ([VehicleModelId]) REFERENCES [VehicleModels] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Reservations] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [StationId] uniqueidentifier NOT NULL,
    [BatteryModelId] uniqueidentifier NOT NULL,
    [BatteryUnitId] uniqueidentifier NULL,
    [SlotDate] date NOT NULL,
    [SlotStartTime] time NOT NULL,
    [SlotEndTime] time NOT NULL,
    [QRCode] nvarchar(max) NULL,
    [CheckedInAt] datetime2 NULL,
    [VerifiedByStaffId] uniqueidentifier NULL,
    [Status] int NOT NULL,
    [CancelReason] int NULL,
    [CancelNote] nvarchar(max) NULL,
    [CancelledAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Reservations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reservations_BatteryModels_BatteryModelId] FOREIGN KEY ([BatteryModelId]) REFERENCES [BatteryModels] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reservations_BatteryUnits_BatteryUnitId] FOREIGN KEY ([BatteryUnitId]) REFERENCES [BatteryUnits] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reservations_Stations_StationId] FOREIGN KEY ([StationId]) REFERENCES [Stations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reservations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reservations_Users_VerifiedByStaffId] FOREIGN KEY ([VerifiedByStaffId]) REFERENCES [Users] ([Id])
);
GO


CREATE TABLE [UserSubscriptions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [SubscriptionPlanId] uniqueidentifier NOT NULL,
    [VehicleId] uniqueidentifier NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [CurrentBillingPeriodStart] datetime2 NOT NULL,
    [CurrentBillingPeriodEnd] datetime2 NOT NULL,
    [CurrentMonthKmUsed] int NOT NULL,
    [DepositPaid] decimal(18,2) NOT NULL,
    [DepositPaidDate] datetime2 NULL,
    [ConsecutiveOverdueMonths] int NOT NULL,
    [IsBlocked] bit NOT NULL,
    [ChargingLimitPercent] int NOT NULL,
    [LastPaymentDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_UserSubscriptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserSubscriptions_SubscriptionPlans_SubscriptionPlanId] FOREIGN KEY ([SubscriptionPlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserSubscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserSubscriptions_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Invoices] (
    [Id] uniqueidentifier NOT NULL,
    [InvoiceNumber] nvarchar(50) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [UserSubscriptionId] uniqueidentifier NULL,
    [Type] int NOT NULL,
    [IssueDate] datetime2 NOT NULL,
    [DueDate] datetime2 NOT NULL,
    [PaidDate] datetime2 NULL,
    [BillingPeriodStart] datetime2 NULL,
    [BillingPeriodEnd] datetime2 NULL,
    [KmUsedInPeriod] int NULL,
    [SubtotalAmount] decimal(18,2) NOT NULL,
    [TaxAmount] decimal(18,2) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [PaidAmount] decimal(18,2) NOT NULL,
    [OverdueFeeAmount] decimal(18,2) NOT NULL,
    [Status] int NOT NULL,
    [Notes] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Invoices_UserSubscriptions_UserSubscriptionId] FOREIGN KEY ([UserSubscriptionId]) REFERENCES [UserSubscriptions] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Invoices_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Payments] (
    [Id] uniqueidentifier NOT NULL,
    [PaymentReference] nvarchar(100) NOT NULL,
    [InvoiceId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Method] int NOT NULL,
    [Type] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Status] int NOT NULL,
    [VnpTxnRef] nvarchar(max) NULL,
    [VnpTransactionNo] nvarchar(max) NULL,
    [VnpResponseCode] nvarchar(max) NULL,
    [VnpSecureHash] nvarchar(max) NULL,
    [VnpPayDate] datetime2 NULL,
    [ProcessedByStaffId] uniqueidentifier NULL,
    [StationId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ProcessedAt] datetime2 NULL,
    [CompletedAt] datetime2 NULL,
    [Notes] nvarchar(max) NULL,
    [FailureReason] nvarchar(max) NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Payments_Stations_StationId] FOREIGN KEY ([StationId]) REFERENCES [Stations] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Payments_Users_ProcessedByStaffId] FOREIGN KEY ([ProcessedByStaffId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Payments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [SwapTransactions] (
    [Id] uniqueidentifier NOT NULL,
    [TransactionNumber] nvarchar(50) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [ReservationId] uniqueidentifier NULL,
    [StationId] uniqueidentifier NOT NULL,
    [VehicleId] uniqueidentifier NOT NULL,
    [UserSubscriptionId] uniqueidentifier NULL,
    [InvoiceId] uniqueidentifier NULL,
    [IssuedBatteryId] uniqueidentifier NOT NULL,
    [ReturnedBatteryId] uniqueidentifier NULL,
    [IssuedBatterySerial] nvarchar(max) NOT NULL,
    [ReturnedBatterySerial] nvarchar(max) NULL,
    [CheckedInByStaffId] uniqueidentifier NULL,
    [BatteryIssuedByStaffId] uniqueidentifier NULL,
    [BatteryReceivedByStaffId] uniqueidentifier NULL,
    [CompletedByStaffId] uniqueidentifier NULL,
    [VehicleOdoAtSwap] int NOT NULL,
    [BatteryHealthIssued] int NULL,
    [BatteryHealthReturned] int NULL,
    [PaymentType] int NOT NULL,
    [SwapFee] decimal(18,2) NOT NULL,
    [KmChargeAmount] decimal(18,2) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [IsPaid] bit NOT NULL,
    [Status] int NOT NULL,
    [StartedAt] datetime2 NOT NULL,
    [CheckedInAt] datetime2 NULL,
    [BatteryIssuedAt] datetime2 NULL,
    [BatteryReturnedAt] datetime2 NULL,
    [CompletedAt] datetime2 NULL,
    [CancelledAt] datetime2 NULL,
    [Notes] nvarchar(max) NULL,
    [CancellationReason] nvarchar(max) NULL,
    [Rating] int NULL,
    [Feedback] nvarchar(max) NULL,
    [RatedAt] datetime2 NULL,
    CONSTRAINT [PK_SwapTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SwapTransactions_BatteryUnits_IssuedBatteryId] FOREIGN KEY ([IssuedBatteryId]) REFERENCES [BatteryUnits] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SwapTransactions_BatteryUnits_ReturnedBatteryId] FOREIGN KEY ([ReturnedBatteryId]) REFERENCES [BatteryUnits] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_SwapTransactions_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_SwapTransactions_Reservations_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [Reservations] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_SwapTransactions_Stations_StationId] FOREIGN KEY ([StationId]) REFERENCES [Stations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SwapTransactions_UserSubscriptions_UserSubscriptionId] FOREIGN KEY ([UserSubscriptionId]) REFERENCES [UserSubscriptions] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_SwapTransactions_Users_BatteryIssuedByStaffId] FOREIGN KEY ([BatteryIssuedByStaffId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_SwapTransactions_Users_BatteryReceivedByStaffId] FOREIGN KEY ([BatteryReceivedByStaffId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_SwapTransactions_Users_CheckedInByStaffId] FOREIGN KEY ([CheckedInByStaffId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_SwapTransactions_Users_CompletedByStaffId] FOREIGN KEY ([CompletedByStaffId]) REFERENCES [Users] ([Id]),
    CONSTRAINT [FK_SwapTransactions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SwapTransactions_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([Id]) ON DELETE NO ACTION
);
GO


CREATE UNIQUE INDEX [IX_BatteryInventories_BatteryModelId_StationId_Status] ON [BatteryInventories] ([BatteryModelId], [StationId], [Status]);
GO


CREATE INDEX [IX_BatteryInventories_StationId] ON [BatteryInventories] ([StationId]);
GO


CREATE INDEX [IX_BatteryUnits_BatteryModelId] ON [BatteryUnits] ([BatteryModelId]);
GO


CREATE UNIQUE INDEX [IX_BatteryUnits_Serial] ON [BatteryUnits] ([Serial]);
GO


CREATE INDEX [IX_BatteryUnits_StationId_Status] ON [BatteryUnits] ([StationId], [Status]);
GO


CREATE INDEX [IX_BatteryUnits_StationId_Status_IsReserved] ON [BatteryUnits] ([StationId], [Status], [IsReserved]);
GO


CREATE UNIQUE INDEX [IX_Invoices_InvoiceNumber] ON [Invoices] ([InvoiceNumber]);
GO


CREATE INDEX [IX_Invoices_UserId] ON [Invoices] ([UserId]);
GO


CREATE INDEX [IX_Invoices_UserSubscriptionId] ON [Invoices] ([UserSubscriptionId]);
GO


CREATE INDEX [IX_PasswordResetTokens_ExpiresAt] ON [PasswordResetTokens] ([ExpiresAt]);
GO


CREATE INDEX [IX_PasswordResetTokens_UserId] ON [PasswordResetTokens] ([UserId]);
GO


CREATE INDEX [IX_PasswordResetTokens_UserId_IsUsed_ExpiresAt] ON [PasswordResetTokens] ([UserId], [IsUsed], [ExpiresAt]);
GO


CREATE INDEX [IX_Payments_InvoiceId] ON [Payments] ([InvoiceId]);
GO


CREATE UNIQUE INDEX [IX_Payments_PaymentReference] ON [Payments] ([PaymentReference]);
GO


CREATE INDEX [IX_Payments_ProcessedByStaffId] ON [Payments] ([ProcessedByStaffId]);
GO


CREATE INDEX [IX_Payments_StationId] ON [Payments] ([StationId]);
GO


CREATE INDEX [IX_Payments_UserId] ON [Payments] ([UserId]);
GO


CREATE INDEX [IX_Reservations_BatteryModelId] ON [Reservations] ([BatteryModelId]);
GO


CREATE INDEX [IX_Reservations_BatteryUnitId] ON [Reservations] ([BatteryUnitId]);
GO


CREATE INDEX [IX_Reservations_StationId_Status] ON [Reservations] ([StationId], [Status]);
GO


CREATE INDEX [IX_Reservations_UserId_CreatedAt] ON [Reservations] ([UserId], [CreatedAt]);
GO


CREATE INDEX [IX_Reservations_VerifiedByStaffId] ON [Reservations] ([VerifiedByStaffId]);
GO


CREATE INDEX [IX_Stations_City_IsActive] ON [Stations] ([City], [IsActive]);
GO


CREATE INDEX [IX_SubscriptionPlans_BatteryModelId] ON [SubscriptionPlans] ([BatteryModelId]);
GO


CREATE UNIQUE INDEX [IX_SubscriptionPlans_Name] ON [SubscriptionPlans] ([Name]);
GO


CREATE INDEX [IX_SwapTransactions_BatteryIssuedByStaffId] ON [SwapTransactions] ([BatteryIssuedByStaffId]);
GO


CREATE INDEX [IX_SwapTransactions_BatteryReceivedByStaffId] ON [SwapTransactions] ([BatteryReceivedByStaffId]);
GO


CREATE INDEX [IX_SwapTransactions_CheckedInByStaffId] ON [SwapTransactions] ([CheckedInByStaffId]);
GO


CREATE INDEX [IX_SwapTransactions_CompletedByStaffId] ON [SwapTransactions] ([CompletedByStaffId]);
GO


CREATE INDEX [IX_SwapTransactions_InvoiceId] ON [SwapTransactions] ([InvoiceId]);
GO


CREATE INDEX [IX_SwapTransactions_IssuedBatteryId] ON [SwapTransactions] ([IssuedBatteryId]);
GO


CREATE INDEX [IX_SwapTransactions_ReservationId] ON [SwapTransactions] ([ReservationId]);
GO


CREATE INDEX [IX_SwapTransactions_ReturnedBatteryId] ON [SwapTransactions] ([ReturnedBatteryId]);
GO


CREATE INDEX [IX_SwapTransactions_StationId] ON [SwapTransactions] ([StationId]);
GO


CREATE UNIQUE INDEX [IX_SwapTransactions_TransactionNumber] ON [SwapTransactions] ([TransactionNumber]);
GO


CREATE INDEX [IX_SwapTransactions_UserId] ON [SwapTransactions] ([UserId]);
GO


CREATE INDEX [IX_SwapTransactions_UserSubscriptionId] ON [SwapTransactions] ([UserSubscriptionId]);
GO


CREATE INDEX [IX_SwapTransactions_VehicleId] ON [SwapTransactions] ([VehicleId]);
GO


CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
GO


CREATE INDEX [IX_UserSubscriptions_SubscriptionPlanId] ON [UserSubscriptions] ([SubscriptionPlanId]);
GO


CREATE INDEX [IX_UserSubscriptions_UserId_VehicleId_IsActive] ON [UserSubscriptions] ([UserId], [VehicleId], [IsActive]);
GO


CREATE INDEX [IX_UserSubscriptions_VehicleId] ON [UserSubscriptions] ([VehicleId]);
GO


CREATE INDEX [IX_VehicleModels_CompatibleBatteryModelId] ON [VehicleModels] ([CompatibleBatteryModelId]);
GO


CREATE UNIQUE INDEX [IX_VehicleModels_Name] ON [VehicleModels] ([Name]);
GO


CREATE INDEX [IX_Vehicles_CompatibleBatteryModelId] ON [Vehicles] ([CompatibleBatteryModelId]);
GO


CREATE UNIQUE INDEX [IX_Vehicles_UserId_Plate] ON [Vehicles] ([UserId], [Plate]);
GO


CREATE UNIQUE INDEX [IX_Vehicles_UserId_VIN] ON [Vehicles] ([UserId], [VIN]);
GO


CREATE INDEX [IX_Vehicles_VehicleModelId] ON [Vehicles] ([VehicleModelId]);
GO



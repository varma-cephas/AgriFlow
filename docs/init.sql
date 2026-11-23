/*
   AgriFlow complete SQL Server setup
   Target: DESKTOP-N1EJ64N\SQLEXPRESS / AgriFlowDB

   WARNING: This script deletes existing AgriFlow tables and data.
   Run only for a development database.
*/

IF DB_ID(N'AgriFlowDB') IS NULL
BEGIN
	CREATE DATABASE AgriFlowDB;
END
GO

USE AgriFlowDB;
GO

/* ----------------------------- CLEAN RESET ----------------------------- */
IF OBJECT_ID(N'dbo.order_items', N'U') IS NOT NULL DROP TABLE dbo.order_items;
IF OBJECT_ID(N'dbo.order_item', N'U') IS NOT NULL DROP TABLE dbo.order_item;
IF OBJECT_ID(N'dbo.orders', N'U') IS NOT NULL DROP TABLE dbo.orders;
IF OBJECT_ID(N'dbo.harvest_listing', N'U') IS NOT NULL DROP TABLE dbo.harvest_listing;
IF OBJECT_ID(N'dbo.listing_report', N'U') IS NOT NULL DROP TABLE dbo.listing_report;
IF OBJECT_ID(N'dbo.listing', N'U') IS NOT NULL DROP TABLE dbo.listing;
IF OBJECT_ID(N'dbo.planting_batch', N'U') IS NOT NULL DROP TABLE dbo.planting_batch;
IF OBJECT_ID(N'dbo.supply_stock', N'U') IS NOT NULL DROP TABLE dbo.supply_stock;
IF OBJECT_ID(N'dbo.farm_visit_request', N'U') IS NOT NULL DROP TABLE dbo.farm_visit_request;
IF OBJECT_ID(N'dbo.agent_farmer_assignment', N'U') IS NOT NULL DROP TABLE dbo.agent_farmer_assignment;
IF OBJECT_ID(N'dbo.audit_log', N'U') IS NOT NULL DROP TABLE dbo.audit_log;
IF OBJECT_ID(N'dbo.farmer_profile', N'U') IS NOT NULL DROP TABLE dbo.farmer_profile;
IF OBJECT_ID(N'dbo.app_user', N'U') IS NOT NULL DROP TABLE dbo.app_user;
IF OBJECT_ID(N'dbo.buyer_business', N'U') IS NOT NULL DROP TABLE dbo.buyer_business;
GO

/* ----------------------------- USERS ----------------------------- */
CREATE TABLE dbo.app_user
(
	Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_app_user PRIMARY KEY,
	FullName        NVARCHAR(150) NOT NULL,
	Email           NVARCHAR(256) NULL,
	Phone           NVARCHAR(20) NULL,
	PasswordHash    NVARCHAR(500) NULL,
	Role            NVARCHAR(10) NOT NULL,
	IsActive        BIT NOT NULL CONSTRAINT DF_app_user_IsActive DEFAULT 1,
	SuspendedReason NVARCHAR(300) NULL,
	BuyerBusinessId INT NULL,
	JobTitle        NVARCHAR(100) NULL,
	CreatedAt       DATETIME2 NOT NULL CONSTRAINT DF_app_user_CreatedAt DEFAULT SYSUTCDATETIME(),
	LastLoginAt     DATETIME2 NULL,
	CONSTRAINT CK_app_user_Role CHECK (Role IN ('Farmer','Buyer','Agent','Admin','Customer'))
);
GO

CREATE UNIQUE INDEX UX_user_email
	ON dbo.app_user(Email)
	WHERE Email IS NOT NULL;
GO

/* ----------------------------- BUYER BUSINESSES ----------------------------- */
CREATE TABLE dbo.buyer_business
(
	Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_buyer_business PRIMARY KEY,
	Name               NVARCHAR(150) NOT NULL,
	BusinessType       NVARCHAR(30) NOT NULL,
	RdbCompanyCode     NVARCHAR(20) NOT NULL,
	Address            NVARCHAR(250) NOT NULL,
	City               NVARCHAR(80) NOT NULL,
	Phone              NVARCHAR(20) NULL,
	VerificationStatus NVARCHAR(10) NOT NULL CONSTRAINT DF_buyer_business_Status DEFAULT 'Pending',
	VerifiedByUserId   INT NULL,
	VerifiedAt         DATETIME2 NULL,
	RejectionReason    NVARCHAR(500) NULL,
	CreatedAt          DATETIME2 NOT NULL CONSTRAINT DF_buyer_business_CreatedAt DEFAULT SYSUTCDATETIME(),
	CONSTRAINT UX_bb_rdb UNIQUE (RdbCompanyCode),
	CONSTRAINT CK_bb_type CHECK (BusinessType IN ('Supermarket','Hotel','Restaurant','School','Hospital','Processor','Exporter','Other')),
	CONSTRAINT CK_bb_status CHECK (VerificationStatus IN ('Pending','Verified','Rejected'))
);
GO

ALTER TABLE dbo.app_user
	ADD CONSTRAINT FK_user_business
	FOREIGN KEY (BuyerBusinessId) REFERENCES dbo.buyer_business(Id);
GO

ALTER TABLE dbo.buyer_business
	ADD CONSTRAINT FK_buyer_business_VerifiedBy
	FOREIGN KEY (VerifiedByUserId) REFERENCES dbo.app_user(Id);
GO

/* ----------------------------- FARMER PROFILES ----------------------------- */
CREATE TABLE dbo.farmer_profile
(
	UserId              INT NOT NULL CONSTRAINT PK_farmer_profile PRIMARY KEY,
	FarmName            NVARCHAR(150) NOT NULL,
	NationalIdEncrypted VARBINARY(512) NOT NULL,
	NationalIdHash      VARBINARY(32) NOT NULL,
	NationalIdMasked    NVARCHAR(30) NOT NULL,
	LandUpi             NVARCHAR(30) NOT NULL,
	Province            NVARCHAR(40) NOT NULL,
	District            NVARCHAR(60) NOT NULL,
	Sector              NVARCHAR(60) NOT NULL,
	Cell                NVARCHAR(60) NOT NULL,
	Village             NVARCHAR(60) NOT NULL,
	MainCrops           NVARCHAR(200) NOT NULL,
	FarmSizeHa          DECIMAL(8,2) NULL,
	CooperativeName     NVARCHAR(150) NULL,
	CooperativeMemberNo NVARCHAR(50) NULL,
	PreferredLanguage   NVARCHAR(20) NOT NULL CONSTRAINT DF_farmer_profile_Language DEFAULT 'Kinyarwanda',
	VerificationStatus  NVARCHAR(12) NOT NULL CONSTRAINT DF_farmer_profile_Status DEFAULT 'Unverified',
	VerifiedByUserId    INT NULL,
	VerifiedAt          DATETIME2 NULL,
	RejectionReason     NVARCHAR(500) NULL,
	RegisteredByAgentId INT NULL,
	CreatedAt           DATETIME2 NOT NULL CONSTRAINT DF_farmer_profile_CreatedAt DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_farmer_profile_User FOREIGN KEY (UserId) REFERENCES dbo.app_user(Id),
	CONSTRAINT UX_fp_nid UNIQUE (NationalIdHash),
	CONSTRAINT UX_fp_upi UNIQUE (LandUpi),
	CONSTRAINT CK_fp_size CHECK (FarmSizeHa IS NULL OR FarmSizeHa > 0),
	CONSTRAINT CK_fp_status CHECK (VerificationStatus IN ('Unverified','Verified','Rejected')),
	CONSTRAINT FK_farmer_profile_VerifiedBy FOREIGN KEY (VerifiedByUserId) REFERENCES dbo.app_user(Id),
	CONSTRAINT FK_farmer_profile_Agent FOREIGN KEY (RegisteredByAgentId) REFERENCES dbo.app_user(Id)
);
GO

/* ----------------------------- AUDIT ----------------------------- */
CREATE TABLE dbo.audit_log
(
	Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_audit_log PRIMARY KEY,
	ActorUserId INT NOT NULL,
	Entity      NVARCHAR(80) NOT NULL,
	EntityId    NVARCHAR(80) NOT NULL,
	Action      NVARCHAR(120) NOT NULL,
	Summary     NVARCHAR(1000) NULL,
	Result      NVARCHAR(30) NOT NULL CONSTRAINT DF_audit_log_Result DEFAULT 'Success',
	CreatedAt   DATETIME2 NOT NULL CONSTRAINT DF_audit_log_CreatedAt DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_audit_log_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.app_user(Id)
);
GO

/* ----------------------------- FARMER/AGENT SUPPORT ----------------------------- */
CREATE TABLE dbo.agent_farmer_assignment
(
	Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_agent_farmer_assignment PRIMARY KEY,
	AgentId    INT NOT NULL,
	FarmerId   INT NOT NULL,
	AssignedAt DATETIME2 NOT NULL CONSTRAINT DF_assignment_AssignedAt DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_assignment_Agent FOREIGN KEY (AgentId) REFERENCES dbo.app_user(Id),
	CONSTRAINT FK_assignment_Farmer FOREIGN KEY (FarmerId) REFERENCES dbo.farmer_profile(UserId),
	CONSTRAINT UQ_assignment_Farmer UNIQUE (FarmerId)
);
GO

CREATE TABLE dbo.farm_visit_request
(
	Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_farm_visit_request PRIMARY KEY,
	FarmerId          INT NOT NULL,
	AgentId           INT NOT NULL,
	RequestedByUserId INT NOT NULL,
	VisitBy           DATE NOT NULL,
	Note              NVARCHAR(500) NULL,
	Status            NVARCHAR(10) NOT NULL CONSTRAINT DF_visit_Status DEFAULT 'Requested',
	CompletedAt       DATETIME2 NULL,
	CreatedAt         DATETIME2 NOT NULL CONSTRAINT DF_visit_CreatedAt DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_visit_Farmer FOREIGN KEY (FarmerId) REFERENCES dbo.farmer_profile(UserId),
	CONSTRAINT FK_visit_Agent FOREIGN KEY (AgentId) REFERENCES dbo.app_user(Id),
	CONSTRAINT FK_visit_RequestedBy FOREIGN KEY (RequestedByUserId) REFERENCES dbo.app_user(Id),
	CONSTRAINT CK_visit_Status CHECK (Status IN ('Requested','Done','Cancelled'))
);
GO

/* ----------------------------- FARM PRODUCTION ----------------------------- */
CREATE TABLE dbo.supply_stock
(
	Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_supply_stock PRIMARY KEY,
	FarmerId     INT NOT NULL,
	CropName     NVARCHAR(100) NOT NULL,
	MaturityDays INT NOT NULL,
	CurrentStock DECIMAL(10,2) NOT NULL,
	Unit         NVARCHAR(20) NOT NULL CONSTRAINT DF_supply_stock_Unit DEFAULT 'kg',
	UpdatedAt    DATETIME2 NOT NULL CONSTRAINT DF_supply_stock_UpdatedAt DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_supply_stock_Farmer FOREIGN KEY (FarmerId) REFERENCES dbo.farmer_profile(UserId),
	CONSTRAINT CK_supply_stock_Maturity CHECK (MaturityDays > 0),
	CONSTRAINT CK_supply_stock_Stock CHECK (CurrentStock >= 0)
);
GO

CREATE TABLE dbo.planting_batch
(
	Id                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_planting_batch PRIMARY KEY,
	FarmerId              INT NOT NULL,
	SupplyStockId         INT NOT NULL,
	QuantityPlanted       DECIMAL(10,2) NOT NULL,
	PlantingDate          DATE NOT NULL,
	PredictedHarvestDate  DATE NOT NULL,
	Status                NVARCHAR(20) NOT NULL CONSTRAINT DF_planting_batch_Status DEFAULT 'active',
	ActualYield           DECIMAL(10,2) NULL,
	YieldUnit             NVARCHAR(20) NULL,
	HarvestDate           DATE NULL,
	CreatedAt             DATETIME2 NOT NULL CONSTRAINT DF_planting_batch_CreatedAt DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_planting_batch_Farmer FOREIGN KEY (FarmerId) REFERENCES dbo.farmer_profile(UserId),
	CONSTRAINT FK_planting_batch_Stock FOREIGN KEY (SupplyStockId) REFERENCES dbo.supply_stock(Id),
	CONSTRAINT CK_planting_batch_Quantity CHECK (QuantityPlanted > 0)
);
GO

/* ----------------------------- MARKETPLACE ----------------------------- */
CREATE TABLE dbo.harvest_listing
(
	Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_harvest_listing PRIMARY KEY,
	FarmerId          INT NOT NULL,
	BatchId           INT NOT NULL,
	CropName          NVARCHAR(100) NOT NULL,
	AvailableQuantity DECIMAL(10,2) NOT NULL,
	Unit              NVARCHAR(20) NOT NULL CONSTRAINT DF_harvest_listing_Unit DEFAULT 'kg',
	PricePerUnit      DECIMAL(10,2) NOT NULL,
	IsActive           BIT NOT NULL CONSTRAINT DF_harvest_listing_IsActive DEFAULT 1,
	CreatedAt         DATETIME2 NOT NULL CONSTRAINT DF_harvest_listing_CreatedAt DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_harvest_listing_Farmer FOREIGN KEY (FarmerId) REFERENCES dbo.farmer_profile(UserId),
	CONSTRAINT FK_harvest_listing_Batch FOREIGN KEY (BatchId) REFERENCES dbo.planting_batch(Id),
	CONSTRAINT CK_harvest_listing_Quantity CHECK (AvailableQuantity >= 0),
	CONSTRAINT CK_harvest_listing_Price CHECK (PricePerUnit >= 0)
);
GO

CREATE TABLE dbo.orders
(
	Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_orders PRIMARY KEY,
	CustomerId   INT NOT NULL,
	TotalAmount  DECIMAL(10,2) NOT NULL,
	Status       NVARCHAR(50) NOT NULL CONSTRAINT DF_orders_Status DEFAULT 'pending',
	CreatedAt    DATETIME2 NOT NULL CONSTRAINT DF_orders_CreatedAt DEFAULT SYSUTCDATETIME(),
	CONSTRAINT FK_orders_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.app_user(Id),
	CONSTRAINT CK_orders_Total CHECK (TotalAmount >= 0)
);
GO

CREATE TABLE dbo.order_items
(
	Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_order_items PRIMARY KEY,
	OrderId           INT NOT NULL,
	HarvestListingId  INT NOT NULL,
	QuantityOrdered   DECIMAL(10,2) NOT NULL,
	PriceAtPurchase   DECIMAL(10,2) NOT NULL,
	CONSTRAINT FK_order_items_Order FOREIGN KEY (OrderId) REFERENCES dbo.orders(Id) ON DELETE CASCADE,
	CONSTRAINT FK_order_items_Listing FOREIGN KEY (HarvestListingId) REFERENCES dbo.harvest_listing(Id),
	CONSTRAINT CK_order_items_Quantity CHECK (QuantityOrdered > 0),
	CONSTRAINT CK_order_items_Price CHECK (PriceAtPurchase >= 0)
);
GO

/* ----------------------------- VALIDATION ----------------------------- */
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN
(
	'app_user', 'buyer_business', 'farmer_profile', 'audit_log',
	'agent_farmer_assignment', 'farm_visit_request', 'supply_stock',
	'planting_batch', 'harvest_listing', 'orders', 'order_items'
)
ORDER BY TABLE_NAME;
GO

/*
   No demo users are inserted here because PasswordHash must contain a BCrypt
   hash generated by the application. Register users through the application.
*/

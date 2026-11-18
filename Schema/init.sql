-- =========================================================================
-- AgriFlow Database Schema & Seed Data
-- =========================================================================

-- Drop existing tables in reverse order of dependencies to allow clean re-runs
IF OBJECT_ID('dbo.order_items', 'U') IS NOT NULL DROP TABLE dbo.order_items;
IF OBJECT_ID('dbo.orders', 'U') IS NOT NULL DROP TABLE dbo.orders;
IF OBJECT_ID('dbo.harvest_listing', 'U') IS NOT NULL DROP TABLE dbo.harvest_listing;
IF OBJECT_ID('dbo.planting_batch', 'U') IS NOT NULL DROP TABLE dbo.planting_batch;
IF OBJECT_ID('dbo.supply_stock', 'U') IS NOT NULL DROP TABLE dbo.supply_stock;
IF OBJECT_ID('dbo.agent_farmer_assignment', 'U') IS NOT NULL DROP TABLE dbo.agent_farmer_assignment;
IF OBJECT_ID('dbo.app_user', 'U') IS NOT NULL DROP TABLE dbo.app_user;

-- =========================================================================
-- 1. USERS TABLE (RBAC)
-- =========================================================================
CREATE TABLE app_user (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    Role NVARCHAR(50) NOT NULL CHECK (Role IN ('farmer', 'customer', 'agent', 'admin')),
    CreatedAt DATETIME2 DEFAULT GETDATE()
);

-- =========================================================================
-- 2. AGENT-FARMER ASSIGNMENT TABLE
-- =========================================================================
CREATE TABLE agent_farmer_assignment (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AgentId INT NOT NULL FOREIGN KEY REFERENCES app_user(Id) ON DELETE CASCADE,
    FarmerId INT NOT NULL FOREIGN KEY REFERENCES app_user(Id) ON DELETE NO ACTION,
    AssignedAt DATETIME2 DEFAULT GETDATE(),
    CONSTRAINT UQ_Agent_Farmer UNIQUE (AgentId, FarmerId)
);

-- =========================================================================
-- 3. SUPPLY STOCK TABLE (Seed Inventory)
-- =========================================================================
CREATE TABLE supply_stock (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FarmerId INT NOT NULL FOREIGN KEY REFERENCES app_user(Id) ON DELETE CASCADE,
    CropName NVARCHAR(100) NOT NULL,
    MaturityDays INT NOT NULL CHECK (MaturityDays > 0),
    CurrentStock DECIMAL(10, 2) NOT NULL CHECK (CurrentStock >= 0),
    Unit NVARCHAR(20) NOT NULL DEFAULT 'kg',
    UpdatedAt DATETIME2 DEFAULT GETDATE()
);

-- =========================================================================
-- 4. PLANTING BATCH TABLE (Crop Lifecycle)
-- =========================================================================
CREATE TABLE planting_batch (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FarmerId INT NOT NULL FOREIGN KEY REFERENCES app_user(Id) ON DELETE CASCADE,
    SupplyStockId INT NOT NULL FOREIGN KEY REFERENCES supply_stock(Id) ON DELETE NO ACTION,
    QuantityPlanted DECIMAL(10, 2) NOT NULL CHECK (QuantityPlanted > 0),
    PlantingDate DATE NOT NULL,
    PredictedHarvestDate DATE NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'active',
    ActualYield DECIMAL(10, 2) NULL,
    YieldUnit NVARCHAR(20) NULL,
    HarvestDate DATE NULL,
    CreatedAt DATETIME2 DEFAULT GETDATE()
);

-- =========================================================================
-- 5. HARVEST LISTING TABLE (Marketplace listing)
-- =========================================================================
CREATE TABLE harvest_listing (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FarmerId INT NOT NULL FOREIGN KEY REFERENCES app_user(Id) ON DELETE NO ACTION,
    BatchId INT NOT NULL FOREIGN KEY REFERENCES planting_batch(Id) ON DELETE CASCADE,
    CropName NVARCHAR(100) NOT NULL,
    AvailableQuantity DECIMAL(10, 2) NOT NULL CHECK (AvailableQuantity >= 0),
    Unit NVARCHAR(20) NOT NULL DEFAULT 'kg',
    PricePerUnit DECIMAL(10, 2) NOT NULL CHECK (PricePerUnit >= 0),
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETDATE()
);

-- =========================================================================
-- 6. ORDERS TABLE (Customer Checkout Header)
-- =========================================================================
CREATE TABLE orders (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId INT NOT NULL FOREIGN KEY REFERENCES app_user(Id) ON DELETE CASCADE,
    TotalAmount DECIMAL(10, 2) NOT NULL CHECK (TotalAmount >= 0),
    Status NVARCHAR(50) NOT NULL DEFAULT 'pending',
    CreatedAt DATETIME2 DEFAULT GETDATE()
);

-- =========================================================================
-- 7. ORDER ITEMS TABLE (Customer Checkout items)
-- =========================================================================
CREATE TABLE order_items (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    OrderId INT NOT NULL FOREIGN KEY REFERENCES orders(Id) ON DELETE CASCADE,
    HarvestListingId INT NOT NULL FOREIGN KEY REFERENCES harvest_listing(Id) ON DELETE NO ACTION,
    QuantityOrdered DECIMAL(10, 2) NOT NULL CHECK (QuantityOrdered > 0),
    PriceAtPurchase DECIMAL(10, 2) NOT NULL CHECK (PriceAtPurchase >= 0)
);

-- =========================================================================
-- Seed data
-- =========================================================================

-- 1. Insert Initial Users across all roles
INSERT INTO app_user (Email, PasswordHash, Role) VALUES 
('admin@agriflow.com', 'AdminPass123!', 'admin'),
('farmer1@agriflow.com', 'FarmerPass123!', 'farmer'),
('agent1@agriflow.com', 'AgentPass123!', 'agent'),
('customer1@agriflow.com', 'CustomerPass123!', 'customer');

-- Retrieve generated IDs using variables for relational data entry
DECLARE @FarmerId INT = (SELECT Id FROM app_user WHERE Email = 'farmer1@agriflow.com');
DECLARE @AgentId INT = (SELECT Id FROM app_user WHERE Email = 'agent1@agriflow.com');
DECLARE @CustomerId INT = (SELECT Id FROM app_user WHERE Email = 'customer1@agriflow.com');

-- 2. Assign Support Agent to the Farmer
INSERT INTO agent_farmer_assignment (AgentId, FarmerId) 
VALUES (@AgentId, @FarmerId);

-- 3. Insert Seed Stock Inventory for Farmer
INSERT INTO supply_stock (FarmerId, CropName, MaturityDays, CurrentStock, Unit) VALUES 
(@FarmerId, 'Maize', 90, 150.00, 'kg'),
(@FarmerId, 'Beans', 60, 45.00, 'kg');

DECLARE @StockId INT = (SELECT TOP 1 Id FROM supply_stock WHERE FarmerId = @FarmerId AND CropName = 'Maize');

-- 4. Insert an Active Planting Batch
INSERT INTO planting_batch (FarmerId, SupplyStockId, QuantityPlanted, PlantingDate, PredictedHarvestDate, Status) VALUES 
(@FarmerId, @StockId, 50.00, DATEADD(day, -30, GETDATE()), DATEADD(day, 60, GETDATE()), 'active');

DECLARE @BatchId INT = (SELECT TOP 1 Id FROM planting_batch WHERE FarmerId = @FarmerId);

-- 5. Insert a Marketplace Harvest Listing (Published crop ready for buyers)
INSERT INTO harvest_listing (FarmerId, BatchId, CropName, AvailableQuantity, Unit, PricePerUnit, IsActive) VALUES 
(@FarmerId, @BatchId, 'Maize', 120.00, 'kg', 2.50, 1);

DECLARE @ListingId INT = (SELECT TOP 1 Id FROM harvest_listing WHERE FarmerId = @FarmerId);

-- 6. Insert a Customer Order & Line Item
INSERT INTO orders (CustomerId, TotalAmount, Status) 
VALUES (@CustomerId, 25.00, 'pending');

DECLARE @OrderId INT = SCOPE_IDENTITY();

INSERT INTO order_items (OrderId, HarvestListingId, QuantityOrdered, PriceAtPurchase) 
VALUES (@OrderId, @ListingId, 10.00, 2.50);
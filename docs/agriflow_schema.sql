/* =====================================================================
   AgriFlow – database schema (SQL Server)
   Matches: AgriFlow System Overview document (sections 3, 9, 10, 11)
            and the Figma designs (screen IDs in comments).
   Setting: Rwanda – prices in RWF, farmers identified by National ID
            and land UPI, businesses by RDB company code.

   Run on an EMPTY database. Order matters (foreign keys).
   ===================================================================== */

/* ---------------------------------------------------------------------
   1. USERS
   One row per person who can sign in (or, for non-digital farmers,
   who is managed by an agent and may have no password).
   --------------------------------------------------------------------- */
CREATE TABLE app_user (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(150)  NOT NULL,
    Email           NVARCHAR(256)  NULL,
    Phone           NVARCHAR(20)   NULL,              -- e.g. 078 555 0198
    PasswordHash    NVARCHAR(500)  NULL,              -- NULL for agent-managed farmers with no login
    Role            NVARCHAR(10)   NOT NULL
                    CONSTRAINT CK_user_role CHECK (Role IN ('Farmer','Buyer','Agent','Admin')),
    IsActive        BIT            NOT NULL CONSTRAINT DF_user_active DEFAULT 1,   -- D3.2 suspend
    SuspendedReason NVARCHAR(300)  NULL,
    BuyerBusinessId INT            NULL,              -- set for Buyer users (FK added below)
    JobTitle        NVARCHAR(100)  NULL,              -- e.g. Procurement officer
    CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_user_created DEFAULT SYSUTCDATETIME(),
    LastLoginAt     DATETIME2      NULL
);
-- Email must be unique when present
CREATE UNIQUE INDEX UX_user_email ON app_user(Email) WHERE Email IS NOT NULL;


/* ---------------------------------------------------------------------
   2. BUYER BUSINESSES  (A3, A4, D2)
   --------------------------------------------------------------------- */
CREATE TABLE buyer_business (
    Id                 INT IDENTITY(1,1) PRIMARY KEY,
    Name               NVARCHAR(150) NOT NULL,
    BusinessType       NVARCHAR(30)  NOT NULL
                       CONSTRAINT CK_bb_type CHECK (BusinessType IN
                       ('Supermarket','Hotel','Restaurant','School','Hospital','Processor','Exporter','Other')),
    RdbCompanyCode     NVARCHAR(20)  NOT NULL CONSTRAINT UX_bb_rdb UNIQUE,
    Address            NVARCHAR(250) NOT NULL,
    City               NVARCHAR(80)  NOT NULL,
    Phone              NVARCHAR(20)  NULL,
    VerificationStatus NVARCHAR(10)  NOT NULL CONSTRAINT DF_bb_status DEFAULT 'Pending'
                       CONSTRAINT CK_bb_status CHECK (VerificationStatus IN ('Pending','Verified','Rejected')),
    VerifiedByUserId   INT           NULL REFERENCES app_user(Id),
    VerifiedAt         DATETIME2     NULL,
    RejectionReason    NVARCHAR(500) NULL,
    CreatedAt          DATETIME2     NOT NULL CONSTRAINT DF_bb_created DEFAULT SYSUTCDATETIME()
);

ALTER TABLE app_user
    ADD CONSTRAINT FK_user_business FOREIGN KEY (BuyerBusinessId) REFERENCES buyer_business(Id);


/* ---------------------------------------------------------------------
   3. FARMER PROFILES  (A2, A5, G2, D2b)
   One row per Farmer user. National ID is encrypted by the app
   (e.g. ASP.NET Core Data Protection); a keyed hash is stored so the
   database can still enforce "one National ID = one farmer" (BR-16).
   --------------------------------------------------------------------- */
CREATE TABLE farmer_profile (
    UserId              INT           PRIMARY KEY REFERENCES app_user(Id),
    FarmName            NVARCHAR(150) NOT NULL,
    NationalIdEncrypted VARBINARY(512) NOT NULL,             -- never shown to buyers (BR-15)
    NationalIdHash      VARBINARY(32)  NOT NULL CONSTRAINT UX_fp_nid UNIQUE,  -- HMAC-SHA256 of the 16 digits
    NationalIdMasked    NVARCHAR(30)  NOT NULL,              -- e.g. 1 1985 8 ••••••• 31 (admin view)
    LandUpi             NVARCHAR(30)  NOT NULL CONSTRAINT UX_fp_upi UNIQUE,   -- e.g. 3/03/10/04/1187
    Province            NVARCHAR(40)  NOT NULL,
    District            NVARCHAR(60)  NOT NULL,
    Sector              NVARCHAR(60)  NOT NULL,
    Cell                NVARCHAR(60)  NOT NULL,
    Village             NVARCHAR(60)  NOT NULL,
    MainCrops           NVARCHAR(200) NOT NULL,
    FarmSizeHa          DECIMAL(8,2)  NULL CONSTRAINT CK_fp_size CHECK (FarmSizeHa > 0),
    CooperativeName     NVARCHAR(150) NULL,
    CooperativeMemberNo NVARCHAR(50)  NULL,
    PreferredLanguage   NVARCHAR(20)  NOT NULL CONSTRAINT DF_fp_lang DEFAULT 'Kinyarwanda',
    VerificationStatus  NVARCHAR(12)  NOT NULL CONSTRAINT DF_fp_status DEFAULT 'Unverified'
                        CONSTRAINT CK_fp_status CHECK (VerificationStatus IN ('Unverified','Verified','Rejected')),
    VerifiedByUserId    INT           NULL REFERENCES app_user(Id),   -- admin, or the agent who registered them
    VerifiedAt          DATETIME2     NULL,
    RejectionReason     NVARCHAR(500) NULL,
    RegisteredByAgentId INT           NULL REFERENCES app_user(Id),   -- set when registered by an agent (auto-verified)
    CreatedAt           DATETIME2     NOT NULL CONSTRAINT DF_fp_created DEFAULT SYSUTCDATETIME()
);

-- Admin asks a local agent to visit a self-registered farm (D2b.2)
CREATE TABLE farm_visit_request (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    FarmerId          INT           NOT NULL REFERENCES farmer_profile(UserId),
    AgentId           INT           NOT NULL REFERENCES app_user(Id),
    RequestedByUserId INT           NOT NULL REFERENCES app_user(Id),
    VisitBy           DATE          NOT NULL,
    Note              NVARCHAR(500) NULL,
    Status            NVARCHAR(10)  NOT NULL CONSTRAINT DF_fvr_status DEFAULT 'Requested'
                      CONSTRAINT CK_fvr_status CHECK (Status IN ('Requested','Done','Cancelled')),
    CompletedAt       DATETIME2     NULL,
    CreatedAt         DATETIME2     NOT NULL CONSTRAINT DF_fvr_created DEFAULT SYSUTCDATETIME()
);


/* ---------------------------------------------------------------------
   4. AGENT ↔ FARMER ASSIGNMENTS  (G1, G2, G3, D4) – BR-4
   One agent per farmer at a time → FarmerId is UNIQUE.
   --------------------------------------------------------------------- */
CREATE TABLE agent_farmer_assignment (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    AgentId           INT           NOT NULL REFERENCES app_user(Id),
    FarmerId          INT           NOT NULL REFERENCES farmer_profile(UserId),
    AssignedByUserId  INT           NULL REFERENCES app_user(Id),   -- admin; NULL when agent registered the farmer
    AssignedAt        DATETIME2     NOT NULL CONSTRAINT DF_afa_at DEFAULT SYSUTCDATETIME(),
    ConsentRecordedAt DATETIME2     NULL,                           -- G2 consent checkbox
    ConsentPlace      NVARCHAR(150) NULL,
    CONSTRAINT UX_afa_farmer UNIQUE (FarmerId)
);


/* ---------------------------------------------------------------------
   5. CROPS (catalog)
   --------------------------------------------------------------------- */
CREATE TABLE crop (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(60) NOT NULL CONSTRAINT UX_crop_name UNIQUE,
    MaturityDays INT          NOT NULL CONSTRAINT CK_crop_days CHECK (MaturityDays > 0)
);


/* ---------------------------------------------------------------------
   6. SEED STOCK  (F2, F2.1–F2.3) – BR-1, BR-5
   Low stock (≤ 10) is calculated in the app, not stored.
   --------------------------------------------------------------------- */
CREATE TABLE seed_stock (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    FarmerId  INT           NOT NULL REFERENCES farmer_profile(UserId),
    CropId    INT           NOT NULL REFERENCES crop(Id),
    Variety   NVARCHAR(80)  NOT NULL,                 -- e.g. Roma
    Supplier  NVARCHAR(120) NULL,
    Quantity  DECIMAL(10,2) NOT NULL CONSTRAINT CK_ss_qty CHECK (Quantity >= 0),
    Unit      NVARCHAR(10)  NOT NULL CONSTRAINT DF_ss_unit DEFAULT 'kg',
    CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_ss_created DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2     NOT NULL CONSTRAINT DF_ss_updated DEFAULT SYSUTCDATETIME(),
    RowVer    ROWVERSION,                              -- concurrency: no overdrawing stock
    CONSTRAINT UX_ss_farmer_crop_variety UNIQUE (FarmerId, CropId, Variety)
);


/* ---------------------------------------------------------------------
   7. PLANTING BATCHES  (F3, F4, F3.1) – BR-1, BR-2, BR-3
   FK to seed_stock uses NO ACTION = SQL Server's "restrict":
   seed stock used by a batch cannot be deleted.
   --------------------------------------------------------------------- */
CREATE TABLE planting_batch (
    Id                   INT IDENTITY(1,1) PRIMARY KEY,
    BatchCode            NVARCHAR(20)  NOT NULL CONSTRAINT UX_pb_code UNIQUE,  -- e.g. B-024
    FarmerId             INT           NOT NULL REFERENCES farmer_profile(UserId),
    SeedStockId          INT           NOT NULL,
    QuantityPlanted      DECIMAL(10,2) NOT NULL CONSTRAINT CK_pb_qty CHECK (QuantityPlanted > 0),
    PlantingDate         DATE          NOT NULL,
    PredictedHarvestDate DATE          NOT NULL,      -- PlantingDate + crop.MaturityDays
    FieldName            NVARCHAR(60)  NULL,
    AreaHa               DECIMAL(8,2)  NULL,
    Status               NVARCHAR(10)  NOT NULL CONSTRAINT DF_pb_status DEFAULT 'Active'
                         CONSTRAINT CK_pb_status CHECK (Status IN ('Active','Harvested','Failed')),
    ActualYield          DECIMAL(12,2) NULL CONSTRAINT CK_pb_yield CHECK (ActualYield IS NULL OR ActualYield > 0),
    YieldUnit            NVARCHAR(20)  NULL,
    HarvestDate          DATE          NULL,
    RecordedByUserId     INT           NOT NULL REFERENCES app_user(Id),       -- farmer or agent
    CreatedAt            DATETIME2     NOT NULL CONSTRAINT DF_pb_created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_pb_seed FOREIGN KEY (SeedStockId) REFERENCES seed_stock(Id)
        ON DELETE NO ACTION,                          -- BR-1
    CONSTRAINT CK_pb_harvest CHECK (Status <> 'Harvested' OR (ActualYield IS NOT NULL AND HarvestDate IS NOT NULL))
);


/* ---------------------------------------------------------------------
   8. LISTINGS  (F4, F5, B2, B3, D5) – BR-7, BR-8, BR-9, BR-12, BR-14
   Created AT PLANTING as 'Upcoming' (estimated qty, can be reserved),
   becomes 'Available' at harvest (real qty).
   Unverified farms' listings are filtered out in queries (BR-14).
   --------------------------------------------------------------------- */
CREATE TABLE listing (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    ListingCode       NVARCHAR(20)  NOT NULL CONSTRAINT UX_l_code UNIQUE,     -- e.g. L-121
    BatchId           INT           NOT NULL CONSTRAINT UX_l_batch UNIQUE REFERENCES planting_batch(Id),
    FarmerId          INT           NOT NULL REFERENCES farmer_profile(UserId),
    CropId            INT           NOT NULL REFERENCES crop(Id),
    Grade             CHAR(1)       NOT NULL CONSTRAINT CK_l_grade CHECK (Grade IN ('A','B','C')),
    Unit              NVARCHAR(20)  NOT NULL,         -- crate, bag, tonne
    UnitSizeKg        DECIMAL(8,2)  NULL,             -- e.g. 25 for "crate (25 kg)"
    PricePerUnit      DECIMAL(12,2) NOT NULL CONSTRAINT CK_l_price CHECK (PricePerUnit > 0),   -- RWF
    MinOrderQty       DECIMAL(10,2) NOT NULL CONSTRAINT CK_l_min CHECK (MinOrderQty > 0),
    EstimatedQty      DECIMAL(12,2) NULL CONSTRAINT CK_l_est CHECK (EstimatedQty IS NULL OR EstimatedQty >= 0),
    ReservedQty       DECIMAL(12,2) NOT NULL CONSTRAINT DF_l_res DEFAULT 0 CONSTRAINT CK_l_res CHECK (ReservedQty >= 0),
    AvailableQty      DECIMAL(12,2) NOT NULL CONSTRAINT DF_l_avail DEFAULT 0 CONSTRAINT CK_l_avail CHECK (AvailableQty >= 0),
    Status            NVARCHAR(10)  NOT NULL CONSTRAINT DF_l_status DEFAULT 'Upcoming'
                      CONSTRAINT CK_l_status CHECK (Status IN ('Upcoming','Available','SoldOut','Hidden')),
    OpenForReservation BIT          NOT NULL CONSTRAINT DF_l_open DEFAULT 1,
    HiddenBy          NVARCHAR(10)  NULL CONSTRAINT CK_l_hiddenby CHECK (HiddenBy IN ('Farmer','Admin')),
    HiddenReason      NVARCHAR(500) NULL,
    Description       NVARCHAR(1000) NULL,
    CreatedAt         DATETIME2     NOT NULL CONSTRAINT DF_l_created DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2     NOT NULL CONSTRAINT DF_l_updated DEFAULT SYSUTCDATETIME(),
    RowVer            ROWVERSION                      -- BR-9: two buyers can't take the same last units
);
CREATE INDEX IX_l_status ON listing(Status, CropId);

-- Buyers report a listing; admins review it (D5)
CREATE TABLE listing_report (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    ListingId        INT           NOT NULL REFERENCES listing(Id),
    ReportedByUserId INT           NOT NULL REFERENCES app_user(Id),
    Reason           NVARCHAR(500) NOT NULL,
    Status           NVARCHAR(10)  NOT NULL CONSTRAINT DF_lr_status DEFAULT 'Open'
                     CONSTRAINT CK_lr_status CHECK (Status IN ('Open','Kept','Hidden')),
    ReviewedByUserId INT           NULL REFERENCES app_user(Id),
    ReviewNote       NVARCHAR(500) NULL,
    ReviewedAt       DATETIME2     NULL,
    CreatedAt        DATETIME2     NOT NULL CONSTRAINT DF_lr_created DEFAULT SYSUTCDATETIME()
);


/* ---------------------------------------------------------------------
   9. ORDERS  (B2.1, B3, B4, B5, F6) – BR-6, BR-10, BR-11
   One order line = one listing = one farmer.
   A reservation is an order_item with Status = 'Reserved';
   at harvest, reservations are settled oldest first (ReservedAt).
   --------------------------------------------------------------------- */
CREATE TABLE orders (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    OrderNumber       NVARCHAR(20)  NOT NULL CONSTRAINT UX_o_number UNIQUE,   -- e.g. AF-2048
    BuyerBusinessId   INT           NOT NULL REFERENCES buyer_business(Id),
    PlacedByUserId    INT           NOT NULL REFERENCES app_user(Id),
    DeliveryAddress   NVARCHAR(250) NOT NULL,
    DeliveryDate      DATE          NULL,
    Status            NVARCHAR(12)  NOT NULL CONSTRAINT DF_o_status DEFAULT 'Placed'
                      CONSTRAINT CK_o_status CHECK (Status IN
                      ('Reserved','Placed','Confirmed','Dispatched','Delivered','Invoiced','Paid','Cancelled')),
    TotalAmount       DECIMAL(14,2) NOT NULL CONSTRAINT DF_o_total DEFAULT 0,  -- RWF, recalculated from items
    CancelledByUserId INT           NULL REFERENCES app_user(Id),
    CancelReason      NVARCHAR(500) NULL,
    CreatedAt         DATETIME2     NOT NULL CONSTRAINT DF_o_created DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2     NOT NULL CONSTRAINT DF_o_updated DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_o_buyer ON orders(BuyerBusinessId, Status);

CREATE TABLE order_item (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    OrderId       INT           NOT NULL REFERENCES orders(Id),
    ListingId     INT           NOT NULL REFERENCES listing(Id),
    QtyRequested  DECIMAL(12,2) NOT NULL CONSTRAINT CK_oi_req CHECK (QtyRequested > 0),
    QtyConfirmed  DECIMAL(12,2) NULL,     -- farmer may confirm less (F6.1 partial)
    QtyDispatched DECIMAL(12,2) NULL,
    QtyAccepted   DECIMAL(12,2) NULL,     -- buyer delivery check (B5)
    QtyRejected   DECIMAL(12,2) NULL,
    RejectReason  NVARCHAR(300) NULL,
    UnitPrice     DECIMAL(12,2) NOT NULL CONSTRAINT CK_oi_price CHECK (UnitPrice > 0),  -- copied at order time
    Status        NVARCHAR(10)  NOT NULL
                  CONSTRAINT CK_oi_status CHECK (Status IN
                  ('Reserved','Placed','Confirmed','Dispatched','Delivered','Cancelled')),
    CancelType    NVARCHAR(10)  NULL CONSTRAINT CK_oi_ctype CHECK (CancelType IN ('Shortfall','Buyer','Farmer','Admin')),
    VehicleInfo   NVARCHAR(100) NULL,     -- F6.2
    ReservedAt    DATETIME2     NULL,     -- used to settle reservations oldest first
    ConfirmedAt   DATETIME2     NULL,
    DispatchedAt  DATETIME2     NULL,
    DeliveredAt   DATETIME2     NULL,
    CONSTRAINT CK_oi_confirm  CHECK (QtyConfirmed IS NULL OR (QtyConfirmed > 0 AND QtyConfirmed <= QtyRequested)),
    CONSTRAINT CK_oi_check    CHECK (QtyAccepted IS NULL OR QtyRejected IS NULL
                                     OR QtyAccepted + QtyRejected <= ISNULL(QtyDispatched, QtyAccepted + QtyRejected)),
    CONSTRAINT CK_oi_nonneg   CHECK (ISNULL(QtyAccepted,0) >= 0 AND ISNULL(QtyRejected,0) >= 0)
);
CREATE INDEX IX_oi_listing ON order_item(ListingId, Status, ReservedAt);


/* ---------------------------------------------------------------------
   10. INVOICES  (B5.1, B6) – billed on ACCEPTED quantity only
   --------------------------------------------------------------------- */
CREATE TABLE invoice (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceNumber    NVARCHAR(20)  NOT NULL CONSTRAINT UX_i_number UNIQUE,   -- e.g. INV-0421
    OrderId          INT           NOT NULL CONSTRAINT UX_i_order UNIQUE REFERENCES orders(Id),
    Amount           DECIMAL(14,2) NOT NULL CONSTRAINT CK_i_amount CHECK (Amount >= 0),  -- RWF
    Status           NVARCHAR(10)  NOT NULL CONSTRAINT DF_i_status DEFAULT 'Unpaid'
                     CONSTRAINT CK_i_status CHECK (Status IN ('Unpaid','Paid')),
    IssuedAt         DATETIME2     NOT NULL CONSTRAINT DF_i_issued DEFAULT SYSUTCDATETIME(),
    DueDate          DATE          NOT NULL,
    PaidAt           DATETIME2     NULL,
    PaymentMethod    NVARCHAR(40)  NULL,          -- 'Mobile money (simulated)'
    PaymentReference NVARCHAR(60)  NULL
);


/* ---------------------------------------------------------------------
   11. DISPUTES  (D6) – farm contests a buyer's rejected quantity
   --------------------------------------------------------------------- */
CREATE TABLE dispute (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    OrderItemId      INT            NOT NULL REFERENCES order_item(Id),
    RaisedByUserId   INT            NOT NULL REFERENCES app_user(Id),
    BuyerStatement   NVARCHAR(1000) NULL,
    FarmerStatement  NVARCHAR(1000) NULL,
    Status           NVARCHAR(10)   NOT NULL CONSTRAINT DF_d_status DEFAULT 'Open'
                     CONSTRAINT CK_d_status CHECK (Status IN ('Open','Resolved')),
    Outcome          NVARCHAR(12)   NULL CONSTRAINT CK_d_outcome CHECK (Outcome IN ('BuyerCheck','FarmSide','Split')),
    BilledQty        DECIMAL(12,2)  NULL,          -- final quantity invoiced after resolution
    ResolutionNote   NVARCHAR(1000) NULL,
    ResolvedByUserId INT            NULL REFERENCES app_user(Id),
    ResolvedAt       DATETIME2      NULL,
    CreatedAt        DATETIME2      NOT NULL CONSTRAINT DF_d_created DEFAULT SYSUTCDATETIME()
);


/* ---------------------------------------------------------------------
   12. AUDIT LOG  (D7, D1 recent activity) – read-only from the UI
   --------------------------------------------------------------------- */
CREATE TABLE audit_log (
    Id                 BIGINT IDENTITY(1,1) PRIMARY KEY,
    At                 DATETIME2     NOT NULL CONSTRAINT DF_a_at DEFAULT SYSUTCDATETIME(),
    ActorUserId        INT           NOT NULL REFERENCES app_user(Id),
    OnBehalfOfFarmerId INT           NULL REFERENCES farmer_profile(UserId),   -- agent proxy actions
    Entity             NVARCHAR(40)  NOT NULL,       -- e.g. SeedStock, Order, Listing
    EntityId           NVARCHAR(40)  NULL,
    Action             NVARCHAR(60)  NOT NULL,       -- e.g. Updated seed stock
    Summary            NVARCHAR(500) NULL,           -- e.g. Tomato · Roma: 12 kg → 8 kg
    Result             NVARCHAR(10)  NOT NULL CONSTRAINT DF_a_result DEFAULT 'Success'
                       CONSTRAINT CK_a_result CHECK (Result IN ('Success','Blocked'))
);
CREATE INDEX IX_a_at ON audit_log(At DESC);
CREATE INDEX IX_a_actor ON audit_log(ActorUserId, At DESC);


/* ---------------------------------------------------------------------
   13. STARTER DATA – crop catalog
   (Users, farms, listings and orders: see AGRI-11 seed data.)
   --------------------------------------------------------------------- */
INSERT INTO crop (Name, MaturityDays) VALUES
 (N'Tomato', 90), (N'Lettuce', 60), (N'Maize', 90), (N'Sweet corn', 90),
 (N'Carrot', 70), (N'Cabbage', 70), (N'Beans', 60), (N'Okra', 60),
 (N'Red onion', 110), (N'Irish potato', 100);

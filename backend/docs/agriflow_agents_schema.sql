 /* Add agent. Added the actual pwd hash here as it is just a test user. the unhashed pwd is agriflowagent1 */
INSERT INTO app_user (FullName, Email, Phone, PasswordHash, Role, IsActive, JobTitle)
    VALUES (
        N'Grace Mukamana',
        N'grace.agent@yourdomain.com',
        N'078 555 0100',
        N'$2a$11$fZvO3jeGIAAqEFHtYyIBvOyBvOxXfvWBRp0XrwufQUScSeNbIG37O',
        'Agent',
        1,
        N'Field agent · Nyagatare'
    );


/* =====================================================================
   AgriFlow – Seed Data for Agent Overview (Screen G1)
   Prerequisite: Schema/init.sql executed + Grace Mukamana added to app_user.
   ===================================================================== */

/* ---------------------------------------------------------------------
1. ADMIN USER & VERIFIED BUYER BUSINESS
   Admin is needed to assign self-registered farmers (AssignedByUserId)
   and schedule farm visits (RequestedByUserId).
   --------------------------------------------------------------------- */
INSERT INTO app_user (FullName, Email, Phone, PasswordHash, Role, IsActive, JobTitle)
VALUES (
    N'Samuel Niyonzima',
    N'admin@agriflow.example',
    N'0785550001',
    N'$2a$11$fZvO3jeGIAAqEFHtYyIBvOyBvOxXfvWBRp0XrwufQUScSeNbIG37O',
    'Admin',
    1,
    N'System Administrator'
);

-- Verified buyer business so we can attach pending orders to farmers
INSERT INTO buyer_business (Name, BusinessType, RdbCompanyCode, Address, City, Phone, VerificationStatus, VerifiedByUserId, VerifiedAt)
VALUES (
    N'FreshMart Supermarkets',
    'Supermarket',
    N'108452367',
    N'Kimironko, Kigali',
    N'Kigali',
    N'078 555 0142',
    'Verified',
    (SELECT Id FROM app_user WHERE Email = N'admin@agriflow.example'),
    '2026-11-01 09:00:00'
);

INSERT INTO app_user (FullName, Email, Phone, PasswordHash, Role, IsActive, BuyerBusinessId, JobTitle)
VALUES (
    N'Aline Uwase',
    N'aline.uwase@freshmart.example',
    N'078 555 0142',
    N'$2a$11$fZvO3jeGIAAqEFHtYyIBvOyBvOxXfvWBRp0XrwufQUScSeNbIG37O',
    'Buyer',
    1,
    (SELECT Id FROM buyer_business WHERE RdbCompanyCode = N'108452367'),
    N'Procurement officer'
);


/* ---------------------------------------------------------------------
2. FIVE FARMER USERS (1 Self-Managed + 4 Non-Digital)
   - Jean Bosco has Email + PasswordHash -> UI shows "Self-manages too".
   - Claudine, Patrick, Josiane, Emmanuel have NULL PasswordHash -> "Non-digital".
   - Claudine & Emmanuel have NULL Phone -> UI renders "No phone".
   --------------------------------------------------------------------- */
INSERT INTO app_user (FullName, Email, Phone, PasswordHash, Role, IsActive)
VALUES
    (N'Jean Bosco Habimana', N'jeanbosco@greenhills.example', N'078 555 0198', N'$2a$11$fZvO3jeGIAAqEFHtYyIBvOyBvOxXfvWBRp0XrwufQUScSeNbIG37O', 'Farmer', 1),
    (N'Claudine Uwimana',    NULL,                           NULL,            NULL, 'Farmer', 1),
    (N'Patrick Mugisha',     NULL,                           N'072 555 0331', NULL, 'Farmer', 1),
    (N'Josiane Mukeshimana', NULL,                           N'073 555 0712', NULL, 'Farmer', 1),
    (N'Emmanuel Nkurunziza', NULL,                           NULL,            NULL, 'Farmer', 1);


/* ---------------------------------------------------------------------
3. FARMER PROFILES & AGENT ASSIGNMENTS (BR-4)
   - Jean Bosco: self-registered (RegisteredByAgentId = NULL), assigned by Admin.
   - Other 4: onboarded by Grace (RegisteredByAgentId = Grace), auto-verified.
   --------------------------------------------------------------------- */
INSERT INTO farmer_profile (
    UserId, FarmName, NationalIdEncrypted, NationalIdHash, NationalIdMasked,
    LandUpi, Province, District, Sector, Cell, Village, MainCrops, FarmSizeHa,
    VerificationStatus, VerifiedByUserId, VerifiedAt, RegisteredByAgentId
)
VALUES
    (
        (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana'),
        N'Green Hills Farm',
        0x0101010101010101,
        0x1111111111111111111111111111111111111111111111111111111111111111,
        N'1 1985 8 ••••••• 31',
        N'3/03/10/04/1187',
        N'Eastern Province', N'Nyagatare', N'Rukomo', N'Gahurura', N'Ryabega',
        N'Tomato, Lettuce, Sweet corn, Carrot', 3.50,
        'Verified',
        (SELECT Id FROM app_user WHERE Email = N'admin@agriflow.example'),
        '2026-11-02 10:00:00',
        NULL
    ),
    (
        (SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana'),
        N'Sunrise Farm',
        0x0202020202020202,
        0x2222222222222222222222222222222222222222222222222222222222222222,
        N'1 1988 7 ••••••• 14',
        N'3/03/10/04/1188',
        N'Eastern Province', N'Nyagatare', N'Rukomo', N'Nyakagarama', N'Kabeza',
        N'Tomato, Beans', 2.00,
        'Verified',
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'),
        '2026-11-03 11:00:00',
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')
    ),
    (
        (SELECT Id FROM app_user WHERE FullName = N'Patrick Mugisha'),
        N'Riverbend Farm',
        0x0303030303030303,
        0x3333333333333333333333333333333333333333333333333333333333333333,
        N'1 1979 8 ••••••• 09',
        N'3/03/10/04/1189',
        N'Eastern Province', N'Nyagatare', N'Nyagatare', N'Barija', N'Nsheke',
        N'Cabbage, Maize', 1.80,
        'Verified',
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'),
        '2026-11-04 14:00:00',
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')
    ),
    (
        (SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana'),
        N'Hillside Farm',
        0x0404040404040404,
        0x4444444444444444444444444444444444444444444444444444444444444444,
        N'1 1991 7 ••••••• 52',
        N'3/03/10/05/2041',
        N'Eastern Province', N'Nyagatare', N'Gatunda', N'Nyamikamba', N'Rugari',
        N'Carrot, Red onion', 2.40,
        'Verified',
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'),
        '2026-11-05 09:30:00',
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')
    ),
    (
        (SELECT Id FROM app_user WHERE FullName = N'Emmanuel Nkurunziza'),
        N'Nkurunziza Family Farm',
        0x0505050505050505,
        0x5555555555555555555555555555555555555555555555555555555555555555,
        N'1 1982 8 ••••••• 77',
        N'3/03/10/04/1195',
        N'Eastern Province', N'Nyagatare', N'Rukomo', N'Rurenge', N'Gahurura',
        N'Tomato, Irish potato', 4.10,
        'Verified',
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'),
        '2026-11-18 15:00:00',
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')
    );

-- Link all 5 farmers to Grace Mukamana
INSERT INTO agent_farmer_assignment (AgentId, FarmerId, AssignedByUserId, ConsentRecordedAt, ConsentPlace)
VALUES
    (
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'),
        (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana'),
        (SELECT Id FROM app_user WHERE Email = N'admin@agriflow.example'),
        '2026-11-02 10:05:00',
        N'Rukomo Sector Office'
    ),
    (
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'),
        (SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana'),
        NULL,
        '2026-11-03 11:00:00',
        N'Sunrise Farm, Rukomo'
    ),
    (
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'),
        (SELECT Id FROM app_user WHERE FullName = N'Patrick Mugisha'),
        NULL,
        '2026-11-04 14:00:00',
        N'Riverbend Farm, Nyagatare'
    ),
    (
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'),
        (SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana'),
        NULL,
        '2026-11-05 09:30:00',
        N'Hillside Farm, Gatunda'
    ),
    (
        (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'),
        (SELECT Id FROM app_user WHERE FullName = N'Emmanuel Nkurunziza'),
        NULL,
        '2026-11-18 15:00:00',
        N'Nkurunziza Family Farm, Rukomo'
    );


/* ---------------------------------------------------------------------
4. SCHEDULED FARM VISIT (Hero Banner "Next visit" Card)
   VisitBy = '2026-11-28' (tomorrow relative to 27 Nov 2026).
   --------------------------------------------------------------------- */
INSERT INTO farm_visit_request (FarmerId, AgentId, RequestedByUserId, VisitBy, Note, Status)
VALUES (
    (SELECT Id FROM app_user WHERE FullName = N'Emmanuel Nkurunziza'),
    (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'),
    (SELECT Id FROM app_user WHERE Email = N'admin@agriflow.example'),
    '2026-11-28',
    N'Record his tomato planting and check his low seed stock.',
    'Requested'
);


/* ---------------------------------------------------------------------
5. SEED STOCK (BR-5: Quantity <= 10 triggers "Low seed" badge & alerts)
   - Jean Bosco: 2 low items (8 kg Tomato, 5 kg Lettuce) + 2 healthy items.
   - Patrick Mugisha: 1 low item (4 kg Cabbage) -> triggers attention alert.
   - Claudine, Josiane, Emmanuel: healthy stock (> 10 kg) -> "OK" badge.
   --------------------------------------------------------------------- */
INSERT INTO seed_stock (FarmerId, CropId, Variety, Supplier, Quantity, Unit)
VALUES
    -- Jean Bosco (2 low, 2 ok)
    ((SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana'), (SELECT Id FROM crop WHERE Name = N'Tomato'),     N'Roma',   N'Agro-Input Ltd',  8.00, N'kg'),
    ((SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana'), (SELECT Id FROM crop WHERE Name = N'Lettuce'),    N'Green',  N'Hinga Inputs',    5.00, N'kg'),
    ((SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana'), (SELECT Id FROM crop WHERE Name = N'Sweet corn'), N'Golden', N'Agro-Input Ltd', 42.00, N'kg'),
    ((SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana'), (SELECT Id FROM crop WHERE Name = N'Carrot'),     N'Nantes', N'Hinga Inputs',   30.00, N'kg'),

    -- Claudine (OK)
    ((SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana'),    (SELECT Id FROM crop WHERE Name = N'Tomato'),     N'Roma',   N'Agro-Input Ltd', 25.00, N'kg'),
    ((SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana'),    (SELECT Id FROM crop WHERE Name = N'Beans'),      N'Bush',   N'Rwanda Seeds',   40.00, N'kg'),

    -- Patrick (1 low: 4 kg Cabbage)
    ((SELECT Id FROM app_user WHERE FullName = N'Patrick Mugisha'),     (SELECT Id FROM crop WHERE Name = N'Cabbage'),    N'Gloria', N'Hinga Inputs',    4.00, N'kg'),
    ((SELECT Id FROM app_user WHERE FullName = N'Patrick Mugisha'),     (SELECT Id FROM crop WHERE Name = N'Maize'),      N'ZM607',  N'Rwanda Seeds',   50.00, N'kg'),

    -- Josiane (OK)
    ((SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana'), (SELECT Id FROM crop WHERE Name = N'Carrot'),     N'Nantes', N'Hinga Inputs',   22.00, N'kg'),
    ((SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana'), (SELECT Id FROM crop WHERE Name = N'Red onion'),  N'Red Creole', N'Agro-Input Ltd', 18.00, N'kg'),

    -- Emmanuel (OK)
    ((SELECT Id FROM app_user WHERE FullName = N'Emmanuel Nkurunziza'), (SELECT Id FROM crop WHERE Name = N'Irish potato'), N'Kinigi', N'RAB Seed Unit', 120.00, N'kg');


/* ---------------------------------------------------------------------
6. ACTIVE PLANTING BATCHES (14 total across the 5 farmers; 4 due within 14 days)
   Distribution: Jean Bosco (3), Claudine (4), Patrick (2), Josiane (3), Emmanuel (2).
   Harvests due (PredictedHarvestDate <= '2026-12-11'): B-104, B-105, B-106, B-110.
   --------------------------------------------------------------------- */
INSERT INTO planting_batch (
    BatchCode, FarmerId, SeedStockId, QuantityPlanted, PlantingDate, PredictedHarvestDate,
    FieldName, AreaHa, Status, RecordedByUserId
)
VALUES
    -- Jean Bosco (3 active batches, future harvest)
    (N'B-101', (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Roma' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana')),
     4.00, '2026-10-01', '2026-12-30', N'Plot A', 1.00, 'Active', (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana')),
    (N'B-102', (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Green' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana')),
     3.00, '2026-11-01', '2026-12-31', N'Plot B', 0.80, 'Active', (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana')),
    (N'B-103', (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Nantes' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana')),
     6.00, '2026-10-20', '2026-12-29', N'Plot C', 1.20, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),

    -- Claudine (4 active batches; 2 due/overdue -> triggers "2 harvests to record")
    (N'B-104', (SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Roma' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana')),
     5.00, '2026-08-20', '2026-11-18', N'North Terrace', 0.70, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),
    (N'B-105', (SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Bush' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana')),
     10.00, '2026-10-05', '2026-12-04', N'South Terrace', 0.60, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),
    (N'B-106', (SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Roma' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana')),
     4.00, '2026-09-10', '2026-12-09', N'East Strip', 0.40, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),
    (N'B-107', (SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Bush' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Claudine Uwimana')),
     8.00, '2026-11-05', '2027-01-04', N'West Strip', 0.30, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),

    -- Patrick (2 active batches)
    (N'B-108', (SELECT Id FROM app_user WHERE FullName = N'Patrick Mugisha'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Gloria' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Patrick Mugisha')),
     3.00, '2026-10-15', '2026-12-24', N'River Field 1', 0.90, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),
    (N'B-109', (SELECT Id FROM app_user WHERE FullName = N'Patrick Mugisha'),
     (SELECT Id FROM seed_stock WHERE Variety = N'ZM607' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Patrick Mugisha')),
     12.00, '2026-10-01', '2026-12-30', N'River Field 2', 0.90, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),

    -- Josiane (3 active batches; 1 due within 14 days)
    (N'B-110', (SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Nantes' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana')),
     5.00, '2026-09-25', '2026-12-04', N'Slope 1', 0.80, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),
    (N'B-111', (SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Nantes' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana')),
     5.00, '2026-10-25', '2027-01-03', N'Slope 2', 0.80, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),
    (N'B-112', (SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Red Creole' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana')),
     4.00, '2026-09-20', '2027-01-08', N'Valley Plot', 0.80, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),

    -- Emmanuel (2 active batches)
    (N'B-113', (SELECT Id FROM app_user WHERE FullName = N'Emmanuel Nkurunziza'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Kinigi' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Emmanuel Nkurunziza')),
     40.00, '2026-10-01', '2027-01-09', N'Main Block A', 2.00, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com')),
    (N'B-114', (SELECT Id FROM app_user WHERE FullName = N'Emmanuel Nkurunziza'),
     (SELECT Id FROM seed_stock WHERE Variety = N'Kinigi' AND FarmerId = (SELECT Id FROM app_user WHERE FullName = N'Emmanuel Nkurunziza')),
     35.00, '2026-10-18', '2027-01-26', N'Main Block B', 2.10, 'Active', (SELECT Id FROM app_user WHERE Email = N'grace.agent@yourdomain.com'));


/* ---------------------------------------------------------------------
7. LISTINGS & PENDING ORDERS (Populates "Orders to handle" & Attention Feed)
   - Jean Bosco: 3 orders to confirm ("3 to confirm").
   - Josiane: 1 order of 20 crates of Carrot Nantes ("1 to confirm").
   --------------------------------------------------------------------- */
INSERT INTO listing (
    ListingCode, BatchId, FarmerId, CropId, Grade, Unit, UnitSizeKg,
    PricePerUnit, MinOrderQty, EstimatedQty, AvailableQty, Status
)
VALUES
    (N'L-201', (SELECT Id FROM planting_batch WHERE BatchCode = N'B-101'),
     (SELECT Id FROM app_user WHERE FullName = N'Jean Bosco Habimana'),
     (SELECT Id FROM crop WHERE Name = N'Tomato'), 'A', N'crate', 25.00,
     18000.00, 5.00, 120.00, 120.00, 'Available'),

    (N'L-202', (SELECT Id FROM planting_batch WHERE BatchCode = N'B-110'),
     (SELECT Id FROM app_user WHERE FullName = N'Josiane Mukeshimana'),
     (SELECT Id FROM crop WHERE Name = N'Carrot'), 'A', N'crate', 25.00,
     15000.00, 5.00, 90.00, 90.00, 'Available');

INSERT INTO orders (OrderNumber, BuyerBusinessId, PlacedByUserId, DeliveryAddress, DeliveryDate, Status, TotalAmount)
VALUES
    (N'AF-3001', (SELECT Id FROM buyer_business WHERE RdbCompanyCode = N'108452367'), (SELECT Id FROM app_user WHERE Email = N'aline.uwase@freshmart.example'), N'Kimironko, Kigali', '2026-11-30', 'Placed', 360000.00),
    (N'AF-3002', (SELECT Id FROM buyer_business WHERE RdbCompanyCode = N'108452367'), (SELECT Id FROM app_user WHERE Email = N'aline.uwase@freshmart.example'), N'Kimironko, Kigali', '2026-12-01', 'Placed', 180000.00),
    (N'AF-3003', (SELECT Id FROM buyer_business WHERE RdbCompanyCode = N'108452367'), (SELECT Id FROM app_user WHERE Email = N'aline.uwase@freshmart.example'), N'Kimironko, Kigali', '2026-12-02', 'Placed', 270000.00),
    (N'AF-3004', (SELECT Id FROM buyer_business WHERE RdbCompanyCode = N'108452367'), (SELECT Id FROM app_user WHERE Email = N'aline.uwase@freshmart.example'), N'Kimironko, Kigali', '2026-12-03', 'Placed', 300000.00);

INSERT INTO order_item (OrderId, ListingId, QtyRequested, UnitPrice, Status)
VALUES
    -- 3 orders for Jean Bosco
    ((SELECT Id FROM orders WHERE OrderNumber = N'AF-3001'), (SELECT Id FROM listing WHERE ListingCode = N'L-201'), 20.00, 18000.00, 'Placed'),
    ((SELECT Id FROM orders WHERE OrderNumber = N'AF-3002'), (SELECT Id FROM listing WHERE ListingCode = N'L-201'), 10.00, 18000.00, 'Placed'),
    ((SELECT Id FROM orders WHERE OrderNumber = N'AF-3003'), (SELECT Id FROM listing WHERE ListingCode = N'L-201'), 15.00, 18000.00, 'Placed'),
    -- 1 order (20 crates of Carrot Nantes) for Josiane
    ((SELECT Id FROM orders WHERE OrderNumber = N'AF-3004'), (SELECT Id FROM listing WHERE ListingCode = N'L-202'), 20.00, 15000.00, 'Placed');



    /* assigns a unverfied farm by admin to agent to visit and verify, please change grace email to the  an actual email assigned to her in the db and an actual admin email*/
    INSERT INTO farm_visit_request (FarmerId, AgentId, RequestedByUserId, VisitBy, Note, Status)
VALUES (
    1,
    (SELECT Id FROM app_user WHERE Email = ''),
    (SELECT Id FROM app_user WHERE Email = 'admin@agriflow.example'),
    '2026-11-30',
    'Verify Land UPI parcel, national ID, and active crops on site for Mown Farms.',
    'Requested'
);
/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : 05_SeedData.sql
  Purpose : Dummy dataset for API testing and the milestone demonstration.
            (Chapter 2.1 - "the current system uses a dummy dataset")
  Author  : Ladhurshan Sivasathyamoorthy
  ------------------------------------------------------------------------------
  CONTENTS
      16 users     (2 admins, 9 sellers, 5 buyers)
      31 property listings, deliberately spread across all three risk levels
      41 property images, including 5 planted duplicate-image pairs
       7 planted duplicate deed-reference pairs
       6 fraud awareness podcasts (English / Sinhala / Tamil)
      plus suspicious reports, saved properties and admin actions.

  IMPORTANT
      Passwords below are BCrypt hashes of the literal string  Test@123
      They exist ONLY for local testing and must never be used in production.
==============================================================================*/

USE LandGuardDB;
GO

SET NOCOUNT ON;
GO

PRINT '>> Seeding LandGuardDB...';
GO

/*==============================================================================
  1. DISTRICT PRICE BENCHMARKS   (input to fraud CHECK 1 - price anomaly)
     Indicative market rate per perch in LKR.
==============================================================================*/
INSERT INTO dbo.PriceBenchmark (District, MarketPricePerPerch) VALUES
 (N'Colombo',        3500000.00),
 (N'Gampaha',         850000.00),
 (N'Kandy',           700000.00),
 (N'Galle',           600000.00),
 (N'Matara',          450000.00),
 (N'Kurunegala',      350000.00),
 (N'Jaffna',          400000.00),
 (N'Anuradhapura',    250000.00),
 (N'Nuwara Eliya',    500000.00),
 (N'Trincomalee',     300000.00),
 (N'Batticaloa',      200000.00),
 (N'Ratnapura',       220000.00);
GO


/*==============================================================================
  2. USERS
==============================================================================*/
SET IDENTITY_INSERT dbo.Users ON;

INSERT INTO dbo.Users
    (UserID, Name, Email, PasswordHash, NIC, Phone, Role, IsActive, NICVerified, CreatedAt)
VALUES
 -- ADMINS ---------------------------------------------------------------------
 ( 1, N'Abilasha Sujeeva Rajamohan', N'abilasha@landguard.lk',
      N'$2b$11$/TgQhjoq1giA98J.EaL2Be8wX46gu.yJAUMghDgWr8KHNzMEHMixC',
      '927654321V', '0771234567', 'Admin',  1, 1, '2026-06-01T09:00:00'),
 ( 2, N'Ladhurshan Sivasathyamoorthy', N'ladhurshan@landguard.lk',
      N'$2b$11$H4HUwZytR7B8SPHQy7HuLuNW2rdScaH34lTFTH2CkdFdIhXDgVEEq',
      '199845612378', '0762345678', 'Admin', 1, 1, '2026-06-01T09:05:00'),

 -- SELLERS --------------------------------------------------------------------
 ( 3, N'Chathura Perera',        N'chathura@example.com',
      N'$2b$11$NxAREL4IWbFkVKppzz0F.eumn/KF1S/YyOdH9gWrnEywr7J9YeVcS',
      '883456789V', '0712345601', 'Seller', 1, 1, '2026-06-03T10:12:00'),
 ( 4, N'Nimal Fernando',         N'nimal.fernando@example.com',
      N'$2b$11$utBqQEH2WKxnODK9M7CqiuGzu9xQL4J7bNLkCotNgMB/bBSOJwOdi',
      '901234567V', '0712345602', 'Seller', 1, 1, '2026-06-04T11:30:00'),
 ( 5, N'Kumari Silva',           N'kumari.silva@example.com',
      N'$2b$11$su8Li3jiV6gqQyfqbMdqQetBapUDZhppzMeqJf3.0UD/eZPLq3fke',
      '925678901X', '0712345603', 'Seller', 1, 1, '2026-06-05T14:05:00'),
 ( 6, N'Rajitha Bandara',        N'rajitha.bandara@example.com',
      N'$2b$11$QO9s.JrbtXMg4o4C.BXW0efUuHTG1D9.eSLcvDs7hedrs2W3dwgra',
      '946789012V', '0712345604', 'Seller', 1, 0, '2026-06-07T08:45:00'),  -- NIC NOT verified
 ( 7, N'Suresh Kumar',           N'suresh.kumar@example.com',
      N'$2b$11$LKjA0YcykiGQ3RvqKPwVsOOFmwD25HmCEt.9YXsHGU0FAxmxZhtoi',
      '199612345670', '0712345605', 'Seller', 1, 1, '2026-06-08T16:20:00'),
 ( 8, N'Malith Jayawardena',     N'malith.j@example.com',
      N'$2b$11$eR8jdIjtam4/jfjz0M8c7epfH2RdZBIqX/353kx57vDyq3hYctyf6',
      '877890123V', '0712345606', 'Seller', 1, 1, '2026-06-09T09:10:00'),  -- repeat offender
 ( 9, N'Priyantha Alwis',        N'priyantha.alwis@example.com',
      N'$2b$11$NfeZIe9TeOMDf272.iifEuOawByCGfsJaKfMKzccO1MMyQFPI1Qxa',
      '958901234V', NULL,        'Seller', 1, 0, '2026-06-10T22:40:00'),   -- unverified + no phone
 (10, N'Ayesha Rahman',          N'ayesha.rahman@example.com',
      N'$2b$11$3oUBI5PjvK/JZ1p.EpHMO.mgw0YBk.Z..7BpbZNWiGyC/nuE5XTXC',
      '939012345V', '0712345608', 'Seller', 1, 1, '2026-06-11T13:00:00'),
 (11, N'Tharindu Wickramasinghe',N'tharindu.w@example.com',
      N'$2b$11$oNOJ6rsFolrXfsKeLA84puqJiYH5BoV5JyeIaAWf8ptGQZ4I8drU6',
      '200012345678', '0712345609', 'Seller', 1, 1, '2026-06-12T17:25:00'),

 -- BUYERS ---------------------------------------------------------------------
 (12, N'Sanduni Rathnayake',     N'sanduni.r@example.com',
      N'$2b$11$6JLUyYrSsNwr3cgcPK9OxO99m34w05ZvUIYikhzb6KTPz5KUboKKy',
      '968123456V', '0759876501', 'Buyer', 1, 0, '2026-06-15T10:00:00'),
 (13, N'Ashan Perera',           N'ashan.perera@example.com',
      N'$2b$11$zeIKGkx8WcSiKsZEIjqFwuSh1E9RTPDebEYD8Z24id0A8.T3gngVC',
      '972345678V', '0759876502', 'Buyer', 1, 0, '2026-06-16T12:30:00'),
 (14, N'Ashokkumar Kisa Priyadarshani', N'kisa@example.com',
      N'$2b$11$TJTNqHo84aasZ.zNU9WcG.oWEtLlz3i2MUCS.sLcmw.HPKnefwbZe',
      '983456789V', '0759876503', 'Buyer', 1, 0, '2026-06-17T09:15:00'),
 (15, N'Thiwanshika Isuruni',    N'thiwanshika@example.com',
      N'$2b$11$NrJcSqGFmoagUOGCMfOSle.Mg4aiVHFzKp5JmuQYoOZtzcn09BAeu',
      '994567890V', '0759876504', 'Buyer', 1, 0, '2026-06-18T11:45:00'),
 (16, N'S. S. Nimsari Lasanya Jayasekara', N'nimsari@example.com',
      N'$2b$11$QqIfvjWoDRID5RIaBgvoTOHy2uTt1GTJ8L0WUcnOz9DBfMH0pjVdi',
      '200145678901', '0759876505', 'Buyer', 1, 0, '2026-06-19T15:05:00');

SET IDENTITY_INSERT dbo.Users OFF;
GO


/*==============================================================================
  3. PROPERTY LISTINGS
  ------------------------------------------------------------------------------
  Every row is annotated with the rules it is designed to trigger, so the
  expected risk score can be verified after the engine runs (see 06_TestQueries).

  Planted deed-reference duplicates : (10,11) (12,13) (15,16) (21,22)
                                      (25,26) (28,29) (30,31)
  Planted invalid locations         : 5, 14, 21, 25, 30
  Planted price anomalies           : 4, 14, 17, 18, 19, 21, 25, 28, 30
  Rejected history listings         : 18, 19 (Malith)  23, 24 (Priyantha)
==============================================================================*/
SET IDENTITY_INSERT dbo.Property ON;

INSERT INTO dbo.Property
    (PropertyID, SellerID, Title, Description, Location, District,
     Latitude, Longitude, Size, Price, DeedReference, Status, UploadDate)
VALUES
-- P1  CLEAN reference listing -> expected 0 / Low ------------------------------
( 1, 3, N'20 Perch Residential Land in Nugegoda',
   N'Flat rectangular residential block on a 20 foot wide tarred road, walking distance to Nugegoda town, water and electricity already connected. Clear deed available for inspection.',
   N'Nugegoda, Colombo', N'Colombo', 6.872500, 79.889200, 20, 70000000.00, 'COL/2023/DEED/1187', 'Pending', '2026-07-01T09:00:00'),

-- P2  CLEAN -> 0 / Low ---------------------------------------------------------
( 2, 4, N'40 Perch Land with Boundary Wall - Ja-Ela',
   N'Level land with a completed boundary wall and steel gate, located 1.2 km from the Colombo-Negombo main road. Suitable for a house or a small commercial project.',
   N'Ja-Ela, Gampaha', N'Gampaha', 7.074500, 79.892300, 40, 33000000.00, 'GAM/2022/DEED/4410', 'Pending', '2026-07-01T10:30:00'),

-- P3  CLEAN -> 0 / Low ---------------------------------------------------------
( 3, 5, N'15 Perch Hill View Land near Kandy Town',
   N'Elevated plot with an uninterrupted view of the Hantana range, 4 km from Kandy town centre. Motorable access road, mains water available at the boundary.',
   N'Peradeniya, Kandy', N'Kandy', 7.290600, 80.633700, 15, 9750000.00, 'KAN/2023/DEED/8891', 'Pending', '2026-07-02T08:15:00'),

-- P4  PRICE ANOMALY only -> 15 / Low -------------------------------------------
( 4, 3, N'25 Perch Land Galle - Urgent Sale',
   N'Owner migrating overseas and needs a fast settlement, therefore priced well below the current market rate for the area. Clear deed, no disputes, immediate transfer possible.',
   N'Unawatuna, Galle', N'Galle', 6.010200, 80.249700, 25, 8400000.00, 'GAL/2021/DEED/2277', 'Pending', '2026-07-02T11:40:00'),

-- P5  LOCATION INVALID (no coordinates) -> 10 / Low ----------------------------
( 5, 4, N'30 Perch Coconut Land Kurunegala',
   N'Mature coconut land with approximately 60 bearing trees, bordered by a gravel access road. Ideal for a small plantation investment or a future housing subdivision.',
   N'Wariyapola area', N'Kurunegala', NULL, NULL, 30, 10200000.00, 'KUR/2020/DEED/6612', 'Pending', '2026-07-03T09:20:00'),

-- P6  MISSING INFO (description too short) -> 8 / Low --------------------------
( 6, 5, N'18 Perch Land Matara',
   N'Good land for sale.',
   N'Kamburugamuwa, Matara', N'Matara', 5.948700, 80.501200, 18, 7900000.00, 'MAT/2022/DEED/3345', 'Pending', '2026-07-03T14:55:00'),

-- P7  DUPLICATE IMAGE with P8 -> 15 / Low --------------------------------------
( 7, 7, N'22 Perch Land in Nallur, Jaffna',
   N'Residential land in a quiet Nallur neighbourhood, close to schools and the Nallur Kandaswamy temple. Well water on site and the boundary is already surveyed.',
   N'Nallur, Jaffna', N'Jaffna', 9.673500, 80.024700, 22, 8500000.00, 'JAF/2023/DEED/1102', 'Pending', '2026-07-04T10:05:00'),

-- P8  DUPLICATE IMAGE with P7 -> 15 / Low --------------------------------------
( 8, 10, N'50 Perch Agricultural Land - Anuradhapura',
   N'Half acre of cultivable land beside a minor irrigation tank, currently used for paddy. Access via a village gravel road, suitable for agriculture or a weekend property.',
   N'Mihintale, Anuradhapura', N'Anuradhapura', 8.354900, 80.510300, 50, 12000000.00, 'ANU/2021/DEED/7788', 'Pending', '2026-07-04T16:45:00'),

-- P9  NIC UNVERIFIED seller -> 20 / Low ----------------------------------------
( 9, 6, N'12 Perch Land Rajagiriya',
   N'Prime residential land in Rajagiriya with easy access to the Parliament road and the Colombo city limits. Suitable for a two storey house or a small apartment project.',
   N'Rajagiriya, Colombo', N'Colombo', 6.909100, 79.895600, 12, 41000000.00, 'COL/2022/DEED/5503', 'Pending', '2026-07-05T09:35:00'),

-- P10 DUPLICATE DEED with P11 -> 20 / Low --------------------------------------
(10, 7, N'25 Perch Land Kadawatha',
   N'Square shaped block in a developed residential lane in Kadawatha, 900 m from the Kandy road and close to the expressway entrance. All utilities available.',
   N'Kadawatha, Gampaha', N'Gampaha', 7.000200, 79.951800, 25, 21000000.00, 'GAM/2021/DEED/3390', 'Pending', '2026-07-05T13:15:00'),

-- P11 DUPLICATE DEED with P10 -> 20 / Low --------------------------------------
(11, 10, N'25 Perch Residential Block - Kadawatha',
   N'Residential land in a quiet cul-de-sac with a wide access road and mains water. Bank valuation report available on request for buyers seeking a housing loan.',
   N'Kadawatha, Gampaha', N'Gampaha', 7.001100, 79.952400, 25, 20500000.00, 'GAM/2021/DEED/3390', 'Pending', '2026-07-05T18:40:00'),

-- P12 NIC + DEED DUP (with P13) -> 40 / Low  [upper boundary of the Low band] ---
(12, 6, N'12 Perch Land Katugastota',
   N'Residential land close to the Katugastota bridge with mains water and three phase electricity at the boundary. Level block requiring no filling before construction.',
   N'Katugastota, Kandy', N'Kandy', 7.324500, 80.622100, 12, 8160000.00, 'KAN/2020/DEED/7712', 'Pending', '2026-07-06T08:50:00'),

-- P13 NIC + DEED DUP (with P12) -> 40 / Low ------------------------------------
(13, 6, N'12 Perch Block Katugastota - Second Plot',
   N'Second plot from the same parent land, sold separately. Quiet residential area with easy access to the Kandy town via the Katugastota main road.',
   N'Katugastota, Kandy', N'Kandy', 7.325000, 80.622900, 12, 8160000.00, 'KAN/2020/DEED/7712', 'Pending', '2026-07-06T09:05:00'),

-- P14 NIC + PRICE + LOCATION + MISSING + IMAGE + HISTORY -> 80 / High ----------
(14, 9, N'12 Perch Colombo Land - Below Market Price!!',
   N'Very urgent sale, owner needs the money this week. Price is far below the market rate. Deposit required immediately to reserve the land before other buyers.',
   N'Colombo area', N'Colombo', NULL, NULL, 12, 12000000.00, 'COL/2019/DEED/0091', 'Pending', '2026-07-07T23:10:00'),

-- P15 NIC + DEED DUP (with P16) + MISSING + HISTORY -> 60 / Medium -------------
(15, 9, N'20 Perch Land Hikkaduwa',
   N'Land close to the Hikkaduwa beach road, suitable for a guest house or a holiday home. Serious buyers only, viewing by appointment during weekdays.',
   N'Hikkaduwa, Galle', N'Galle', 6.140800, 80.101500, 20, 11600000.00, 'GAL/2018/DEED/5521', 'Pending', '2026-07-08T21:30:00'),

-- P16 DUPLICATE DEED with P15 -> 20 / Low  [the genuine owner] -----------------
(16, 11, N'22 Perch Beach Road Land - Hikkaduwa',
   N'Family owned land held under the same deed for over twenty years, now released for sale. Surveyed, fenced, and located 300 m from the Galle road.',
   N'Hikkaduwa, Galle', N'Galle', 6.141200, 80.102000, 22, 12980000.00, 'GAL/2018/DEED/5521', 'Pending', '2026-07-08T22:00:00'),

-- P17 PRICE + IMAGE (with P20) + SELLER HISTORY -> 42 / Medium -----------------
(17, 8, N'30 Perch Land Kurunegala - Quick Deal',
   N'Land available for an immediate sale at a reduced price. Located near the Kurunegala Puttalam road with good access for vehicles and mains electricity nearby.',
   N'Mallawapitiya, Kurunegala', N'Kurunegala', 7.501200, 80.365400, 30, 5700000.00, 'KUR/2019/DEED/9901', 'Pending', '2026-07-09T10:20:00'),

-- P18 REJECTED history listing (Malith) -> 15 / Low, stays Rejected ------------
(18, 8, N'28 Perch Land Kurunegala - Cheap',
   N'Land near the town centre offered at a very attractive price for a quick transaction. Documents can be shown after an advance payment is made.',
   N'Kurunegala Town', N'Kurunegala', 7.487300, 80.362800, 28, 5180000.00, 'KUR/2019/DEED/9902', 'Rejected', '2026-06-20T10:00:00'),

-- P19 REJECTED history listing (Malith) -> 15 / Low, stays Rejected ------------
(19, 8, N'26 Perch Land Kurunegala - Bargain Price',
   N'Bargain priced land close to the main road. Owner is not available for meetings, all communication is handled through the phone number on the listing.',
   N'Kurunegala Town', N'Kurunegala', 7.486900, 80.363500, 26, 4680000.00, 'KUR/2019/DEED/9903', 'Rejected', '2026-06-22T10:00:00'),

-- P20 DUPLICATE IMAGE with P17 -> 15 / Low  [the genuine owner] ----------------
(20, 7, N'25 Perch Land Mallawapitiya',
   N'Well maintained residential land with a concrete boundary and a small storage shed. Located in a developed area with neighbouring houses already constructed.',
   N'Mallawapitiya, Kurunegala', N'Kurunegala', 7.500800, 80.366100, 25, 8500000.00, 'KUR/2022/DEED/4455', 'Pending', '2026-07-09T15:40:00'),

-- P21 ALL SEVEN RULES FIRE -> 100 / High  [worst case demonstration] -----------
(21, 9, N'15 Perch Colombo 07 Land - Emergency Sale',
   N'Extremely urgent sale in a prime Colombo location at a fraction of the normal price. Advance payment required today to hold the property, no viewings available.',
   N'Colombo 07', N'Colombo', NULL, NULL, 15, 15000000.00, 'COL/2017/DEED/9080', 'Pending', '2026-07-10T02:15:00'),

-- P22 DUPLICATE DEED with P21 -> 20 / Low  [the genuine owner] -----------------
(22, 11, N'18 Perch Land Colombo 07',
   N'Land in an established Colombo 07 residential street, held by the family since 1998. Clear deed with a full chain of title available for the buyer to verify.',
   N'Colombo 07', N'Colombo', 6.906800, 79.865200, 18, 59400000.00, 'COL/2017/DEED/9080', 'Pending', '2026-07-10T09:00:00'),

-- P23 REJECTED history listing (Priyantha) -> 28 / Low, stays Rejected ---------
(23, 9, N'10 Perch Land Dehiwala',
   N'Residential land in Dehiwala offered for sale. Owner is currently abroad, so all arrangements are handled by a representative through online communication only.',
   N'Dehiwala, Colombo', N'Colombo', 6.851100, 79.865400, 10, 34000000.00, 'COL/2018/DEED/1140', 'Rejected', '2026-06-25T20:00:00'),

-- P24 REJECTED history listing (Priyantha) -> 28 / Low, stays Rejected ---------
(24, 9, N'10 Perch Land Mount Lavinia',
   N'Land close to the Mount Lavinia beach area. The seller is contactable by message only and requires a refundable deposit before the deed can be viewed.',
   N'Mount Lavinia, Colombo', N'Colombo', 6.838400, 79.863900, 10, 34000000.00, 'COL/2018/DEED/1141', 'Rejected', '2026-06-27T20:00:00'),

-- P25 NIC + DEED (with P26) + IMAGE (with P27) + PRICE + LOCATION -> 80 / High -
(25, 6, N'20 Perch Beach Land Trincomalee - Special Offer',
   N'Beach front land offered at a special reduced rate for a limited period only. Immediate booking payment is required, and the balance can be settled after transfer.',
   N'Nilaveli, Trincomalee', N'Trincomalee', NULL, NULL, 20, 3000000.00, 'TRI/2016/DEED/2245', 'Pending', '2026-07-11T01:45:00'),

-- P26 DUPLICATE DEED with P25 -> 20 / Low  [the genuine owner] -----------------
(26, 11, N'25 Perch Land Nilaveli, Trincomalee',
   N'Land situated 500 m inland from the Nilaveli beach, suitable for a holiday home or a small guest house. Surveyed with clear boundary markers on all four sides.',
   N'Nilaveli, Trincomalee', N'Trincomalee', 8.700400, 81.187600, 25, 7250000.00, 'TRI/2016/DEED/2245', 'Pending', '2026-07-11T11:20:00'),

-- P27 DUPLICATE IMAGE with P25 -> 15 / Low  [the genuine owner] ---------------
(27, 10, N'40 Perch Land Batticaloa',
   N'Spacious block on the Kallady side of Batticaloa with a wide frontage on a tarred road. Suitable for a residence or a small commercial building.',
   N'Kallady, Batticaloa', N'Batticaloa', 7.703200, 81.706400, 40, 7800000.00, 'BAT/2022/DEED/6690', 'Pending', '2026-07-12T09:50:00'),

-- P28 NIC + DEED (with P29) + IMAGE (with P29) + PRICE -> 70 / Medium ----------
--     [upper boundary of the Medium band - later approved on appeal]
(28, 6, N'16 Perch Land Nuwara Eliya - Discounted',
   N'Cool climate land in a residential area of Nuwara Eliya, offered at a discounted rate because the owner needs to settle a bank loan before the end of the quarter.',
   N'Nuwara Eliya Town', N'Nuwara Eliya', 6.970400, 80.782900, 16, 4480000.00, 'NUW/2019/DEED/0044', 'Pending', '2026-07-12T14:10:00'),

-- P29 DUPLICATE DEED + IMAGE with P28 -> 35 / Low ------------------------------
(29, 10, N'20 Perch Land Nuwara Eliya',
   N'Land with a pleasant view over the Nuwara Eliya valley, located on a private road shared by four households. Water and electricity connections are already in place.',
   N'Nuwara Eliya Town', N'Nuwara Eliya', 6.971100, 80.783500, 20, 9600000.00, 'NUW/2019/DEED/0044', 'Pending', '2026-07-12T15:35:00'),

-- P30 NIC + DEED (with P31) + PRICE + LOCATION + MISSING -> 73 / High ----------
--     [lower boundary of the High band]
(30, 6, N'14 Perch Matara Land',
   N'Cheap land, call now.',
   N'Matara', N'Matara', NULL, NULL, 14, 3500000.00, 'MAT/2015/DEED/6633', 'Pending', '2026-07-13T03:25:00'),

-- P31 DUPLICATE DEED with P30 -> 20 / Low  [the genuine owner] -----------------
(31, 5, N'16 Perch Land Matara Town',
   N'Residential land within the Matara municipal limits, close to schools and the bus stand. Held under a clear deed with the survey plan available for inspection.',
   N'Matara Town', N'Matara', 5.948200, 80.535500, 16, 6880000.00, 'MAT/2015/DEED/6633', 'Pending', '2026-07-13T10:00:00');

SET IDENTITY_INSERT dbo.Property OFF;
GO


/*==============================================================================
  4. PROPERTY IMAGES
  ------------------------------------------------------------------------------
  ImageHash simulates the SHA-256 / perceptual fingerprint written by the API.
  Five hashes are deliberately shared between two different properties so that
  fraud CHECK 2 (duplicate image) can be demonstrated:

      HASH_DUP_A  -> P7  and P8
      HASH_DUP_B  -> P14 and P21
      HASH_DUP_C  -> P17 and P20
      HASH_DUP_D  -> P25 and P27
      HASH_DUP_E  -> P28 and P29

  Every ImageURL below has a matching placeholder JPEG committed under
  LandGuard.API/wwwroot/uploads/properties/<filename> (flat, no
  PropertyID subfolder - that subfolder shape is only used by the real
  upload path, LocalFileStorageService.SaveImageAsync). These rows were
  originally seeded with no backing file at all, which 404'd through
  app.UseStaticFiles() while every genuinely-uploaded image worked fine.
  If you add a new seeded PropertyImage row here, add its placeholder
  file alongside it or the same 404 will recur for that row.
==============================================================================*/
INSERT INTO dbo.PropertyImage (PropertyID, ImageURL, ImageHash, IsPrimary) VALUES
 ( 1, N'/uploads/properties/p1_front.jpg',      'e3b0c44298fc1c149afbf4c8996fb001', 1),
 ( 1, N'/uploads/properties/p1_road.jpg',       'e3b0c44298fc1c149afbf4c8996fb002', 0),
 ( 2, N'/uploads/properties/p2_gate.jpg',       'e3b0c44298fc1c149afbf4c8996fb003', 1),
 ( 2, N'/uploads/properties/p2_wall.jpg',       'e3b0c44298fc1c149afbf4c8996fb004', 0),
 ( 3, N'/uploads/properties/p3_view.jpg',       'e3b0c44298fc1c149afbf4c8996fb005', 1),
 ( 4, N'/uploads/properties/p4_plot.jpg',       'e3b0c44298fc1c149afbf4c8996fb006', 1),
 ( 5, N'/uploads/properties/p5_coconut.jpg',    'e3b0c44298fc1c149afbf4c8996fb007', 1),
 ( 6, N'/uploads/properties/p6_land.jpg',       'e3b0c44298fc1c149afbf4c8996fb008', 1),

 ( 7, N'/uploads/properties/p7_nallur.jpg',     'HASH_DUP_A_9f2c7b41d8e05a3364bc1e', 1),
 ( 7, N'/uploads/properties/p7_side.jpg',       'e3b0c44298fc1c149afbf4c8996fb009', 0),
 ( 8, N'/uploads/properties/p8_paddy.jpg',      'HASH_DUP_A_9f2c7b41d8e05a3364bc1e', 1),
 ( 8, N'/uploads/properties/p8_tank.jpg',       'e3b0c44298fc1c149afbf4c8996fb010', 0),

 ( 9, N'/uploads/properties/p9_rajagiriya.jpg', 'e3b0c44298fc1c149afbf4c8996fb011', 1),
 (10, N'/uploads/properties/p10_kadawatha.jpg', 'e3b0c44298fc1c149afbf4c8996fb012', 1),
 (11, N'/uploads/properties/p11_block.jpg',     'e3b0c44298fc1c149afbf4c8996fb013', 1),
 (12, N'/uploads/properties/p12_katu.jpg',      'e3b0c44298fc1c149afbf4c8996fb014', 1),
 (13, N'/uploads/properties/p13_katu2.jpg',     'e3b0c44298fc1c149afbf4c8996fb015', 1),

 (14, N'/uploads/properties/p14_colombo.jpg',   'HASH_DUP_B_4a8e91c5f7203db6e84f0a', 1),
 (21, N'/uploads/properties/p21_col07.jpg',     'HASH_DUP_B_4a8e91c5f7203db6e84f0a', 1),

 (15, N'/uploads/properties/p15_hikka.jpg',     'e3b0c44298fc1c149afbf4c8996fb016', 1),
 (16, N'/uploads/properties/p16_beachrd.jpg',   'e3b0c44298fc1c149afbf4c8996fb017', 1),

 (17, N'/uploads/properties/p17_kuru.jpg',      'HASH_DUP_C_b62d0f8a3e4157cc90ad2b', 1),
 (20, N'/uploads/properties/p20_malla.jpg',     'HASH_DUP_C_b62d0f8a3e4157cc90ad2b', 1),
 (20, N'/uploads/properties/p20_shed.jpg',      'e3b0c44298fc1c149afbf4c8996fb018', 0),

 (18, N'/uploads/properties/p18_town.jpg',      'e3b0c44298fc1c149afbf4c8996fb019', 1),
 (19, N'/uploads/properties/p19_town.jpg',      'e3b0c44298fc1c149afbf4c8996fb020', 1),
 (22, N'/uploads/properties/p22_col07.jpg',     'e3b0c44298fc1c149afbf4c8996fb021', 1),
 (23, N'/uploads/properties/p23_dehiwala.jpg',  'e3b0c44298fc1c149afbf4c8996fb022', 1),
 (24, N'/uploads/properties/p24_mtlavinia.jpg', 'e3b0c44298fc1c149afbf4c8996fb023', 1),

 (25, N'/uploads/properties/p25_nilaveli.jpg',  'HASH_DUP_D_1c9047fb85e236aa7d3e6f', 1),
 (27, N'/uploads/properties/p27_kallady.jpg',   'HASH_DUP_D_1c9047fb85e236aa7d3e6f', 1),
 (27, N'/uploads/properties/p27_front.jpg',     'e3b0c44298fc1c149afbf4c8996fb024', 0),

 (26, N'/uploads/properties/p26_nilaveli.jpg',  'e3b0c44298fc1c149afbf4c8996fb025', 1),

 (28, N'/uploads/properties/p28_nuwara.jpg',    'HASH_DUP_E_7e5b23ad06fc4198cb70d5', 1),
 (29, N'/uploads/properties/p29_nuwara.jpg',    'HASH_DUP_E_7e5b23ad06fc4198cb70d5', 1),
 (29, N'/uploads/properties/p29_valley.jpg',    'e3b0c44298fc1c149afbf4c8996fb026', 0),

 (30, N'/uploads/properties/p30_matara.jpg',    'e3b0c44298fc1c149afbf4c8996fb027', 1),
 (31, N'/uploads/properties/p31_matara.jpg',    'e3b0c44298fc1c149afbf4c8996fb028', 1),
 (31, N'/uploads/properties/p31_road.jpg',      'e3b0c44298fc1c149afbf4c8996fb029', 0);
GO


/*==============================================================================
  5. RUN THE 8-POINT FRAUD ENGINE OVER THE WHOLE DATASET
  ------------------------------------------------------------------------------
  This generates one FRAUD_CHECK and one RISK_REPORT per property, sets each
  listing's status, and raises the seller / admin notifications - exactly what
  happens when a listing is submitted through POST /api/properties.
==============================================================================*/
PRINT '>> Running the fraud detection engine over all seeded properties...';
GO

EXEC dbo.usp_Fraud_ReanalyseAll;
GO


/*==============================================================================
  6. BUYER ACTIVITY   (seeded AFTER analysis so it does not alter the scores)
==============================================================================*/

-- Saved properties (FR07)
INSERT INTO dbo.SavedProperty (BuyerID, PropertyID) VALUES
 (12,  1), (12,  3), (12, 22),
 (13,  2), (13, 10), (13, 26),
 (14,  1), (14,  4),
 (15,  8), (15, 20), (15, 31),
 (16,  3), (16, 16);
GO

-- Suspicious listing reports (FR12)
INSERT INTO dbo.SuspiciousReport (BuyerID, PropertyID, Reason, Description, Status, ReportDate) VALUES
 (12, 21, N'Price is unrealistically low',
      N'A 15 perch block in Colombo 07 for 15 million is impossible. The seller is asking for a deposit before any viewing.',
      'Open',         '2026-07-14T10:20:00'),
 (13, 21, N'Seller refuses to show the deed',
      N'I asked to see the deed and the survey plan and the seller stopped replying.',
      'Open',         '2026-07-14T14:05:00'),
 (14, 14, N'Photos appear on another listing',
      N'The same photographs are used on another Colombo listing by what looks like the same person.',
      'Under Review', '2026-07-15T09:40:00'),
 (15, 25, N'Suspected fake beach front listing',
      N'The location given does not match the photographs and the price is far below anything else in Nilaveli.',
      'Open',         '2026-07-15T16:30:00'),
 (16, 18, N'Advance payment demanded before viewing',
      N'The seller insisted on an advance payment before showing any documents.',
      'Resolved',     '2026-06-28T11:00:00'),
 (12, 30, N'Listing has almost no information',
      N'There is no description, no location detail and no way to verify who the owner is.',
      'Open',         '2026-07-16T08:15:00');
GO


/*==============================================================================
  7. ADMIN ACTIVITY
==============================================================================*/

-- Admin approves P28 on appeal: a genuine discounted sale that the price rule
-- flagged. This is the manual appeal path from the Chapter 3.3 Risk Analysis.
EXEC dbo.usp_Admin_ApproveProperty
     @AdminID    = 1,
     @PropertyID = 28,
     @Remarks    = N'Bank loan settlement verified with the seller. Discounted price is genuine - listing approved.';
GO

-- Admin rejects the worst listing outright
EXEC dbo.usp_Admin_RejectProperty
     @AdminID    = 1,
     @PropertyID = 21,
     @Remarks    = N'All seven fraud rules triggered. Duplicate deed reference and stolen images confirmed.';
GO

-- Admin suspends the repeat fraudulent seller
EXEC dbo.usp_Admin_SetUserActive
     @AdminID      = 1,
     @TargetUserID = 9,
     @IsActive     = 0,
     @Remarks      = N'Three fraudulent listings confirmed, including one that triggered all seven detection rules.';
GO

-- Admin manually verifies a seller's NIC documents
EXEC dbo.usp_Admin_VerifyNIC
     @AdminID      = 2,
     @TargetUserID = 6,
     @Remarks      = N'NIC and supporting documents checked against the submitted scans.';
GO


/*==============================================================================
  8. FRAUD AWARENESS PODCASTS   (FR11 / NFR06)
==============================================================================*/
INSERT INTO dbo.Podcast (AdminID, Title, Language, Description, AudioURL, UploadDate) VALUES
 (1, N'How to Spot a Fake Land Deed', 'English',
     N'A short guide to the warning signs of a forged title deed and the checks a buyer can carry out at the Land Registry before paying any money.',
     N'/media/podcasts/en_fake_deed.mp3',        '2026-06-20T10:00:00'),
 (1, N'ව්‍යාජ ඔප්පු හඳුනා ගන්නේ කෙසේද', 'Sinhala',
     N'ව්‍යාජ ඔප්පු පිළිබඳ අනතුරු ඇඟවීමේ සලකුණු සහ මුදල් ගෙවීමට පෙර ගැනුම්කරුවෙකු කළ යුතු පරීක්ෂාවන්.',
     N'/media/podcasts/si_fake_deed.mp3',        '2026-06-20T10:15:00'),
 (1, N'போலி காணி பத்திரங்களை அடையாளம் காண்பது எப்படி', 'Tamil',
     N'போலி பத்திரங்களின் எச்சரிக்கை அறிகுறிகள் மற்றும் பணம் செலுத்துவதற்கு முன் வாங்குபவர் செய்ய வேண்டிய சரிபார்ப்புகள்.',
     N'/media/podcasts/ta_fake_deed.mp3',        '2026-06-20T10:30:00'),
 (2, N'Five Land Scams Common in Sri Lanka', 'English',
     N'Duplicate sales, impersonated owners, forged NIC details, unrealistic pricing and pressure deposits - how each scam works and how to avoid it.',
     N'/media/podcasts/en_five_scams.mp3',       '2026-07-02T09:00:00'),
 (2, N'ඉඩම් වංචා පහක්', 'Sinhala',
     N'ශ්‍රී ලංකාවේ බහුලව දක්නට ලැබෙන ඉඩම් වංචා පහක් සහ ඒවායින් ආරක්ෂා වන ආකාරය.',
     N'/media/podcasts/si_five_scams.mp3',       '2026-07-02T09:20:00'),
 (2, N'இலங்கையில் பொதுவான ஐந்து காணி மோசடிகள்', 'Tamil',
     N'இலங்கையில் பரவலாகக் காணப்படும் ஐந்து காணி மோசடிகள் மற்றும் அவற்றிலிருந்து தப்பிக்கும் வழிகள்.',
     N'/media/podcasts/ta_five_scams.mp3',       '2026-07-02T09:40:00');
GO

PRINT '>> Seed data loaded successfully.';
GO

/*------------------------------------------------------------------------------
  Quick confirmation counts
------------------------------------------------------------------------------*/
SELECT 'Users' AS TableName, COUNT(*) AS TotalRows FROM dbo.Users
UNION ALL SELECT 'Property',          COUNT(*) FROM dbo.Property
UNION ALL SELECT 'PropertyImage',     COUNT(*) FROM dbo.PropertyImage
UNION ALL SELECT 'FraudCheck',        COUNT(*) FROM dbo.FraudCheck
UNION ALL SELECT 'RiskReport',        COUNT(*) FROM dbo.RiskReport
UNION ALL SELECT 'SuspiciousReport',  COUNT(*) FROM dbo.SuspiciousReport
UNION ALL SELECT 'Notification',      COUNT(*) FROM dbo.Notification
UNION ALL SELECT 'SavedProperty',     COUNT(*) FROM dbo.SavedProperty
UNION ALL SELECT 'AdminAction',       COUNT(*) FROM dbo.AdminAction
UNION ALL SELECT 'Podcast',           COUNT(*) FROM dbo.Podcast
UNION ALL SELECT 'PriceBenchmark',    COUNT(*) FROM dbo.PriceBenchmark
UNION ALL SELECT 'FraudRuleWeight',   COUNT(*) FROM dbo.FraudRuleWeight;
GO

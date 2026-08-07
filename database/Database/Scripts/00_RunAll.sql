/*==============================================================================
  LANDGUARD - Land Transaction System with Fraud Detection
  ------------------------------------------------------------------------------
  Script  : 00_RunAll.sql
  Purpose : Builds the entire database in one go.
  Author  : Ladhurshan Sivasathyamoorthy
  ------------------------------------------------------------------------------
  HOW TO RUN

  Option A - SQL Server Management Studio (SSMS)
      1. Open SSMS and connect to your LOCAL instance
         (e.g. localhost\SQLEXPRESS  or  (localdb)\MSSQLLocalDB)
      2. Query menu -> SQLCMD Mode        <-- REQUIRED for the :r commands below
      3. Open this file, press F5.

  Option B - sqlcmd from a command prompt (run from the Scripts folder)
      sqlcmd -S localhost\SQLEXPRESS -E -f 65001 -i 00_RunAll.sql

      -f 65001 sets the UTF-8 code page so the Sinhala and Tamil podcast rows
      load correctly. Leave it out and those rows become question marks.

  Option C - run the six scripts manually, in numeric order.
==============================================================================*/
SET ANSI_NULLS ON;
GO

SET QUOTED_IDENTIFIER ON;
GO
:setvar ScriptDir "."

PRINT '=============================================================';
PRINT ' LANDGUARD DATABASE BUILD - STARTING';
PRINT '=============================================================';
GO

:r $(ScriptDir)\01_Schema.sql
:r $(ScriptDir)\02_Indexes.sql
:r $(ScriptDir)\03_Views.sql
:r $(ScriptDir)\04_StoredProcedures.sql
:r $(ScriptDir)\05_SeedData.sql

PRINT '=============================================================';
PRINT ' LANDGUARD DATABASE BUILD - COMPLETE';
PRINT ' Run 06_TestQueries.sql to verify the build.';
PRINT '=============================================================';
GO

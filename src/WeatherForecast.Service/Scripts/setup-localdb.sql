-- ============================================================
-- Script de création de la base locale PhM_Meteo_Dev (développement)
--
-- Fonctionne sur n'importe quelle instance SQL Server (2025, Express, LocalDB).
--
-- Exécution (adapter -S selon ton serveur) :
--   sqlcmd -S "localhost" -E -i setup-localdb.sql
--   sqlcmd -S "localhost\SQLEXPRESS" -E -i setup-localdb.sql
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i setup-localdb.sql
-- Ou : SSMS → Nouvelle requête → coller ce fichier → Exécuter
-- ============================================================

-- Créer la base si elle n'existe pas
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PhM_Meteo_Dev')
BEGIN
    CREATE DATABASE PhM_Meteo_Dev;
END
GO

USE PhM_Meteo_Dev;
GO

-- ============================================================
-- Tbl_MeteoDico (dictionnaire des conditions météo)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tbl_MeteoDico')
BEGIN
    CREATE TABLE Tbl_MeteoDico (
        MeteoDico_Id        INT             NOT NULL PRIMARY KEY,
        MeteoDico_Libelle_Fr VARCHAR(100)   NULL,
        MeteoDico_Libelle_Nl VARCHAR(100)   NULL,
        MeteoDico_Defaut    BIT             NULL,
        MeteoDico_Freemeteo VARCHAR(100)    NULL,
        MeteoDico_StampCrea DATETIME        NOT NULL DEFAULT(GETDATE()),
        MeteoDico_StampModi DATETIME        NOT NULL DEFAULT(GETDATE())
    );

    INSERT INTO Tbl_MeteoDico (MeteoDico_Id, MeteoDico_Libelle_Fr, MeteoDico_Libelle_Nl, MeteoDico_Defaut, MeteoDico_Freemeteo) VALUES
    (16, 'Peu nuageux',       'Licht bewolkt',     0, '2'),
    (17, 'Très nuageux',      'zeer bewolkt',      0, '3'),
    (18, 'Pluie',             'Regen',             0, '7'),
    (19, 'Temps clair',       'Droog',             0, '1'),
    (20, 'Nuageux',           'bewolkt',           0, '4'),
    (21, 'Pluie probable',    'Regen',             0, '5'),
    (22, 'Pluie légère',      'Licht regen',       0, '6'),
    (23, 'Pluie, tempête',    'Regen',             0, '10'),
    (24, 'Tempête',           'Storm',             0, '11'),
    (25, 'Neige probable',    'Sneeuwval',         0, '24'),
    (26, 'Neige légère',      'Sneeuwval',         0, '25'),
    (27, 'Chute de neige',    'Sneeuwval',         0, '26'),
    (28, 'Brouillard givrant','Aanvriezende mist', 0, '94');
END
GO

-- ============================================================
-- Tbl_MeteoLoc (les 14 emplacements)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tbl_MeteoLoc')
BEGIN
    CREATE TABLE Tbl_MeteoLoc (
        MeteoLoc_Id                INT             NOT NULL PRIMARY KEY,
        MeteoLoc_Loc               VARCHAR(30)     NULL,
        MeteoLoc_Ville             VARCHAR(100)    NULL,
        MeteoLoc_Pays              VARCHAR(100)    NULL,
        MeteoLoc_FreeMeteo         VARCHAR(100)    NULL,
        MeteoLoc_Freemeteo_Requete VARCHAR(150)    NULL,
        MeteoLoc_StampCrea         DATETIME        NOT NULL DEFAULT(GETDATE()),
        MeteoLoc_StampModi         DATETIME        NOT NULL DEFAULT(GETDATE()),
        MeteoLoc_Latitude          DECIMAL(9,6)    NULL,
        MeteoLoc_Longitude         DECIMAL(9,6)    NULL,
        MeteoLoc_Actif             BIT             NOT NULL DEFAULT(1),
        MeteoLoc_Source            NVARCHAR(50)    NULL
    );

    INSERT INTO Tbl_MeteoLoc (MeteoLoc_Id, MeteoLoc_Loc, MeteoLoc_Ville, MeteoLoc_Pays, MeteoLoc_Latitude, MeteoLoc_Longitude) VALUES
    (1,  'Hainaut',              'Charleroi',      'B', 50.410800, 4.444600),
    (2,  'Luxembourg L',         'Luxembourg',     'L', 49.611700, 6.131900),
    (3,  'Brabant',              'Uccle',          'B', 50.801400, 4.339200),
    (4,  'Flandre Occidentale',  'Oostende',       'B', 51.230000, 2.920000),
    (5,  'Limbourg',             'Kleine-Brogel',  'B', 51.168300, 5.470000),
    (6,  'Luxembourg B',         'St Hubert',      'B', 50.026700, 5.374400),
    (7,  'Liège',                'Bierset',        'B', 50.643300, 5.450000),
    (8,  'Anvers',               'Antwerpen',      'B', 51.219400, 4.402500),
    (9,  'Flandre Orientale',    'Semmerzake',     'B', 50.973600, 3.660000),
    (10, 'Namur',                'Florennes',      'B', 50.242000, 4.645000),
    (11, 'Lille',                'Lille',           'F', 50.629200, 3.057300),
    (12, 'Calais',               'Calais',         'F', 50.951300, 1.858700),
    (13, 'Maubeuge',             'Maubeuge',       'F', 50.277500, 3.972700),
    (14, 'Arras',                'Arras',          'F', 50.291000, 2.777500);
END
GO

-- ============================================================
-- Tbl_Meteo (données météo quotidiennes)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tbl_Meteo')
BEGIN
    CREATE TABLE Tbl_Meteo (
        Meteo_Id            INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Meteo_Date          SMALLDATETIME   NULL,
        Meteo_Temperature   SMALLMONEY      NULL,
        Meteo_Pays          CHAR(1)         NULL,
        Meteo_Precipitation SMALLMONEY      NULL,
        Meteo_Loc           VARCHAR(30)     NULL,
        Meteo_Ville         VARCHAR(100)    NULL,
        Meteo_StampCrea     DATETIME        NOT NULL DEFAULT(GETDATE()),
        Meteo_StampModi     DATETIME        NOT NULL DEFAULT(GETDATE()),
        Meteo_MeteoDico_Id  INT             NOT NULL
    );
END
GO

-- ============================================================
-- Tbl_Log (journal d'exécution)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tbl_Log')
BEGIN
    CREATE TABLE Tbl_Log (
        Log_Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Log_Date        DATETIME        NULL,
        Log_User_Id     VARCHAR(255)    NULL,
        Log_Type        CHAR(1)         NULL,
        Log_Fonction    VARCHAR(100)    NULL,
        Log_Msg         VARCHAR(2000)   NULL,
        Log_NbreRecord  INT             NULL
    );
END
GO

PRINT 'Base PhM_Meteo_Dev prête.';
GO

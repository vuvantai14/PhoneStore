function Get-CdSqlFixtureDefaults {
    param([string]$ComposeDirectory = $PSScriptRoot)
    [pscustomobject]@{
        Project             = 'phonestore-cd-rehearsal'
        NetworkName         = 'phonestore-cd-rehearsal_isolated'
        VolumeName          = 'phonestore-cd-rehearsal_sql'
        ComposeFile         = [IO.Path]::GetFullPath((Join-Path $ComposeDirectory 'docker-compose.rehearsal.yml'))
        SqlService          = 'db'
        SqlHost             = 'db,1433'
        SqlImage            = 'mcr.microsoft.com/mssql/server@sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd'
        SqlImageId          = 'sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd'
        DatabaseName        = 'PhoneStoreDB_CdRehearsal'
        ProvisionLogin      = 'cd_rehearsal_provision'
        AppLogin            = 'cd_rehearsal'
        ProductVersion      = '10.0.15'
        Migrations          = @('20261001150857_InitialCreate', '20261102122856_EnforceSingleAdmin')
        BrandName           = 'S353-FIXTURE-BRAND'
        CategoryName        = 'S353-FIXTURE-CATEGORY'
        ActiveMarker        = 'S353E-FIXTURE-PHONE'
        ActivePrice         = 12990000.00
        ActiveStock         = 5
        InactiveMarker      = 'S353E-FIXTURE-PHONE-INACTIVE'
        InactivePrice       = 9990000.00
        InactiveStock       = 0
        ForbiddenProjects   = @('phonestore-restore-c2', 'phonestore', 'phonestore-acceptance')
        ForbiddenDatabases  = @('PhoneStoreDB', 'PhoneStoreDB_AcceptanceTest', 'master', 'model', 'msdb', 'tempdb')
        ForbiddenVolumes    = @('phonestore-restore-c2_mssql_data')
        ForbiddenContainers = @('phonestore-restore-c2-db-1', 'phonestore-restore-c2-api-1', 'phonestore-restore-c2-web-1')
    }
}

function Write-CdSqlFixtureLog {
    param(
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][ValidateSet('INFO','PASS','FAIL')][string]$Status,
        [Parameter(Mandatory)][string]$Detail,
        [scriptblock]$LogWriter
    )
    $clean = ($Detail -replace '[\r\n]+', ' ').Trim()
    $line = '[CD-SQL-FIXTURE] utc={0} phase={1} status={2} detail="{3}"' -f `
        ([DateTime]::UtcNow.ToString('o')), $Phase, $Status, $clean
    if ($LogWriter) {
        & $LogWriter $line
    } else {
        Write-Host $line
    }
}

function ConvertTo-CdNormalizedPath {
    param([string]$PathValue)
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return '' }
    try {
        return ([IO.Path]::GetFullPath($PathValue)).Replace('/', '\').TrimEnd('\').ToLowerInvariant()
    } catch {
        return $PathValue.Replace('/', '\').TrimEnd('\').ToLowerInvariant()
    }
}

function Test-CdSqlFixtureConnectionString {
    param(
        [Parameter(Mandatory)][string]$ConnectionString,
        [string]$ExpectedHost = 'db,1433',
        [string]$ExpectedDatabase = 'PhoneStoreDB_CdRehearsal',
        [string]$ExpectedUser = 'cd_rehearsal'
    )
    Assert-Cd (-not [string]::IsNullOrWhiteSpace($ConnectionString)) 'SQL fixture connection string is empty.'
    $defaults = Get-CdSqlFixtureDefaults
    Assert-Cd ($ExpectedHost -ceq $defaults.SqlHost) 'SQL fixture hostname must be db,1433.'
    Assert-Cd ($ExpectedDatabase -ceq $defaults.DatabaseName -and $ExpectedDatabase -notin $defaults.ForbiddenDatabases) 'Database name is not the isolated CD rehearsal fixture.'
    Assert-Cd ($ExpectedUser -in @($defaults.AppLogin, $defaults.ProvisionLogin)) 'SQL login is outside the CD rehearsal fixture.'

    if ($ConnectionString -imatch '(^|;)\s*(Trusted_Connection|Integrated\s+Security)\s*=\s*(true|yes|sspi|1)') {
        throw 'Integrated security / business Windows Authentication connection string is forbidden in CD rehearsal.'
    }
    if ($ConnectionString -imatch '14334|PhoneStoreDB_AcceptanceTest') {
        throw 'Business or acceptance SQL endpoint detected in connection string.'
    }

    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ConnectionString)
    Assert-Cd (-not $builder.IntegratedSecurity) 'IntegratedSecurity is forbidden for SQL fixture.'
    Assert-Cd ($builder.DataSource -ceq $ExpectedHost) "SQL hostname '$($builder.DataSource)' does not match fixture '$ExpectedHost'."
    Assert-Cd ($builder.InitialCatalog -ceq $ExpectedDatabase) "Database name '$($builder.InitialCatalog)' does not match fixture '$ExpectedDatabase'."
    Assert-Cd ($builder.UserID -ceq $ExpectedUser) "SQL login '$($builder.UserID)' does not match expected fixture login '$ExpectedUser'."
    Assert-Cd ($builder.Encrypt -eq $true -and $builder.TrustServerCertificate -eq $true) 'SQL fixture connection string must keep TLS encryption enabled.'
    Assert-Cd ($builder.MaxPoolSize -ge 1 -and $builder.MaxPoolSize -le 10) 'SQL fixture connection string MaxPoolSize must be bounded (1..10).'
    $true
}

function Get-CdForbiddenBusinessSecrets {
    param(
        [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
        [scriptblock]$DockerRunner
    )
    $forbidden = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

    foreach ($envKey in @('MSSQL_SA_PASSWORD', 'SA_PASSWORD', 'S323B_SA', 'S323B_PROVISION', 'S323B_RUNTIME', 'S323B_ADMIN', 'S323B_CUSTOMER_A', 'S323B_CUSTOMER_B')) {
        $val = [Environment]::GetEnvironmentVariable($envKey)
        if (-not [string]::IsNullOrWhiteSpace($val)) { [void]$forbidden.Add($val.Trim()) }
    }

    $dotEnv = Join-Path $RepoRoot '.env'
    if (Test-Path -LiteralPath $dotEnv) {
        foreach ($line in (Get-Content -LiteralPath $dotEnv -ErrorAction SilentlyContinue)) {
            $trimmed = $line.Trim()
            if ($trimmed.StartsWith('#') -or -not $trimmed.Contains('=')) { continue }
            $idx = $trimmed.IndexOf('=')
            $k = $trimmed.Substring(0, $idx).Trim()
            $v = $trimmed.Substring($idx + 1).Trim().Trim('"').Trim("'")
            if ($k -imatch 'PASSWORD|SECRET|CONNECTION' -and -not [string]::IsNullOrWhiteSpace($v)) {
                [void]$forbidden.Add($v)
                if ($v -imatch 'Password=([^;]+)') { [void]$forbidden.Add($Matches[1].Trim()) }
            }
        }
    }

    if ($DockerRunner) {
        try {
            $raw = & $DockerRunner @('inspect', 'phonestore-restore-c2-db-1', 'phonestore-restore-c2-api-1', 'phonestore-restore-c2-web-1')
            if ($raw) {
                $bizContainers = @($raw | ConvertFrom-Json)
                foreach ($c in $bizContainers) {
                    foreach ($envEntry in @($c.Config.Env)) {
                        if ($envEntry -clike 'MSSQL_SA_PASSWORD=*') {
                            [void]$forbidden.Add($envEntry.Substring('MSSQL_SA_PASSWORD='.Length))
                        } elseif ($envEntry -clike 'ConnectionStrings__PhoneStoreConnection=*') {
                            $cs = $envEntry.Substring('ConnectionStrings__PhoneStoreConnection='.Length)
                            [void]$forbidden.Add($cs)
                            try {
                                $b = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($cs)
                                if (-not [string]::IsNullOrWhiteSpace($b.Password)) { [void]$forbidden.Add($b.Password) }
                            } catch { }
                        }
                    }
                }
            }
        } catch { }
    }

    $forbidden
}

function Assert-CdSqlFixtureCredentials {
    param(
        [Parameter(Mandatory)][hashtable]$Credentials,
        [string[]]$ForbiddenSecrets = @(),
        [switch]$RequireProvisionPassword
    )
    $pattern = '^[a-zA-Z0-9!_@#%^+=.-]{16,128}$'
    $sqlPassword = [string]$Credentials['SqlPassword']
    $appPassword = [string]$Credentials['AppPassword']
    $provisionPassword = [string]$Credentials['ProvisionPassword']

    Assert-Cd ($sqlPassword -cmatch $pattern) 'CD_REHEARSAL_SQL_PASSWORD is missing or does not meet complexity/format policy.'
    Assert-Cd ($appPassword -cmatch $pattern) 'CD_REHEARSAL_APP_PASSWORD is missing or does not meet complexity/format policy.'
    Assert-Cd ($sqlPassword -cne $appPassword) 'CD_REHEARSAL_SQL_PASSWORD and CD_REHEARSAL_APP_PASSWORD must be distinct.'

    if ($RequireProvisionPassword -or -not [string]::IsNullOrWhiteSpace($provisionPassword)) {
        Assert-Cd ($provisionPassword -cmatch $pattern) 'CD_REHEARSAL_PROVISION_PASSWORD is missing or does not meet complexity/format policy.'
        Assert-Cd ($provisionPassword -cne $sqlPassword -and $provisionPassword -cne $appPassword) 'CD_REHEARSAL_PROVISION_PASSWORD must be distinct from SQL SA and app passwords.'
    }

    $forbiddenSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($s in $ForbiddenSecrets) {
        if (-not [string]::IsNullOrWhiteSpace($s)) { [void]$forbiddenSet.Add($s) }
    }
    foreach ($candidate in @($sqlPassword, $appPassword, $provisionPassword)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and $forbiddenSet.Contains($candidate)) {
            throw 'Rehearsal secret reuses a forbidden business or acceptance secret.'
        }
    }
    $true
}

function Assert-CdSqlFixtureTopology {
    param(
        [Parameter(Mandatory)]$Topology,
        [string]$ExpectedProject = 'phonestore-cd-rehearsal',
        [string]$ExpectedComposeFile = ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'docker-compose.rehearsal.yml'))),
        [string]$ExpectedSqlHost = 'db,1433',
        [string]$ExpectedDatabase = 'PhoneStoreDB_CdRehearsal'
    )
    $defaults = Get-CdSqlFixtureDefaults
    Assert-Cd ($ExpectedProject -ceq $defaults.Project -and $ExpectedProject -notin $defaults.ForbiddenProjects) 'Compose project is not the isolated CD rehearsal project.'
    Assert-Cd ($ExpectedSqlHost -ceq $defaults.SqlHost) 'SQL hostname is not the isolated fixture hostname (db,1433).'
    Assert-Cd ($ExpectedDatabase -ceq $defaults.DatabaseName -and $ExpectedDatabase -notin $defaults.ForbiddenDatabases) 'Database name is not the isolated fixture database (PhoneStoreDB_CdRehearsal).'

    $db = $Topology.DbContainer
    $network = $Topology.Network
    $volume = $Topology.Volume
    Assert-Cd ($null -ne $db -and $null -ne $network -and $null -ne $volume) 'Incomplete rehearsal Docker topology metadata.'

    $dbName = ([string]$db.Name).TrimStart('/')
    Assert-Cd ($dbName -notin $defaults.ForbiddenContainers -and $dbName -notlike 'phonestore-restore-c2*') 'Target SQL container belongs to business stack.'
    if ($Topology.BusinessContainerIds -and ($Topology.BusinessContainerIds -contains [string]$db.Id)) {
        throw 'Target SQL container ID matches a running business container ID.'
    }

    Assert-Cd ($db.Config.Labels.'com.docker.compose.project' -ceq $defaults.Project -and
        $db.Config.Labels.'com.docker.compose.service' -ceq $defaults.SqlService) 'SQL container Compose project or service label mismatch.'

    $expectedNormCompose = ConvertTo-CdNormalizedPath $ExpectedComposeFile
    $actualNormCompose = ConvertTo-CdNormalizedPath ([string]$db.Config.Labels.'com.docker.compose.project.config_files')
    Assert-Cd ($actualNormCompose -ceq $expectedNormCompose) 'SQL container config_files label is outside scripts/cd/docker-compose.rehearsal.yml.'

    Assert-Cd ([bool]$db.State.Running -and -not [bool]$db.HostConfig.Privileged) 'SQL fixture container must be running and unprivileged.'
    if ($null -ne $db.State.PSObject.Properties['Health'] -and $db.State.Health) {
        Assert-Cd ($db.State.Health.Status -ceq 'healthy') "SQL fixture container health is '$($db.State.Health.Status)', expected 'healthy'."
    }
    $portBindings = if ($db.HostConfig.PortBindings) { @($db.HostConfig.PortBindings.PSObject.Properties.Name) } else { @() }
    Assert-Cd ($portBindings.Count -eq 0) 'SQL fixture container must not publish any host ports.'

    $binds = @($db.HostConfig.Binds | Where-Object { $_ -and $_ -cnotlike "$($defaults.VolumeName):/var/opt/mssql*" })
    Assert-Cd ($binds.Count -eq 0) 'SQL fixture container must not have any host bind mounts.'

    $mounts = @($db.Mounts)
    Assert-Cd ($mounts.Count -eq 1 -and
        $mounts[0].Type -ceq 'volume' -and
        $mounts[0].Name -ceq $defaults.VolumeName -and
        $mounts[0].Name -notin $defaults.ForbiddenVolumes -and
        $mounts[0].Destination -ceq '/var/opt/mssql' -and
        [bool]$mounts[0].RW) 'SQL fixture container volume mount mismatch or uses business volume.'

    Assert-Cd ($volume.Name -ceq $defaults.VolumeName -and
        $volume.Name -notin $defaults.ForbiddenVolumes -and
        $volume.Driver -ceq 'local' -and
        -not $volume.Options -and
        $volume.Labels.'com.docker.compose.project' -ceq $defaults.Project) 'SQL fixture Docker volume isolation check failed.'

    Assert-Cd ($network.Name -ceq $defaults.NetworkName -and
        [bool]$network.Internal -and
        $network.Labels.'com.docker.compose.project' -ceq $defaults.Project) 'SQL fixture Docker network must be internal and labeled for phonestore-cd-rehearsal.'

    $dbNets = @($db.NetworkSettings.Networks.PSObject.Properties.Name)
    Assert-Cd (($dbNets -join ',') -ceq $defaults.NetworkName) 'SQL fixture container must be attached exclusively to phonestore-cd-rehearsal_isolated.'

    foreach ($app in @($Topology.ApiContainer, $Topology.WebContainer | Where-Object { $_ })) {
        $appService = [string]$app.Config.Labels.'com.docker.compose.service'
        Assert-Cd ($app.Config.Labels.'com.docker.compose.project' -ceq $defaults.Project -and
            (ConvertTo-CdNormalizedPath ([string]$app.Config.Labels.'com.docker.compose.project.config_files')) -ceq $expectedNormCompose) "App container $appService is outside rehearsal Compose file."
        $appMounts = @($app.Mounts)
        Assert-Cd ($appMounts.Count -eq 0 -and -not [bool]$app.HostConfig.Privileged) "App container $appService must not have mounts or privileged mode."
        $connEnv = @($app.Config.Env | Where-Object { $_ -clike 'ConnectionStrings__PhoneStoreConnection=*' })
        Assert-Cd ($connEnv.Count -eq 1) "App container $appService is missing ConnectionStrings__PhoneStoreConnection."
        $cs = $connEnv[0].Substring($connEnv[0].IndexOf('=') + 1)
        Test-CdSqlFixtureConnectionString -ConnectionString $cs -ExpectedHost $ExpectedSqlHost -ExpectedDatabase $ExpectedDatabase -ExpectedUser $defaults.AppLogin | Out-Null
    }

    $containerHost = [string]$db.Config.Hostname
    Assert-Cd (-not [string]::IsNullOrWhiteSpace($containerHost) -and $containerHost -cne 'addd99b34bf2') 'Invalid or business SQL container hostname.'

    [pscustomobject]@{
        ContainerId       = [string]$db.Id
        ContainerHostname = $containerHost
        Project           = $defaults.Project
        NetworkName       = $defaults.NetworkName
        VolumeName        = $defaults.VolumeName
        DatabaseName      = $defaults.DatabaseName
        SqlHost           = $defaults.SqlHost
    }
}

function Get-CdSqlPreflightScript {
    param([Parameter(Mandatory)][string]$ExpectedMachineName)
    $safeHost = $ExpectedMachineName.Replace("'", "''")
    @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_ID(N'PhoneStoreDB') IS NOT NULL OR DB_ID(N'PhoneStoreDB_AcceptanceTest') IS NOT NULL
    THROW 51090, 'Target SQL instance contains a business or acceptance database; aborting fixture provisioning.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> N'$safeHost'
    THROW 51092, 'SQL instance MachineName does not match verified rehearsal container hostname.', 1;
SELECT CONCAT(
    N'PREFLIGHT_OK|',
    CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)),
    N'|',
    ISNULL(CAST(DB_ID(N'PhoneStoreDB') AS nvarchar(20)), N'NULL'),
    N'|',
    ISNULL(CAST(DB_ID(N'PhoneStoreDB_AcceptanceTest') AS nvarchar(20)), N'NULL')
);
"@
}

function Get-CdSqlBootstrapScript {
    param(
        [Parameter(Mandatory)][string]$ExpectedMachineName,
        [Parameter(Mandatory)][string]$ProvisionPassword,
        [Parameter(Mandatory)][string]$AppPassword
    )
    $defaults = Get-CdSqlFixtureDefaults
    $safeHost = $ExpectedMachineName.Replace("'", "''")
    $safeProvPwd = $ProvisionPassword.Replace("'", "''")
    $safeAppPwd = $AppPassword.Replace("'", "''")
    $db = $defaults.DatabaseName
    $provLogin = $defaults.ProvisionLogin
    $appLogin = $defaults.AppLogin

    @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_ID(N'PhoneStoreDB') IS NOT NULL OR DB_ID(N'PhoneStoreDB_AcceptanceTest') IS NOT NULL
    THROW 51090, 'Target SQL instance contains a business or acceptance database; aborting.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> N'$safeHost'
    THROW 51092, 'SQL instance MachineName does not match verified rehearsal container hostname.', 1;

IF DB_ID(N'$db') IS NULL
BEGIN
    CREATE DATABASE [$db]
    ON PRIMARY (NAME = N'$db', FILENAME = N'/var/opt/mssql/data/$db.mdf', SIZE = 32MB, MAXSIZE = 256MB, FILEGROWTH = 8MB)
    LOG ON (NAME = N'${db}_log', FILENAME = N'/var/opt/mssql/data/${db}_log.ldf', SIZE = 16MB, MAXSIZE = 128MB, FILEGROWTH = 8MB);
END;
GO

USE [master];
IF SUSER_ID(N'$provLogin') IS NULL
    CREATE LOGIN [$provLogin] WITH PASSWORD = N'$safeProvPwd', DEFAULT_DATABASE = [$db], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [$provLogin] WITH PASSWORD = N'$safeProvPwd', DEFAULT_DATABASE = [$db], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ALTER LOGIN [$provLogin] ENABLE;

IF SUSER_ID(N'$appLogin') IS NULL
    CREATE LOGIN [$appLogin] WITH PASSWORD = N'$safeAppPwd', DEFAULT_DATABASE = [$db], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [$appLogin] WITH PASSWORD = N'$safeAppPwd', DEFAULT_DATABASE = [$db], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ALTER LOGIN [$appLogin] ENABLE;
GO

USE [$db];
IF DB_NAME() <> N'$db'
    THROW 51091, 'Active database is not PhoneStoreDB_CdRehearsal.', 1;
IF USER_ID(N'$provLogin') IS NULL
    CREATE USER [$provLogin] FOR LOGIN [$provLogin];
ELSE
    ALTER USER [$provLogin] WITH LOGIN = [$provLogin];
ALTER ROLE [db_owner] ADD MEMBER [$provLogin];

IF USER_ID(N'$appLogin') IS NULL
    CREATE USER [$appLogin] FOR LOGIN [$appLogin];
ELSE
    ALTER USER [$appLogin] WITH LOGIN = [$appLogin];
SELECT N'BOOTSTRAP_OK|$db';
"@
}

function Get-CdSqlMigrationAndSeedScript {
    param([Parameter(Mandatory)][string]$ExpectedMachineName)
    $defaults = Get-CdSqlFixtureDefaults
    $safeHost = $ExpectedMachineName.Replace("'", "''")
    $db = $defaults.DatabaseName
    $provLogin = $defaults.ProvisionLogin
    $productVersion = $defaults.ProductVersion
    $brand = $defaults.BrandName
    $category = $defaults.CategoryName
    $activeMarker = $defaults.ActiveMarker
    $inactiveMarker = $defaults.InactiveMarker

    @"
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;

IF DB_ID(N'PhoneStoreDB') IS NOT NULL OR DB_ID(N'PhoneStoreDB_AcceptanceTest') IS NOT NULL
    THROW 51090, 'Target SQL instance contains a business or acceptance database; aborting.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> N'$safeHost'
    THROW 51092, 'SQL instance MachineName does not match verified rehearsal container hostname.', 1;
IF DB_NAME() <> N'$db'
    THROW 51091, 'Active database is not PhoneStoreDB_CdRehearsal.', 1;
IF ORIGINAL_LOGIN() <> N'$provLogin'
    THROW 51093, 'Schema and seed must execute under dedicated provisioning login cd_rehearsal_provision.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[__EFMigrationsHistory] (
            [MigrationId] nvarchar(150) NOT NULL,
            [ProductVersion] nvarchar(32) NOT NULL,
            CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
        );
    END;

    -- Migration 1: 20261001150857_InitialCreate
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261001150857_InitialCreate')
    BEGIN
        CREATE TABLE [dbo].[Brands] (
            [BrandId] int NOT NULL IDENTITY(1,1),
            [BrandName] nvarchar(100) NOT NULL,
            [Description] nvarchar(500) NULL,
            [IsActive] bit NOT NULL CONSTRAINT [DF_Brands_IsActive] DEFAULT (CONVERT(bit, 1)),
            [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_Brands_CreatedAt] DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT [PK_Brands] PRIMARY KEY ([BrandId])
        );

        CREATE TABLE [dbo].[Categories] (
            [CategoryId] int NOT NULL IDENTITY(1,1),
            [CategoryName] nvarchar(100) NOT NULL,
            [Description] nvarchar(500) NULL,
            [IsActive] bit NOT NULL CONSTRAINT [DF_Categories_IsActive] DEFAULT (CONVERT(bit, 1)),
            [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_Categories_CreatedAt] DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryId])
        );

        CREATE TABLE [dbo].[Roles] (
            [RoleId] int NOT NULL IDENTITY(1,1),
            [RoleName] nvarchar(50) NOT NULL,
            CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
        );

        CREATE TABLE [dbo].[Products] (
            [ProductId] int NOT NULL IDENTITY(1,1),
            [ProductName] nvarchar(200) NOT NULL,
            [BrandId] int NOT NULL,
            [CategoryId] int NOT NULL,
            [Price] decimal(18,2) NOT NULL,
            [StockQuantity] int NOT NULL,
            [RAM] nvarchar(50) NULL,
            [Storage] nvarchar(50) NULL,
            [Chip] nvarchar(150) NULL,
            [Screen] nvarchar(200) NULL,
            [Camera] nvarchar(250) NULL,
            [Battery] nvarchar(100) NULL,
            [OperatingSystem] nvarchar(100) NULL,
            [Description] nvarchar(max) NULL,
            [IsActive] bit NOT NULL CONSTRAINT [DF_Products_IsActive] DEFAULT (CONVERT(bit, 1)),
            [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_Products_CreatedAt] DEFAULT (SYSUTCDATETIME()),
            [UpdatedAt] datetime2 NULL,
            CONSTRAINT [PK_Products] PRIMARY KEY ([ProductId]),
            CONSTRAINT [CK_Products_Price] CHECK ([Price] > 0),
            CONSTRAINT [CK_Products_StockQuantity] CHECK ([StockQuantity] >= 0),
            CONSTRAINT [FK_Products_Brands_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [dbo].[Brands] ([BrandId]),
            CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories] ([CategoryId])
        );

        CREATE TABLE [dbo].[Users] (
            [UserId] int NOT NULL IDENTITY(1,1),
            [RoleId] int NOT NULL,
            [FullName] nvarchar(100) NOT NULL,
            [Email] nvarchar(150) NOT NULL,
            [Phone] nvarchar(20) NULL,
            [PasswordHash] nvarchar(500) NOT NULL,
            [Address] nvarchar(255) NULL,
            [IsActive] bit NOT NULL CONSTRAINT [DF_Users_IsActive] DEFAULT (CONVERT(bit, 1)),
            [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),
            CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([RoleId])
        );

        CREATE TABLE [dbo].[ProductImages] (
            [ImageId] int NOT NULL IDENTITY(1,1),
            [ProductId] int NOT NULL,
            [ImageUrl] nvarchar(500) NOT NULL,
            [IsPrimary] bit NOT NULL CONSTRAINT [DF_ProductImages_IsPrimary] DEFAULT (CONVERT(bit, 0)),
            [DisplayOrder] int NOT NULL,
            CONSTRAINT [PK_ProductImages] PRIMARY KEY ([ImageId]),
            CONSTRAINT [CK_ProductImages_DisplayOrder] CHECK ([DisplayOrder] >= 0),
            CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([ProductId]) ON DELETE CASCADE
        );

        CREATE TABLE [dbo].[Orders] (
            [OrderId] int NOT NULL IDENTITY(1,1),
            [UserId] int NOT NULL,
            [OrderCode] nvarchar(30) NOT NULL,
            [ReceiverName] nvarchar(100) NOT NULL,
            [ReceiverPhone] nvarchar(20) NOT NULL,
            [ShippingAddress] nvarchar(300) NOT NULL,
            [Note] nvarchar(500) NULL,
            [OrderDate] datetime2 NOT NULL CONSTRAINT [DF_Orders_OrderDate] DEFAULT (SYSUTCDATETIME()),
            [TotalAmount] decimal(18,2) NOT NULL,
            [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_Orders_Status] DEFAULT (N'Pending'),
            CONSTRAINT [PK_Orders] PRIMARY KEY ([OrderId]),
            CONSTRAINT [CK_Orders_TotalAmount] CHECK ([TotalAmount] >= 0),
            CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId])
        );

        CREATE TABLE [dbo].[OrderDetails] (
            [OrderDetailId] int NOT NULL IDENTITY(1,1),
            [OrderId] int NOT NULL,
            [ProductId] int NOT NULL,
            [Quantity] int NOT NULL,
            [UnitPrice] decimal(18,2) NOT NULL,
            CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([OrderDetailId]),
            CONSTRAINT [CK_OrderDetails_Quantity] CHECK ([Quantity] > 0),
            CONSTRAINT [CK_OrderDetails_UnitPrice] CHECK ([UnitPrice] >= 0),
            CONSTRAINT [FK_OrderDetails_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([OrderId]) ON DELETE CASCADE,
            CONSTRAINT [FK_OrderDetails_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([ProductId])
        );

        SET IDENTITY_INSERT [dbo].[Roles] ON;
        INSERT INTO [dbo].[Roles] ([RoleId], [RoleName]) VALUES (1, N'Admin'), (2, N'Customer');
        SET IDENTITY_INSERT [dbo].[Roles] OFF;

        CREATE UNIQUE INDEX [IX_Brands_BrandName] ON [dbo].[Brands] ([BrandName]);
        CREATE UNIQUE INDEX [IX_Categories_CategoryName] ON [dbo].[Categories] ([CategoryName]);
        CREATE INDEX [IX_OrderDetails_OrderId] ON [dbo].[OrderDetails] ([OrderId]);
        CREATE INDEX [IX_OrderDetails_ProductId] ON [dbo].[OrderDetails] ([ProductId]);
        CREATE UNIQUE INDEX [IX_Orders_OrderCode] ON [dbo].[Orders] ([OrderCode]);
        CREATE INDEX [IX_Orders_UserId] ON [dbo].[Orders] ([UserId]);
        CREATE INDEX [IX_ProductImages_ProductId] ON [dbo].[ProductImages] ([ProductId]);
        CREATE INDEX [IX_Products_BrandId] ON [dbo].[Products] ([BrandId]);
        CREATE INDEX [IX_Products_CategoryId] ON [dbo].[Products] ([CategoryId]);
        CREATE UNIQUE INDEX [IX_Roles_RoleName] ON [dbo].[Roles] ([RoleName]);
        CREATE UNIQUE INDEX [IX_Users_Email] ON [dbo].[Users] ([Email]);
        CREATE INDEX [IX_Users_RoleId] ON [dbo].[Users] ([RoleId]);

        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20261001150857_InitialCreate', N'$productVersion');
    END;

    -- Migration 2: 20261102122856_EnforceSingleAdmin
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261102122856_EnforceSingleAdmin')
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleId] = 1 AND [RoleName] = N'Admin')
            OR EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = N'Admin' AND [RoleId] <> 1)
            THROW 51000, 'Admin role mapping must be reviewed before this migration.', 1;
        IF (SELECT COUNT_BIG(*) FROM [dbo].[Users] WHERE [RoleId] = 1) > 1
            THROW 51001, 'Multiple Admin accounts exist; no automatic data changes are allowed.', 1;

        CREATE UNIQUE INDEX [UX_Users_SingleAdmin] ON [dbo].[Users] ([RoleId]) WHERE [RoleId] = 1;
        ALTER TABLE [dbo].[Roles] ADD CONSTRAINT [CK_Roles_AdminIdentity]
            CHECK (([RoleId] = 1 AND [RoleName] = N'Admin') OR ([RoleId] <> 1 AND [RoleName] <> N'Admin'));

        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20261102122856_EnforceSingleAdmin', N'$productVersion');
    END;

    -- Idempotent minimal synthetic catalog seed
    IF NOT EXISTS (SELECT 1 FROM [dbo].[Brands] WHERE [BrandName] = N'$brand')
    BEGIN
        INSERT INTO [dbo].[Brands] ([BrandName], [Description], [IsActive])
        VALUES (N'$brand', N'Synthetic CD rehearsal brand fixture', 1);
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Brands]
        SET [Description] = N'Synthetic CD rehearsal brand fixture', [IsActive] = 1
        WHERE [BrandName] = N'$brand';
    END;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[Categories] WHERE [CategoryName] = N'$category')
    BEGIN
        INSERT INTO [dbo].[Categories] ([CategoryName], [Description], [IsActive])
        VALUES (N'$category', N'Synthetic CD rehearsal category fixture', 1);
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Categories]
        SET [Description] = N'Synthetic CD rehearsal category fixture', [IsActive] = 1
        WHERE [CategoryName] = N'$category';
    END;

    DECLARE @BrandId int = (SELECT [BrandId] FROM [dbo].[Brands] WHERE [BrandName] = N'$brand');
    DECLARE @CategoryId int = (SELECT [CategoryId] FROM [dbo].[Categories] WHERE [CategoryName] = N'$category');

    IF NOT EXISTS (SELECT 1 FROM [dbo].[Products] WHERE [ProductName] = N'$activeMarker')
    BEGIN
        INSERT INTO [dbo].[Products] (
            [ProductName], [BrandId], [CategoryId], [Price], [StockQuantity],
            [RAM], [Storage], [Chip], [Screen], [Camera], [Battery], [OperatingSystem],
            [Description], [IsActive]
        )
        VALUES (
            N'$activeMarker', @BrandId, @CategoryId, 12990000.00, 5,
            N'8 GB', N'128 GB', N'Synthetic SoC', N'6.1 inch OLED', N'50 MP', N'4500 mAh', N'Android 15',
            N'Synthetic CD rehearsal active catalog fixture', 1
        );
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Products]
        SET [BrandId] = @BrandId, [CategoryId] = @CategoryId, [Price] = 12990000.00,
            [StockQuantity] = 5, [RAM] = N'8 GB', [Storage] = N'128 GB', [IsActive] = 1
        WHERE [ProductName] = N'$activeMarker';
    END;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[Products] WHERE [ProductName] = N'$inactiveMarker')
    BEGIN
        INSERT INTO [dbo].[Products] (
            [ProductName], [BrandId], [CategoryId], [Price], [StockQuantity],
            [RAM], [Storage], [Description], [IsActive]
        )
        VALUES (
            N'$inactiveMarker', @BrandId, @CategoryId, 9990000.00, 0,
            N'8 GB', N'128 GB', N'Synthetic CD rehearsal inactive catalog fixture', 0
        );
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Products]
        SET [BrandId] = @BrandId, [CategoryId] = @CategoryId, [Price] = 9990000.00,
            [StockQuantity] = 0, [RAM] = N'8 GB', [Storage] = N'128 GB', [IsActive] = 0
        WHERE [ProductName] = N'$inactiveMarker';
    END;

    IF (SELECT COUNT(*) FROM [dbo].[Users]) <> 0 OR (SELECT COUNT(*) FROM [dbo].[Orders]) <> 0 OR (SELECT COUNT(*) FROM [dbo].[OrderDetails]) <> 0
        THROW 51094, 'Fixture database contains unexpected Users or Orders records; aborting.', 1;

    COMMIT TRANSACTION;
    SELECT N'MIGRATE_SEED_OK';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
"@
}

function Get-CdSqlLockdownScript {
    param([Parameter(Mandatory)][string]$ExpectedMachineName)
    $defaults = Get-CdSqlFixtureDefaults
    $safeHost = $ExpectedMachineName.Replace("'", "''")
    $db = $defaults.DatabaseName
    $provLogin = $defaults.ProvisionLogin
    $appLogin = $defaults.AppLogin

    @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_ID(N'PhoneStoreDB') IS NOT NULL OR DB_ID(N'PhoneStoreDB_AcceptanceTest') IS NOT NULL
    THROW 51090, 'Target SQL instance contains a business or acceptance database; aborting.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> N'$safeHost'
    THROW 51092, 'SQL instance MachineName does not match verified rehearsal container hostname.', 1;

USE [$db];
IF DB_NAME() <> N'$db'
    THROW 51091, 'Active database is not PhoneStoreDB_CdRehearsal.', 1;

GRANT SELECT ON OBJECT::[dbo].[Brands] TO [$appLogin];
GRANT SELECT ON OBJECT::[dbo].[Categories] TO [$appLogin];
GRANT SELECT ON OBJECT::[dbo].[Products] TO [$appLogin];
GRANT SELECT ON OBJECT::[dbo].[ProductImages] TO [$appLogin];
GRANT SELECT ON OBJECT::[dbo].[Orders] TO [$appLogin];
GRANT SELECT ON OBJECT::[dbo].[OrderDetails] TO [$appLogin];
GRANT SELECT ON OBJECT::[dbo].[Roles] TO [$appLogin];
GRANT SELECT ON OBJECT::[dbo].[__EFMigrationsHistory] TO [$appLogin];

DENY SELECT ON OBJECT::[dbo].[Users] TO [$appLogin];
DENY INSERT, UPDATE, DELETE, ALTER ON SCHEMA::[dbo] TO [$appLogin];

IF (SELECT COUNT(*) FROM [dbo].[Users]) <> 0 OR (SELECT COUNT(*) FROM [dbo].[Orders]) <> 0 OR (SELECT COUNT(*) FROM [dbo].[OrderDetails]) <> 0
    THROW 51094, 'Fixture database contains unexpected Users or Orders records; aborting.', 1;

USE [master];
ALTER LOGIN [$provLogin] DISABLE;
SELECT N'LOCKDOWN_OK';
"@
}

function Get-CdSqlVerificationScript {
    param([Parameter(Mandatory)][string]$ExpectedMachineName)
    $defaults = Get-CdSqlFixtureDefaults
    $safeHost = $ExpectedMachineName.Replace("'", "''")
    $db = $defaults.DatabaseName
    $appLogin = $defaults.AppLogin
    $brand = $defaults.BrandName
    $category = $defaults.CategoryName
    $activeMarker = $defaults.ActiveMarker
    $inactiveMarker = $defaults.InactiveMarker

    @"
SET NOCOUNT ON;
IF DB_NAME() <> N'$db'
    THROW 51091, 'Active database is not PhoneStoreDB_CdRehearsal.', 1;
IF CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) <> N'$safeHost'
    THROW 51092, 'SQL instance MachineName does not match verified rehearsal container hostname.', 1;
IF ORIGINAL_LOGIN() <> N'$appLogin'
    THROW 51095, 'Verification query must execute as runtime login cd_rehearsal.', 1;

SELECT
    DB_NAME() AS [database],
    CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) AS [machineName],
    ORIGINAL_LOGIN() AS [login],
    CASE WHEN DB_ID(N'PhoneStoreDB') IS NULL AND DB_ID(N'PhoneStoreDB_AcceptanceTest') IS NULL THEN 0 ELSE 1 END AS [businessDbPresent],
    ISNULL(IS_SRVROLEMEMBER('sysadmin'), 0) AS [isSysAdmin],
    ISNULL(HAS_DBACCESS(N'PhoneStoreDB'), 0) AS [hasBusinessAccess],
    ISNULL(HAS_PERMS_BY_NAME(N'dbo.Products', N'OBJECT', N'SELECT'), 0) AS [canSelectProducts],
    ISNULL(HAS_PERMS_BY_NAME(N'dbo.Products', N'OBJECT', N'INSERT'), 0) AS [canInsertProducts],
    ISNULL(HAS_PERMS_BY_NAME(N'dbo.Products', N'OBJECT', N'UPDATE'), 0) AS [canUpdateProducts],
    ISNULL(HAS_PERMS_BY_NAME(N'dbo.Products', N'OBJECT', N'DELETE'), 0) AS [canDeleteProducts],
    ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER'), 0) AS [canAlterDatabase],
    ISNULL(HAS_PERMS_BY_NAME(N'dbo.__EFMigrationsHistory', N'OBJECT', N'UPDATE'), 0) AS [canUpdateMigrations],
    (SELECT COUNT(*) FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] IN (N'20261001150857_InitialCreate', N'20261102122856_EnforceSingleAdmin')) AS [migrations],
    (SELECT COUNT(*) FROM [dbo].[Roles]) AS [roles],
    (SELECT COUNT(*) FROM [dbo].[Brands] WHERE [BrandName] = N'$brand' AND [IsActive] = 1) AS [brands],
    (SELECT COUNT(*) FROM [dbo].[Categories] WHERE [CategoryName] = N'$category' AND [IsActive] = 1) AS [categories],
    ISNULL((SELECT TOP (1) [ProductId] FROM [dbo].[Products] WHERE [ProductName] = N'$activeMarker' AND [IsActive] = 1), 0) AS [activeProductId],
    ISNULL((SELECT TOP (1) [StockQuantity] FROM [dbo].[Products] WHERE [ProductName] = N'$activeMarker' AND [IsActive] = 1), -1) AS [activeProductStock],
    ISNULL((SELECT TOP (1) [Price] FROM [dbo].[Products] WHERE [ProductName] = N'$activeMarker' AND [IsActive] = 1), 0) AS [activeProductPrice],
    ISNULL((SELECT TOP (1) [ProductId] FROM [dbo].[Products] WHERE [ProductName] = N'$inactiveMarker' AND [IsActive] = 0), 0) AS [inactiveProductId],
    (SELECT COUNT(*) FROM [dbo].[Products]) AS [totalProducts],
    ISNULL(HAS_PERMS_BY_NAME(N'dbo.Users', N'OBJECT', N'SELECT'), 0) AS [users],
    (SELECT COUNT(*) FROM [dbo].[Orders]) AS [orders],
    (SELECT COUNT(*) FROM [dbo].[OrderDetails]) AS [orderDetails]
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
"@
}

function Assert-CdSqlVerificationResult {
    param(
        [Parameter(Mandatory)][string]$JsonOutput,
        [Parameter(Mandatory)][string]$ExpectedMachineName
    )
    $defaults = Get-CdSqlFixtureDefaults
    Assert-Cd (-not [string]::IsNullOrWhiteSpace($JsonOutput)) 'Empty verification response from SQL fixture.'
    $cleanJson = ($JsonOutput -replace '[\r\n]+', '').Trim()
    $v = $cleanJson | ConvertFrom-Json -ErrorAction Stop

    Assert-Cd ($v.database -ceq $defaults.DatabaseName) "Verified database '$($v.database)' is not '$($defaults.DatabaseName)'."
    Assert-Cd ($v.machineName -ceq $ExpectedMachineName) "Verified SQL MachineName '$($v.machineName)' does not match rehearsal container '$ExpectedMachineName'."
    Assert-Cd ($v.login -ceq $defaults.AppLogin) "Verification must run under '$($defaults.AppLogin)', got '$($v.login)'."
    Assert-Cd ([int]$v.businessDbPresent -eq 0) 'Business or acceptance database detected on target SQL instance.'
    Assert-Cd ([int]$v.isSysAdmin -eq 0 -and [int]$v.hasBusinessAccess -eq 0) 'Runtime login cd_rehearsal has forbidden server or business DB access.'
    Assert-Cd ([int]$v.canSelectProducts -eq 1 -and
        [int]$v.canInsertProducts -eq 0 -and
        [int]$v.canUpdateProducts -eq 0 -and
        [int]$v.canDeleteProducts -eq 0 -and
        [int]$v.canAlterDatabase -eq 0 -and
        [int]$v.canUpdateMigrations -eq 0) 'Runtime login cd_rehearsal permissions violate least-privilege SELECT-only policy.'
    Assert-Cd ([int]$v.migrations -eq 2) "Expected 2 applied migrations in __EFMigrationsHistory, found $($v.migrations)."
    Assert-Cd ([int]$v.roles -eq 2 -and [int]$v.brands -eq 1 -and [int]$v.categories -eq 1) 'Seeded Roles, Brand or Category count mismatch.'
    Assert-Cd ([int]$v.activeProductId -gt 0 -and
        [int]$v.activeProductStock -eq $defaults.ActiveStock -and
        [decimal]$v.activeProductPrice -eq [decimal]$defaults.ActivePrice) 'Active catalog fixture marker S353E-FIXTURE-PHONE verification failed.'
    Assert-Cd ([int]$v.inactiveProductId -gt 0 -and [int]$v.inactiveProductId -ne [int]$v.activeProductId -and
        [int]$v.totalProducts -eq 2) 'Inactive catalog fixture marker verification failed.'
    Assert-Cd ([int]$v.users -eq 0 -and [int]$v.orders -eq 0 -and [int]$v.orderDetails -eq 0) 'SQL fixture must have zero Users, Orders and OrderDetails.'

    [pscustomobject]@{
        Database          = [string]$v.database
        MachineName       = [string]$v.machineName
        Login             = [string]$v.login
        Migrations        = [int]$v.migrations
        Roles             = [int]$v.roles
        Brands            = [int]$v.brands
        Categories        = [int]$v.categories
        ActiveProductId   = [int]$v.activeProductId
        ActiveMarker      = $defaults.ActiveMarker
        ActiveStock       = [int]$v.activeProductStock
        InactiveProductId = [int]$v.inactiveProductId
        InactiveMarker    = $defaults.InactiveMarker
        TotalProducts     = [int]$v.totalProducts
        Users             = [int]$v.users
        Orders            = [int]$v.orders
        OrderDetails      = [int]$v.orderDetails
    }
}

function Get-CdLiveSqlFixtureTopology {
    param([scriptblock]$DockerRunner)
    $defaults = Get-CdSqlFixtureDefaults
    $invokeDocker = if ($DockerRunner) {
        $DockerRunner
    } else {
        {
            param([string[]]$Arguments)
            $prevEap = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            try {
                $out = & docker --context desktop-linux @Arguments 2>$null
                $code = $LASTEXITCODE
            } finally {
                $ErrorActionPreference = $prevEap
            }
            if ($code -ne 0) { throw "Docker $($Arguments[0]) failed (exit $code)." }
            $out
        }
    }

    $dbIds = @(& $invokeDocker @('ps', '-aq', '--filter', "label=com.docker.compose.project=$($defaults.Project)", '--filter', "label=com.docker.compose.service=db"))
    Assert-Cd ($dbIds.Count -eq 1) 'Expected exactly one isolated db container in project phonestore-cd-rehearsal.'
    $db = @(& $invokeDocker @('inspect', $dbIds[0]) | ConvertFrom-Json)[0]

    $apiIds = @(& $invokeDocker @('ps', '-aq', '--filter', "label=com.docker.compose.project=$($defaults.Project)", '--filter', "label=com.docker.compose.service=api"))
    $api = if ($apiIds.Count -eq 1) { @(& $invokeDocker @('inspect', $apiIds[0]) | ConvertFrom-Json)[0] } else { $null }

    $webIds = @(& $invokeDocker @('ps', '-aq', '--filter', "label=com.docker.compose.project=$($defaults.Project)", '--filter', "label=com.docker.compose.service=web"))
    $web = if ($webIds.Count -eq 1) { @(& $invokeDocker @('inspect', $webIds[0]) | ConvertFrom-Json)[0] } else { $null }

    $net = @(& $invokeDocker @('network', 'inspect', $defaults.NetworkName) | ConvertFrom-Json)[0]
    $vol = @(& $invokeDocker @('volume', 'inspect', $defaults.VolumeName) | ConvertFrom-Json)[0]

    $bizIds = @()
    try {
        $bizIds = @(& $invokeDocker @('inspect', '--format', '{{.Id}}', 'phonestore-restore-c2-db-1', 'phonestore-restore-c2-api-1', 'phonestore-restore-c2-web-1'))
    } catch { }

    [pscustomobject]@{
        DbContainer          = $db
        ApiContainer         = $api
        WebContainer         = $web
        Network              = $net
        Volume               = $vol
        BusinessContainerIds = $bizIds
    }
}

function Invoke-CdSqlFixtureProvisioning {
    param(
        [ValidateSet('ValidateOnly','Verify','Provision')][string]$Mode = 'ValidateOnly',
        [hashtable]$Credentials,
        [string[]]$ForbiddenSecrets,
        $Topology,
        [scriptblock]$DockerRunner,
        [scriptblock]$SqlExecutor,
        [scriptblock]$ResourceGate,
        [scriptblock]$LogWriter,
        [switch]$ConfirmFixtureProvisioning
    )
    $defaults = Get-CdSqlFixtureDefaults
    if ($Mode -eq 'Provision') {
        Assert-Cd ([bool]$ConfirmFixtureProvisioning) 'Provision mode requires -ConfirmFixtureProvisioning.'
    }
    Write-CdSqlFixtureLog -Phase 'Start' -Status 'INFO' -Detail "mode=$Mode project=$($defaults.Project) db=$($defaults.DatabaseName) host=$($defaults.SqlHost)" -LogWriter $LogWriter

    $creds = if ($Credentials) {
        $Credentials
    } else {
        @{
            SqlPassword       = [Environment]::GetEnvironmentVariable('CD_REHEARSAL_SQL_PASSWORD')
            ProvisionPassword = [Environment]::GetEnvironmentVariable('CD_REHEARSAL_PROVISION_PASSWORD')
            AppPassword       = [Environment]::GetEnvironmentVariable('CD_REHEARSAL_APP_PASSWORD')
        }
    }
    $forbidden = if ($null -ne $ForbiddenSecrets) {
        $ForbiddenSecrets
    } else {
        @(Get-CdForbiddenBusinessSecrets -DockerRunner $DockerRunner)
    }

    Assert-CdSqlFixtureCredentials -Credentials $creds -ForbiddenSecrets $forbidden -RequireProvisionPassword:($Mode -eq 'Provision') | Out-Null
    Write-CdSqlFixtureLog -Phase 'Credentials' -Status 'PASS' -Detail 'Rehearsal credentials validated and verified distinct from business secrets.' -LogWriter $LogWriter

    $sampleConn = "Server=$($defaults.SqlHost);Database=$($defaults.DatabaseName);User Id=$($defaults.AppLogin);Password=$($creds['AppPassword']);Encrypt=True;TrustServerCertificate=True;Max Pool Size=5"
    Test-CdSqlFixtureConnectionString -ConnectionString $sampleConn -ExpectedHost $defaults.SqlHost -ExpectedDatabase $defaults.DatabaseName -ExpectedUser $defaults.AppLogin | Out-Null
    Write-CdSqlFixtureLog -Phase 'ConnectionTarget' -Status 'PASS' -Detail "Target host=$($defaults.SqlHost) database=$($defaults.DatabaseName) user=$($defaults.AppLogin)" -LogWriter $LogWriter

    $effectiveTopology = if ($Topology) { $Topology } else { Get-CdLiveSqlFixtureTopology -DockerRunner $DockerRunner }
    $verifiedTarget = Assert-CdSqlFixtureTopology -Topology $effectiveTopology
    Write-CdSqlFixtureLog -Phase 'Topology' -Status 'PASS' -Detail "Container=$($verifiedTarget.ContainerHostname) volume=$($verifiedTarget.VolumeName) network=$($verifiedTarget.NetworkName)" -LogWriter $LogWriter

    if ($Mode -eq 'ValidateOnly') {
        $scripts = [pscustomobject]@{
            Preflight = Get-CdSqlPreflightScript -ExpectedMachineName $verifiedTarget.ContainerHostname
            Bootstrap = if ($creds['ProvisionPassword']) {
                Get-CdSqlBootstrapScript -ExpectedMachineName $verifiedTarget.ContainerHostname -ProvisionPassword $creds['ProvisionPassword'] -AppPassword $creds['AppPassword']
            } else { $null }
            MigrateAndSeed = Get-CdSqlMigrationAndSeedScript -ExpectedMachineName $verifiedTarget.ContainerHostname
            Lockdown = Get-CdSqlLockdownScript -ExpectedMachineName $verifiedTarget.ContainerHostname
            Verify = Get-CdSqlVerificationScript -ExpectedMachineName $verifiedTarget.ContainerHostname
        }
        Write-CdSqlFixtureLog -Phase 'ValidateOnly' -Status 'PASS' -Detail 'Fixture isolation guards and SQL scripts validated without executing provisioning.' -LogWriter $LogWriter
        return [pscustomobject]@{
            Mode    = 'ValidateOnly'
            Target  = $verifiedTarget
            Scripts = $scripts
        }
    }

    $runSql = if ($SqlExecutor) {
        $SqlExecutor
    } else {
        {
            param([string]$ContainerId, [string]$Database, [string]$Login, [string]$Password, [string]$SqlBatch)
            $prevEap = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            try {
                $out = $SqlBatch | & docker --context desktop-linux exec -i -e "SQLCMDPASSWORD=$Password" -e "SQLCMDMAXVARTYPEWIDTH=4000" $ContainerId `
                    /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -d $Database -U $Login -I -C -b -l 5 -V 11 -h -1 -W 2>$null
                $code = $LASTEXITCODE
            } finally {
                $ErrorActionPreference = $prevEap
            }
            if ($code -ne 0) { throw "sqlcmd batch in database '$Database' as '$Login' failed (exit $code)." }
            ($out -join "`n").Trim()
        }
    }

    if ($ResourceGate) {
        & $ResourceGate 'SqlProvision' 'Pre' | Out-Null
    }
    $preflightSql = Get-CdSqlPreflightScript -ExpectedMachineName $verifiedTarget.ContainerHostname
    $preflightOut = & $runSql $verifiedTarget.ContainerId 'master' 'sa' $creds['SqlPassword'] $preflightSql
    Assert-Cd ($preflightOut -cmatch "^PREFLIGHT_OK\|$([Regex]::Escape($verifiedTarget.ContainerHostname))\|NULL\|NULL$") 'SQL preflight handshake did not confirm isolated rehearsal container.'
    Write-CdSqlFixtureLog -Phase 'SqlPreflight' -Status 'PASS' -Detail "SQL handshake matched container hostname $($verifiedTarget.ContainerHostname) with zero business DBs." -LogWriter $LogWriter

    if ($Mode -eq 'Provision') {
        Assert-Cd ([bool]$ConfirmFixtureProvisioning) 'Provision mode requires -ConfirmFixtureProvisioning.'
        $bootstrapSql = Get-CdSqlBootstrapScript -ExpectedMachineName $verifiedTarget.ContainerHostname -ProvisionPassword $creds['ProvisionPassword'] -AppPassword $creds['AppPassword']
        $bootstrapOut = & $runSql $verifiedTarget.ContainerId 'master' 'sa' $creds['SqlPassword'] $bootstrapSql
        Assert-Cd ($bootstrapOut -cmatch "BOOTSTRAP_OK\|$([Regex]::Escape($defaults.DatabaseName))$") 'SQL bootstrap phase did not complete as expected.'
        Write-CdSqlFixtureLog -Phase 'Bootstrap' -Status 'PASS' -Detail 'Fixture database and dedicated logins prepared.' -LogWriter $LogWriter

        $migrateSeedSql = Get-CdSqlMigrationAndSeedScript -ExpectedMachineName $verifiedTarget.ContainerHostname
        $migrateSeedOut = & $runSql $verifiedTarget.ContainerId $defaults.DatabaseName $defaults.ProvisionLogin $creds['ProvisionPassword'] $migrateSeedSql
        Assert-Cd ($migrateSeedOut -cmatch 'MIGRATE_SEED_OK$') 'SQL migration and synthetic catalog seed phase did not complete as expected.'
        Write-CdSqlFixtureLog -Phase 'MigrateAndSeed' -Status 'PASS' -Detail 'Migrations 20261001150857_InitialCreate + 20261102122856_EnforceSingleAdmin and synthetic catalog seed applied.' -LogWriter $LogWriter

        $lockdownSql = Get-CdSqlLockdownScript -ExpectedMachineName $verifiedTarget.ContainerHostname
        $lockdownOut = & $runSql $verifiedTarget.ContainerId $defaults.DatabaseName 'sa' $creds['SqlPassword'] $lockdownSql
        Assert-Cd ($lockdownOut -cmatch 'LOCKDOWN_OK$') 'SQL permission lockdown phase did not complete as expected.'
        Write-CdSqlFixtureLog -Phase 'Lockdown' -Status 'PASS' -Detail 'Granted SELECT-only to cd_rehearsal and disabled cd_rehearsal_provision.' -LogWriter $LogWriter
    }

    $verifySql = Get-CdSqlVerificationScript -ExpectedMachineName $verifiedTarget.ContainerHostname
    $verifyOut = & $runSql $verifiedTarget.ContainerId $defaults.DatabaseName $defaults.AppLogin $creds['AppPassword'] $verifySql
    $verified = Assert-CdSqlVerificationResult -JsonOutput $verifyOut -ExpectedMachineName $verifiedTarget.ContainerHostname
    if ($ResourceGate) {
        & $ResourceGate 'SqlProvision' 'Post' | Out-Null
    }
    Write-CdSqlFixtureLog -Phase 'Verify' -Status 'PASS' -Detail "migrations=$($verified.Migrations) roles=$($verified.Roles) brands=$($verified.Brands) categories=$($verified.Categories) activeId=$($verified.ActiveProductId) stock=$($verified.ActiveStock) users=$($verified.Users) orders=$($verified.Orders)" -LogWriter $LogWriter

    [pscustomobject]@{
        Mode         = $Mode
        Target       = $verifiedTarget
        Verification = $verified
    }
}

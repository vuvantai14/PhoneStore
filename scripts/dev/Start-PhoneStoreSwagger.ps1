[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)][int]$Port = 5026,
    [string]$StateDirectory = (Join-Path $env:LOCALAPPDATA 'PhoneStore-DevSwagger'),
    [switch]$LaunchBrowser
)

$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$composeFile = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'docker-compose.swagger.yml'))
$project = 'phonestore-dev-swagger'
$apiContainerName = 'phonestore-dev-swagger-api-1'
$dbContainerName = 'phonestore-dev-swagger-db-1'

. (Join-Path $repoRoot 'scripts\cd\Rehearsal.Engine.ps1')
. (Join-Path $repoRoot 'scripts\cd\Rehearsal.ResourceGate.ps1')
. (Join-Path $repoRoot 'scripts\cd\Rehearsal.SqlFixture.ps1')

if ($Port -in @(5015, 5025, 1433, 14334)) {
    throw "Port $Port is reserved for PhoneStore business services. Use port 5026."
}

function Invoke-SwaggerDocker {
    param([string[]]$Arguments, [switch]$AllowFailure, [string]$StdInText)
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        if ($PSBoundParameters.ContainsKey('StdInText')) {
            $out = $StdInText | & docker --context desktop-linux @Arguments 2>&1
        } else {
            $out = & docker --context desktop-linux @Arguments 2>&1
        }
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $prevEap
    }
    if (-not $AllowFailure -and $code -ne 0) {
        throw "Docker $($Arguments[0]) failed (exit $code): $($out -join ' ')"
    }
    [pscustomobject]@{ ExitCode = $code; Output = ($out -join "`n") }
}

function New-SwaggerRandomPassword([int]$ByteLength = 32) {
    $bytes = New-Object byte[] $ByteLength
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $b64 = [Convert]::ToBase64String($bytes).Replace('+', 'A').Replace('/', 'B').Replace('=', '')
    return 'Swg9!' + $b64.Substring(0, [Math]::Min(36, $b64.Length)) + '#d7'
}

function New-SwaggerRandomSigningKey {
    $bytes = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return [Convert]::ToBase64String($bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=')
}

function New-IdentityV3PasswordHash([string]$Password) {
    $salt = New-Object byte[] 16
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
    $pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
        $Password, $salt, 100000, [System.Security.Cryptography.HashAlgorithmName]::SHA512)
    try { $subkey = $pbkdf2.GetBytes(32) } finally { $pbkdf2.Dispose() }
    $out = New-Object byte[] (13 + 16 + 32)
    $out[0] = 0x01; $out[4] = 2; $out[6] = 1; $out[7] = 0x86; $out[8] = 0xA0; $out[12] = 16
    [Buffer]::BlockCopy($salt, 0, $out, 13, 16)
    [Buffer]::BlockCopy($subkey, 0, $out, 29, 32)
    return [Convert]::ToBase64String($out)
}

function Wait-ContainerHealthy([string]$ContainerName, [int]$MaxAttempts = 30) {
    for ($i = 1; $i -le $MaxAttempts; $i++) {
        $res = Invoke-SwaggerDocker -AllowFailure @('inspect', '--format', '{{.State.Running}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}', $ContainerName)
        if ($res.ExitCode -eq 0 -and $res.Output.Trim() -ceq 'true|healthy') {
            return $true
        }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Test-PortInUse([int]$CheckPort) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $iar = $client.BeginConnect('127.0.0.1', $CheckPort, $null, $null)
        if ($iar.AsyncWaitHandle.WaitOne(400, $false)) {
            $client.EndConnect($iar)
            return $true
        }
        return $false
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

Write-Host '[1/6] Checking business stack invariants (phonestore-restore-c2)...'
$bizBefore = Get-CdBusinessInvariantsSnapshot

Write-Host "[2/6] Checking port $Port and existing '$project' containers..."
$existingApi = Invoke-SwaggerDocker -AllowFailure @('inspect', '--format', '{{.State.Running}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}|{{.Name}}', $apiContainerName)
$apiAlreadyHealthy = ($existingApi.ExitCode -eq 0 -and $existingApi.Output.Trim() -ceq "true|healthy|/$apiContainerName")

if (Test-PortInUse -CheckPort $Port) {
    if ($apiAlreadyHealthy) {
        Write-Host "[PASS] PhoneStore Development Swagger is already running and healthy."
        Write-Host "  Swagger UI : http://localhost:$Port/swagger"
        Write-Host "  OpenAPI v1 : http://localhost:$Port/swagger/v1/swagger.json"
        if ($LaunchBrowser) { Start-Process "http://localhost:$Port/swagger" }
        exit 0
    }
    $existingProjectApi = ($existingApi.ExitCode -eq 0 -and $existingApi.Output.Trim().EndsWith("|/$apiContainerName"))
    if (-not $existingProjectApi) {
        throw "BLOCKED: Port 127.0.0.1:$Port is currently in use by another process outside '$project'. Refusing to kill unknown processes."
    }
}

Write-Host '[3/6] Preparing isolated Development secrets outside Git...'
if (-not (Test-Path -LiteralPath $StateDirectory)) {
    New-Item -ItemType Directory -Path $StateDirectory -Force | Out-Null
}
$secretsFile = Join-Path $StateDirectory 'swagger-dev.secrets.json'
$secrets = $null
if (Test-Path -LiteralPath $secretsFile) {
    try {
        $secrets = (Get-Content -LiteralPath $secretsFile -Raw -Encoding UTF8) | ConvertFrom-Json
    } catch {
        $secrets = $null
    }
}

if ($null -eq $secrets -or [string]::IsNullOrWhiteSpace($secrets.SaPassword) -or [string]::IsNullOrWhiteSpace($secrets.JwtSigningKey)) {
    $secrets = [ordered]@{
        SaPassword        = (New-SwaggerRandomPassword)
        AppPassword       = (New-SwaggerRandomPassword)
        JwtSigningKey     = (New-SwaggerRandomSigningKey)
        DevAdminEmail     = 'admin.dev@phonestore.local'
        DevAdminPassword  = (New-SwaggerRandomPassword 18)
        DevCustomerEmail  = 'customer.dev@phonestore.local'
        DevCustomerPassword = (New-SwaggerRandomPassword 18)
        CreatedAtUtc      = ([DateTime]::UtcNow.ToString('o'))
    }
    [IO.File]::WriteAllText($secretsFile, (($secrets | ConvertTo-Json -Depth 4) + "`n"), (New-Object System.Text.UTF8Encoding($false)))
    $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    & icacls $secretsFile /inheritance:r /grant:r "${currentUser}:(F)" | Out-Null
}

$forbiddenSecrets = Get-CdForbiddenBusinessSecrets -RepoRoot $repoRoot
foreach ($candidate in @($secrets.SaPassword, $secrets.AppPassword, $secrets.JwtSigningKey, $secrets.DevAdminPassword, $secrets.DevCustomerPassword)) {
    if ($forbiddenSecrets.Contains([string]$candidate)) {
        throw 'BLOCKED: A development secret collided with a forbidden business secret.'
    }
}

$env:SWAGGER_DEV_SA_PASSWORD = [string]$secrets.SaPassword
$env:SWAGGER_DEV_APP_PASSWORD = [string]$secrets.AppPassword
$env:SWAGGER_DEV_JWT_KEY = [string]$secrets.JwtSigningKey

try {
    $existingDb = Invoke-SwaggerDocker -AllowFailure @('inspect', '--format', '{{.State.Running}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}', $dbContainerName)
    $dbAlreadyHealthy = ($existingDb.ExitCode -eq 0 -and $existingDb.Output.Trim() -ceq 'true|healthy')

    Write-Host '[4/6] Checking Phased ResourceGate & starting isolated SQL Server 2022 fixture...'
    $gatePhase = if ($dbAlreadyHealthy) { 'StartApi' } else { 'SqlColdStart' }
    $gate = Assert-CdResourceGate -Phase $gatePhase -Stage 'Pre' -BaselineBusinessSnapshot $bizBefore
    Write-Host ("  ResourceGate ({0}/Pre) PASS: WinFree={1:N2} GiB, WslAvail={2:N2} GiB, Swap={3:N0} MiB" -f `
        $gatePhase, $gate.Telemetry.WindowsFreeGiB, $gate.Telemetry.WslAvailableGiB, $gate.Telemetry.WslSwapUsedMiB)

    if (-not $dbAlreadyHealthy) {
        Invoke-SwaggerDocker @('compose', '--env-file', 'NUL', '-p', $project, '-f', $composeFile, 'up', '-d', '--no-deps', '--no-build', '--pull', 'never', 'db') | Out-Null
        if (-not (Wait-ContainerHealthy -ContainerName $dbContainerName -MaxAttempts 30)) {
            throw "Isolated SQL fixture container '$dbContainerName' did not become healthy in time."
        }
    }

    Write-Host '[5/6] Idempotently provisioning PhoneStoreDB_SwaggerDev schema, 3 migrations & sample data...'
    $safeAppPwd = ([string]$secrets.AppPassword).Replace("'", "''")
    $adminHash = (New-IdentityV3PasswordHash ([string]$secrets.DevAdminPassword)).Replace("'", "''")
    $custHash = (New-IdentityV3PasswordHash ([string]$secrets.DevCustomerPassword)).Replace("'", "''")
    $adminEmail = ([string]$secrets.DevAdminEmail).Replace("'", "''")
    $custEmail = ([string]$secrets.DevCustomerEmail).Replace("'", "''")

    $provisionSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_ID(N'PhoneStoreDB') IS NOT NULL
    THROW 54000, 'Business database PhoneStoreDB detected on fixture instance! Aborting.', 1;

IF DB_ID(N'PhoneStoreDB_SwaggerDev') IS NULL
    CREATE DATABASE [PhoneStoreDB_SwaggerDev];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'swagger_dev_app')
    CREATE LOGIN [swagger_dev_app] WITH PASSWORD = N'$safeAppPwd', DEFAULT_DATABASE = [PhoneStoreDB_SwaggerDev], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [swagger_dev_app] WITH PASSWORD = N'$safeAppPwd';
GO

USE [PhoneStoreDB_SwaggerDev];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'swagger_dev_app')
    CREATE USER [swagger_dev_app] FOR LOGIN [swagger_dev_app];
ALTER ROLE [db_datareader] ADD MEMBER [swagger_dev_app];
ALTER ROLE [db_datawriter] ADD MEMBER [swagger_dev_app];

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261001150857_InitialCreate')
BEGIN
    CREATE TABLE [Brands] (
        [BrandId] int NOT NULL IDENTITY,
        [BrandName] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Brands] PRIMARY KEY ([BrandId])
    );
    CREATE TABLE [Categories] (
        [CategoryId] int NOT NULL IDENTITY,
        [CategoryName] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryId])
    );
    CREATE TABLE [Roles] (
        [RoleId] int NOT NULL IDENTITY,
        [RoleName] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
    );
    CREATE TABLE [Products] (
        [ProductId] int NOT NULL IDENTITY,
        [ProductName] nvarchar(200) NOT NULL,
        [BrandId] int NOT NULL,
        [CategoryId] int NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [StockQuantity] int NOT NULL DEFAULT 0,
        [RAM] nvarchar(50) NULL,
        [Storage] nvarchar(50) NULL,
        [Chip] nvarchar(100) NULL,
        [Screen] nvarchar(150) NULL,
        [Camera] nvarchar(150) NULL,
        [Battery] nvarchar(100) NULL,
        [OperatingSystem] nvarchar(100) NULL,
        [Description] nvarchar(2000) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([ProductId]),
        CONSTRAINT [CK_Products_Price] CHECK ([Price] >= 0),
        CONSTRAINT [CK_Products_StockQuantity] CHECK ([StockQuantity] >= 0),
        CONSTRAINT [FK_Products_Brands_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brands] ([BrandId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([CategoryId]) ON DELETE NO ACTION
    );
    CREATE TABLE [Users] (
        [UserId] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Address] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]) ON DELETE NO ACTION
    );
    CREATE TABLE [ProductImages] (
        [ImageId] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [IsPrimary] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DisplayOrder] int NOT NULL DEFAULT 0,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([ImageId]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE CASCADE
    );
    CREATE TABLE [Orders] (
        [OrderId] int NOT NULL IDENTITY,
        [OrderCode] nvarchar(30) NOT NULL,
        [CheckoutToken] nvarchar(32) NULL,
        [UserId] int NOT NULL,
        [OrderDate] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(30) NOT NULL DEFAULT N'Pending',
        [ReceiverName] nvarchar(150) NOT NULL,
        [ReceiverPhone] nvarchar(20) NOT NULL,
        [ShippingAddress] nvarchar(500) NOT NULL,
        [Note] nvarchar(500) NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([OrderId]),
        CONSTRAINT [CK_Orders_Status] CHECK ([Status] IN (N'Pending', N'Confirmed', N'Shipping', N'Completed', N'Cancelled')),
        CONSTRAINT [CK_Orders_TotalAmount] CHECK ([TotalAmount] >= 0),
        CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
    );
    CREATE TABLE [OrderDetails] (
        [OrderDetailId] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([OrderDetailId]),
        CONSTRAINT [CK_OrderDetails_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_OrderDetails_UnitPrice] CHECK ([UnitPrice] >= 0),
        CONSTRAINT [FK_OrderDetails_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([OrderId]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderDetails_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE NO ACTION
    );

    SET IDENTITY_INSERT [Roles] ON;
    INSERT INTO [Roles] ([RoleId], [RoleName]) VALUES (1, N'Admin'), (2, N'Customer');
    SET IDENTITY_INSERT [Roles] OFF;

    CREATE UNIQUE INDEX [IX_Brands_BrandName] ON [Brands] ([BrandName]);
    CREATE UNIQUE INDEX [IX_Categories_CategoryName] ON [Categories] ([CategoryName]);
    CREATE INDEX [IX_OrderDetails_OrderId] ON [OrderDetails] ([OrderId]);
    CREATE INDEX [IX_OrderDetails_ProductId] ON [OrderDetails] ([ProductId]);
    CREATE UNIQUE INDEX [IX_Orders_OrderCode] ON [Orders] ([OrderCode]);
    CREATE UNIQUE INDEX [UX_Orders_UserId_CheckoutToken] ON [Orders] ([UserId], [CheckoutToken]) WHERE [CheckoutToken] IS NOT NULL;
    CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);
    CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
    CREATE INDEX [IX_Products_BrandId] ON [Products] ([BrandId]);
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
    CREATE UNIQUE INDEX [IX_Roles_RoleName] ON [Roles] ([RoleName]);
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
    CREATE INDEX [IX_Users_RoleId] ON [Users] ([RoleId]);

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001150857_InitialCreate', N'10.0.15');
END;

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261102122856_EnforceSingleAdmin')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Users_SingleAdmin] ON [Users] ([RoleId]) WHERE [RoleId] = 1');
    EXEC(N'ALTER TABLE [Roles] ADD CONSTRAINT [CK_Roles_AdminIdentity] CHECK (([RoleId] = 1 AND [RoleName] = ''Admin'') OR ([RoleId] <> 1 AND [RoleName] <> ''Admin''))');
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102122856_EnforceSingleAdmin', N'10.0.15');
END;

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261121044020_AddApiRefreshTokens')
BEGIN
    CREATE TABLE [ApiRefreshTokens] (
        [ApiRefreshTokenId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [FamilyId] nvarchar(32) NOT NULL,
        [TokenHash] nvarchar(64) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        [RevokedReason] nvarchar(64) NULL,
        [ReplacedByTokenHash] nvarchar(64) NULL,
        CONSTRAINT [PK_ApiRefreshTokens] PRIMARY KEY ([ApiRefreshTokenId]),
        CONSTRAINT [FK_ApiRefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_ApiRefreshTokens_FamilyId] ON [ApiRefreshTokens] ([FamilyId]);
    CREATE INDEX [IX_ApiRefreshTokens_UserId] ON [ApiRefreshTokens] ([UserId]);
    CREATE UNIQUE INDEX [UX_ApiRefreshTokens_TokenHash] ON [ApiRefreshTokens] ([TokenHash]);
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261121044020_AddApiRefreshTokens', N'10.0.15');
END;

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [RoleId] = 1)
    INSERT INTO [Users] ([RoleId], [FullName], [Email], [Phone], [PasswordHash], [Address], [IsActive], [CreatedAt])
    VALUES (1, N'Swagger Dev Admin', N'$adminEmail', N'0900005026', N'$adminHash', N'Dev Lab', 1, SYSUTCDATETIME());
ELSE
    UPDATE [Users] SET [Email] = N'$adminEmail', [PasswordHash] = N'$adminHash', [IsActive] = 1 WHERE [RoleId] = 1;

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = N'$custEmail')
    INSERT INTO [Users] ([RoleId], [FullName], [Email], [Phone], [PasswordHash], [Address], [IsActive], [CreatedAt])
    VALUES (2, N'Swagger Dev Customer', N'$custEmail', N'0900005027', N'$custHash', N'123 Dev Street', 1, SYSUTCDATETIME());
ELSE
    UPDATE [Users] SET [PasswordHash] = N'$custHash', [IsActive] = 1 WHERE [Email] = N'$custEmail';

IF NOT EXISTS (SELECT 1 FROM [Brands])
BEGIN
    INSERT INTO [Brands] ([BrandName], [Description], [IsActive]) VALUES
      (N'Apple (Dev Fixture)', N'Isolated Swagger Development Brand', 1),
      (N'Samsung (Dev Fixture)', N'Isolated Swagger Development Brand', 1);
END;

IF NOT EXISTS (SELECT 1 FROM [Categories])
BEGIN
    INSERT INTO [Categories] ([CategoryName], [Description], [IsActive]) VALUES
      (N'Flagship (Dev Fixture)', N'Isolated Swagger Development Category', 1),
      (N'Mid-Range (Dev Fixture)', N'Isolated Swagger Development Category', 1);
END;

IF NOT EXISTS (SELECT 1 FROM [Products])
BEGIN
    DECLARE @bApple INT = (SELECT TOP 1 [BrandId] FROM [Brands] ORDER BY [BrandId]);
    DECLARE @bSamsung INT = (SELECT TOP 1 [BrandId] FROM [Brands] ORDER BY [BrandId] DESC);
    DECLARE @cFlag INT = (SELECT TOP 1 [CategoryId] FROM [Categories] ORDER BY [CategoryId]);
    INSERT INTO [Products] ([ProductName], [BrandId], [CategoryId], [Price], [StockQuantity], [RAM], [Storage], [Chip], [Screen], [OperatingSystem], [Description], [IsActive])
    VALUES
      (N'iPhone 16 Pro Dev Fixture', @bApple, @cFlag, 28990000, 10, N'8 GB', N'256 GB', N'A18 Pro', N'6.3 OLED', N'iOS 18', N'Swagger Development synthetic sample', 1),
      (N'Galaxy S24 Ultra Dev Fixture', @bSamsung, @cFlag, 27990000, 8, N'12 GB', N'256 GB', N'Snapdragon 8 Gen 3', N'6.8 AMOLED', N'Android 14', N'Swagger Development synthetic sample', 1),
      (N'iPhone 15 Dev Fixture', @bApple, @cFlag, 19990000, 15, N'6 GB', N'128 GB', N'A16 Bionic', N'6.1 OLED', N'iOS 17', N'Swagger Development synthetic sample', 1);
END;
"@

    Invoke-SwaggerDocker @('exec', '-i', $dbContainerName, 'bash', '-c', 'SQLCMDPASSWORD=$MSSQL_SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -I -W') -StdInText $provisionSql | Out-Null

    Write-Host "[6/6] Starting Development API container on http://127.0.0.1:$Port ..."
    Invoke-SwaggerDocker @('compose', '--env-file', 'NUL', '-p', $project, '-f', $composeFile, 'up', '-d', '--no-deps', '--no-build', '--pull', 'never', 'api') | Out-Null
    if (-not (Wait-ContainerHealthy -ContainerName $apiContainerName -MaxAttempts 25)) {
        throw "Development API container '$apiContainerName' did not become healthy in time."
    }

    $swaggerUiResp = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/swagger/index.html" -UseBasicParsing -TimeoutSec 10
    $openApiResp = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/swagger/v1/swagger.json" -UseBasicParsing -TimeoutSec 10
    $readyResp = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/api/health/ready" -UseBasicParsing -TimeoutSec 10
    if ([int]$swaggerUiResp.StatusCode -ne 200 -or [int]$openApiResp.StatusCode -ne 200 -or [int]$readyResp.StatusCode -ne 200) {
        throw 'Swagger UI, OpenAPI JSON or readiness endpoint did not return HTTP 200.'
    }

    $bizAfter = Get-CdBusinessInvariantsSnapshot
    if ($bizAfter.Signature -cne $bizBefore.Signature) {
        throw 'Business stack invariant drift detected after starting Swagger Development fixture!'
    }

    Write-Host ''
    Write-Host '[PASS] PhoneStore Development Swagger UI is running on port 5026 (100% isolated from business).'
    Write-Host "  Swagger UI        : http://localhost:$Port/swagger"
    Write-Host "  OpenAPI JSON (v1) : http://localhost:$Port/swagger/v1/swagger.json"
    Write-Host "  Health Ready      : http://localhost:$Port/api/health/ready"
    Write-Host "  Dev State / Auth  : $secretsFile (ACL restricted to current Windows user)"
    Write-Host '  To stop           : powershell -ExecutionPolicy Bypass -File scripts/dev/Stop-PhoneStoreSwagger.ps1'

    if ($LaunchBrowser) {
        Start-Process "http://localhost:$Port/swagger"
    }
} finally {
    Remove-Item Env:SWAGGER_DEV_SA_PASSWORD, Env:SWAGGER_DEV_APP_PASSWORD, Env:SWAGGER_DEV_JWT_KEY -ErrorAction SilentlyContinue
}

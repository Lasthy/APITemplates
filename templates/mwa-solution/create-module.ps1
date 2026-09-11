param(
    [string]$Name
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Name)) {
    $Name = Read-Host "Enter the module name (e.g. Catalog, Orders, Identity)"
}

if ([string]::IsNullOrWhiteSpace($Name)) {
    Write-Error "Module name cannot be empty."
    exit 1
}

$solutionDir = $PSScriptRoot
$moduleDir = "$solutionDir/Modules/$Name"
$moduleCsproj = "$moduleDir/$Name.csproj"
$contractsDir = "$solutionDir/Modules/$Name.Contracts"
$contractsCsproj = "$contractsDir/$Name.Contracts.csproj"
$hostCsproj = "$solutionDir/Host/Host.csproj"

if (Test-Path $moduleCsproj) {
    Write-Error "Module '$Name' already exists at '$moduleDir'."
    exit 1
}

Write-Host "Creating module '$Name' (with companion '$Name.Contracts')..." -ForegroundColor Cyan
Push-Location $solutionDir
try {
    $specializedTemplates = @{
        "Identity" = "mwa-module-identity"
        "MultiTenancy" = "mwa-module-multitenancy"
        "Scheduling" = "mwa-module-scheduling"
        "Cms" = "mwa-module-cms"
        "Webhooks" = "mwa-module-webhooks"
    }

    if ($specializedTemplates.ContainsKey($Name)) {
        $templateName = $specializedTemplates[$Name]
        dotnet new $templateName
    } else {
        dotnet new mwa-module -n $Name
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to scaffold module '$Name'."
    }

    $slnFile = Get-ChildItem -Path $solutionDir -Filter "*.slnx" | Select-Object -First 1
    if (-not $slnFile) {
        $slnFile = Get-ChildItem -Path $solutionDir -Filter "*.sln" | Select-Object -First 1
    }

    Write-Host "Adding '$Name' and '$Name.Contracts' to solution under Modules folder..." -ForegroundColor Cyan
    if ($slnFile) {
        dotnet sln $slnFile.FullName add $moduleCsproj -s "Modules"
        if (Test-Path $contractsCsproj) {
            dotnet sln $slnFile.FullName add $contractsCsproj -s "Modules"
        }
    } else {
        dotnet sln add $moduleCsproj -s "Modules"
        if (Test-Path $contractsCsproj) {
            dotnet sln add $contractsCsproj -s "Modules"
        }
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to add projects to solution."
    }

    $testDir = "$solutionDir/Tests/$Name.Tests"
    $testCsproj = "$testDir/$Name.Tests.csproj"
    if (Test-Path $testCsproj) {
        Write-Host "Adding '$Name.Tests' to solution under Tests folder..." -ForegroundColor Cyan
        if ($slnFile) {
            dotnet sln $slnFile.FullName add $testCsproj -s "Tests"
        } else {
            dotnet sln add $testCsproj -s "Tests"
        }
    }

    if (Test-Path $hostCsproj) {
        Write-Host "Adding project reference to Host..." -ForegroundColor Cyan
        dotnet add $hostCsproj reference $moduleCsproj
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to add project reference to Host."
        }
    } else {
        Write-Warning "Host project not found at '$hostCsproj'. Skipping reference addition."
    }

    $instanceSettings = "$solutionDir/Host/appsettings.Instance.json"
    if (Test-Path $instanceSettings) {
        try {
            $json = Get-Content $instanceSettings -Raw | ConvertFrom-Json
            if ($null -eq $json.ActiveModules) {
                $json | Add-Member -NotePropertyName "ActiveModules" -NotePropertyValue (New-Object PSObject)
            }
            $json.ActiveModules | Add-Member -NotePropertyName $Name -NotePropertyValue $true -Force
            $json | ConvertTo-Json -Depth 10 | Set-Content $instanceSettings
            Write-Host "Enabled '$Name' in Host/appsettings.Instance.json." -ForegroundColor Cyan
        } catch {
            Write-Warning "Could not automatically enable '$Name' in Host/appsettings.Instance.json: $_"
        }
    }

    Write-Host "Module '$Name' (and '$Name.Contracts') created and configured successfully!" -ForegroundColor Green
}
finally {
    Pop-Location
}

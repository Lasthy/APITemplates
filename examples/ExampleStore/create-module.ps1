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
    dotnet new mwa-module -n $Name
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to scaffold module with 'dotnet new mwa-module -n $Name'."
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

    if (Test-Path $hostCsproj) {
        Write-Host "Adding project reference to Host..." -ForegroundColor Cyan
        dotnet add $hostCsproj reference $moduleCsproj
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to add project reference to Host."
        }
    } else {
        Write-Warning "Host project not found at '$hostCsproj'. Skipping reference addition."
    }

    Write-Host "Module '$Name' (and '$Name.Contracts') created and configured successfully!" -ForegroundColor Green
    Write-Host "Remember to enable '$Name' in Host/appsettings.Instance.json if required." -ForegroundColor Yellow
}
finally {
    Pop-Location
}

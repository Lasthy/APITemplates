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
$hostCsproj = "$solutionDir/Host/Host.csproj"

if (Test-Path $moduleCsproj) {
    Write-Error "Module '$Name' already exists at '$moduleDir'."
    exit 1
}

Write-Host "Creating module '$Name'..." -ForegroundColor Cyan
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

    Write-Host "Adding '$Name' to solution under Modules folder..." -ForegroundColor Cyan
    if ($slnFile) {
        dotnet sln $slnFile.FullName add $moduleCsproj -s "Modules"
    } else {
        dotnet sln add $moduleCsproj -s "Modules"
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to add '$moduleCsproj' to solution."
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

    Write-Host "Module '$Name' created and configured successfully!" -ForegroundColor Green
    Write-Host "Remember to enable '$Name' in Host/settings.Instance.json if required." -ForegroundColor Yellow
}
finally {
    Pop-Location
}

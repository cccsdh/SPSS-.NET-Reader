param(
    [string]$SolutionPath = "SpssNet.sln",
    [string]$Configuration = "Debug"
)

Write-Host "Using solution: $SolutionPath"

if (-not (Test-Path $SolutionPath)) {
    Write-Error "Solution file '$SolutionPath' not found. Ensure this script is run from the repository root where SpssNet.sln is located."
    exit 1
}

Write-Host "Restoring NuGet packages..."
$restore = dotnet restore "$SolutionPath"
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet restore failed"
    exit $LASTEXITCODE
}

Write-Host "Building solution ($Configuration)..."
$build = dotnet build "$SolutionPath" -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet build failed"
    exit $LASTEXITCODE
}

Write-Host "Build succeeded."

param(
    [switch]$Fix,
    [switch]$SkipTests,
    [switch]$Strict
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $Root "HermesDesktop.sln"
$RoslynatorSeverity = if ($Strict) { "warning" } else { "error" }

Push-Location $Root
try {
    dotnet tool restore
    dotnet restore $Solution

    if ($Fix) {
        dotnet format $Solution --verbosity minimal
        dotnet roslynator fix $Solution
    }
    else {
        Get-ChildItem -Path $Root -Recurse -Filter "*.csproj" |
            Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\" } |
            ForEach-Object {
                dotnet roslynator analyze $_.FullName --severity-level $RoslynatorSeverity --verbosity minimal
            }
    }

    dotnet build $Solution --no-restore --verbosity minimal

    if (-not $SkipTests) {
        dotnet test $Solution --no-build --verbosity minimal
    }
}
finally {
    Pop-Location
}

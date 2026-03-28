$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$nugetApiKey = $env:NUGET_API_KEY
if ([string]::IsNullOrWhiteSpace($nugetApiKey)) {
    $nugetApiKey = "PASTE_YOUR_NUGET_API_KEY_HERE"
}

if ($nugetApiKey -eq "PASTE_YOUR_NUGET_API_KEY_HERE" -or [string]::IsNullOrWhiteSpace($nugetApiKey)) {
    Write-Error "Set environment variable NUGET_API_KEY, or edit `$nugetApiKey in a copy (see nuget-publish.local.ps1 in .gitignore)."
    exit 1
}

dotnet test LayeredChat.sln -c Release --verbosity minimal
dotnet pack LayeredChat.sln -c Release -o ./artifacts

$source = "https://api.nuget.org/v3/index.json"
Get-ChildItem -Path ./artifacts -Filter *.nupkg | ForEach-Object {
    dotnet nuget push $_.FullName --api-key $nugetApiKey --source $source --skip-duplicate
}
Get-ChildItem -Path ./artifacts -Filter *.snupkg -ErrorAction SilentlyContinue | ForEach-Object {
    dotnet nuget push $_.FullName --api-key $nugetApiKey --source $source --skip-duplicate
}

Write-Host "Done."

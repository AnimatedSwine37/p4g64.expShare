# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/p4g64.expShare/*" -Force -Recurse
dotnet publish "./p4g64.expShare.csproj" -c Release -o "$env:RELOADEDIIMODS/p4g64.expShare" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -o app/x64
dotnet publish -c Release -r win-arm64 -p:PublishSingleFile=true --self-contained true -o app/arm64

Get-ChildItem "app\" "*.exe" -Recurse |
foreach-object {
    Rename-Item -Path $_.FullName "$($_.BaseName)-$($_.Directory.Name)$($_.Extension)"
}

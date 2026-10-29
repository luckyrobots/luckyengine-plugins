# Hello Native

This example only references the public LuckyEngine.PluginSdk NuGet package.
Copy this directory anywhere; no other engine file is required.

```powershell
dotnet restore --source C:\path\to\sdk-packages --source https://api.nuget.org/v3/index.json
dotnet publish -c Release -r win-x64 --self-contained true -o package
Copy-Item lucky-plugin.yaml,README.md package
```

Zip the contents of `package` (not its parent directory). Use forward slashes
for ZIP entry names. Install the ZIP with the distributed plugin manager's
`--install-local <zip> --store <editor-plugin-packages-directory> --trust-local`.
Then open Tools → Install Plugin → Installed → Open. No workspace contribution is
needed for a plugin that only adds a panel.

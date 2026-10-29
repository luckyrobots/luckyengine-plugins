# Lucky Engine plugins

[`main.md`](main.md) lists available plugins. [`index.yaml`](index.yaml) is the
source of truth used by the editor. Plugin descriptions and documentation belong
in each plugin repository's README; the catalog does not duplicate them.

## Add your plugin

1. Publish a repository containing `lucky-plugin.yaml`, a README and platform
   release ZIPs with SHA-256 hashes.
2. Build using the public Lucky Engine Plugin SDK. Engine source access is not
   required or expected.
3. Add your plugin ID and repository URL to `index.yaml`.
4. Run `python -m pip install -r requirements.txt`, then
   `python scripts/generate_catalog.py`.
5. Submit a pull request containing `index.yaml` and generated `main.md`.

After review and merge, the entry appears when users refresh the editor catalog.
Users can also enter your repository URL directly, review it and choose to trust
and install it without waiting for catalog approval.

API 1 is a development preview. The first plugin, Pipers, is being tested against
the same public SDK as external plugins. Listing a repository does not certify its
code; plugins run with the user's operating-system permissions.

## Develop without engine source

The [public C# SDK](sdk/README.md) and [Hello Native example](sdk/Examples/HelloNative)
are included here. Build the SDK with `dotnet pack sdk/LuckyEngine.PluginSdk -c Release
-o artifacts`. Reference the resulting `LuckyEngine.PluginSdk` NuGet package from
your own repository. Only a compatible installed editor and .NET 9 SDK are needed.
No private engine headers, assemblies or native build tools are required.

Pipers is the first preview integration. Its catalog listing is present while its
remote release package is being validated; a listing does not imply that a remote
installable release has already been published. The editor rejects missing packages.

# Lucky Engine public plugin SDK

Plugin authors need the installed Lucky Engine editor, the .NET 9 SDK and
`LuckyEngine.PluginSdk` 0.1.0. They do **not** need the engine checkout, private
headers, Hazel assemblies, a native compiler or access to the engine repository.

This SDK is distributed as a NuGet package. Engine maintainers produce it with:

```powershell
dotnet pack PluginSDK/LuckyEngine.PluginSdk -c Release -o PluginSDK/artifacts
```

Authors obtain the `.nupkg` from the SDK release, add that folder as a NuGet
source, then reference it from their own independent project:

```xml
<PackageReference Include="LuckyEngine.PluginSdk" Version="0.1.0" />
```

Use `PluginApplication.RunAsync(new MyPlugin())` as the entry point. Implement
`ILuckyPlugin.StartAsync`, `HandleEventAsync` and `DisposeAsync`. Publish a native
widget tree with `PublishViewAsync`; update measured values with `PatchAsync`.
Widgets are rendered by the editor, not by a browser. UI changes made by a patch
never generate a user-input event. Slider events distinguish intermediate changes
from a committed target.

## Distribution

A repository contains `lucky-plugin.yaml`, a README and platform release ZIPs.
The ZIP contains the same manifest plus a self-contained published executable.
Declare API 1, a reverse-domain plugin ID, namespaced panels and optional workspaces.
Catalog inclusion is optional: Tools → Install Plugin → From repository supports
a direct public GitHub repository URL and a trust review. Plugins run as the current
user: the trust review is **not an operating-system sandbox**. No administrator
rights are needed for a normal installation.

The engine rejects incompatible API versions and verifies the archive's SHA-256
before activating it. Each update occupies its own directory. Settings are separate
from the package and survive updates, disabling and uninstalling.

## Compatibility and protocol

API 1 is currently a development preview. It is versioned independently of the
engine's private C++ ABI. The editor owns the adapter; plugins link only this SDK.
Do not publish a stable API guarantee until the first plugin integration passes.

Messages are UTF-8 JSON, one per line, with `protocol: 1`. Standard output is reserved
for protocol messages; use `ReportAsync` for logs. Requests carry a `requestId` and
receive a response with either `result` or `error`. The editor starts the session
with the plugin ID, package directory and writable settings directory. EOF or
`stop` cancels the session. A plugin must cancel its work and dispose connections.

Current native widgets: column, row, text, button, slider, input, dropdown and
Markdown text. Widget IDs must be unique per panel. Every contribution must use
the plugin's namespace. Optional workspace contributions open the plugin panel
beside the existing scene viewport; they do not replace project scene data.

## Public scene and camera API

`host.info` returns `api` and `platform`. `scene.robots` returns the scene UUID
and robot entity UUIDs, labels and named joint descriptions. `robot.read` takes
`sceneId` and `entityId`; positions and limits use radians for revolute joints
and metres for prismatic joints. Declare `scene.robots` for these calls.

`robot.feedback` also takes a `joints` object mapping joint names to measurements.
Declare `scene.pose.write`. It changes only the selected edit-mode model's pose;
it does not send hardware commands, change calibration, or produce user events.
It returns `applied:false` while a user is dragging that robot. Play mode rejects
these writes. Resolve UUIDs again when the scene/model changes.

`robot.subscribe` with the selected scene/entity UUIDs requests explicit viewport
joint-edit commits as `engine.robot.target` UI events. The value contains the
same named joint array as `robot.read`. `robot.unsubscribe` removes the subscription.
Only deliberate supported joint-gizmo edits are emitted; a changed pose is never
assumed to be a user command. Plugins remain responsible for device permissions,
fresh telemetry, unit/profile validation, limits, command leases and recovery.

Image widgets name a stream via `imageId`. `PublishFrameAsync` accepts JPEG frames
up to 2048×2048 and 4 MiB encoded payload, with declared dimensions, sequence,
timestamp in microseconds, clock domain and stable device identity. The host
coalesces previews and preserves their aspect ratio; it does not record them.
Clock domains must distinguish host receipt time from hardware exposure time.
This API alone does not establish hardware synchronization.

The independent [HelloNative example](Examples/HelloNative) demonstrates startup
RPC, native controls and patches. It references only the published NuGet package.

## Local author iteration

Publish a self-contained executable and ZIP it with `lucky-plugin.yaml`, README
and dependencies. Archive names use `/`, never Windows `\\`. The installed editor
ships `Plugins/Manager/LuckyEngine.PluginManager.exe`; its `--install-local <zip>
--store <Plugins/packages> --trust-local` command verifies and installs an explicitly
trusted local build. Open Tools → Install Plugin → Installed to refresh contributions.

API 1 currently supports public GitHub repositories and local developer packages.
Private-repository login, a richer native widget set and hardware-timestamped
recording are separate extensions. No engine source is needed for either flow.

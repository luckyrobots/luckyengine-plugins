# Lucky Engine public plugin SDK

Plugin authors need the installed Lucky Engine editor, the .NET 9 SDK and
`LuckyEngine.PluginSdk` 0.1.0. They do **not** need the engine checkout, private
headers, Hazel assemblies, a native compiler or access to the engine repository.

Download [`LuckyEngine.PluginSdk.0.1.0.nupkg`](https://github.com/luckyrobots/luckyengine-plugins/releases/download/sdk-v0.1.0/LuckyEngine.PluginSdk.0.1.0.nupkg)
from the public [SDK preview release](https://github.com/luckyrobots/luckyengine-plugins/releases/tag/sdk-v0.1.0).
You can also build the same SDK from this public repository:

```powershell
dotnet pack sdk/LuckyEngine.PluginSdk -c Release -o artifacts
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
It returns `applied:true` with the resulting `pose` for verification. While a user
edits a joint it returns `applied:false`, preserves that joint's preview and keeps
the other joints measured. Feedback temporarily overrides Edit-mode motion-graph
preview; it does not change saved graph settings. Play mode rejects these writes.
Resolve UUIDs again when the scene/model changes.

`robot.subscribe` with the selected scene/entity UUIDs requests explicit viewport
joint-edit commits as `engine.robot.target` UI events. The value contains the
same named joint array as `robot.read`, plus `editedJoint` identifying the one
deliberately changed hinge/slide. Use only that joint as the command target and
hold other hardware joints at fresh measured positions. No-op/cancelled edits do
not emit targets. `robot.unsubscribe` removes the subscription.
Only deliberate supported joint-gizmo edits are emitted; a changed pose is never
assumed to be a user command. Plugins remain responsible for device permissions,
fresh telemetry, unit/profile validation, limits, command leases and recovery.

Image widgets name a stream via `imageId`. `PublishFrameAsync` accepts JPEG frames
up to 2048×2048 and 4 MiB encoded payload, with declared dimensions, sequence,
timestamp in microseconds, clock domain and stable device identity. The host
coalesces previews and preserves their aspect ratio; it does not record them.
Clock domains must distinguish host receipt time from hardware exposure time.
This API alone does not establish hardware synchronization.

`robot.teleoperate` requires `scene.pose.write` and an existing owned
`robot.subscribe` for the same `sceneId`/`entityId`. Pass `enabled: true` and a
named hinge `joint` to select its rotation gizmo and suppress ordinary viewport
selection. This is an Edit-mode interaction layer; it does not start physics.
Pass `enabled: false` with the same IDs to pause. Escape, lost window focus,
scene/mode changes or 500 ms without measured feedback pause it too.

The plugin receives `engine.robot.follow` events (`begin`, `update`, `commit`, `end`),
with string `gesture`, monotonic `sequence`, `timestampUnixMilliseconds`,
`editedJoint`, scene/entity IDs, and named joint positions in radians/metres.
Updates are emitted at most every 50 ms and replace pending updates. Cancellation (`end`) carries identity fields only. Normal release emits `commit`
with the final pose: validate it as a fresh monotonic target and finish it under
the existing command lease. Missing drag input must still hold; never replay a
target after reconnect. Provide a visible progress indicator and Stop movement. Engine
feedback does not produce target events. Feedback replies include `teleoperating`.
The viewport's cyan skeleton represents measured bodies; the solid robot is the
dragged leader. During committed movement `robot.feedback` may include
`leaderJoint` and `leaderPosition` (radians), retaining the target while measured
feedback supplies the ghost. This override is accepted only within an active
owned Teleoperate session. Camera previews currently render that leader scene during a drag,
so they must not be presented as synchronized measured-camera observations.
This API provides joint rotation; Cartesian tip IK is not implemented yet.

`scene.cameras` (capability `camera.capture`) lists camera entity UUIDs, labels and
vertical FOV for the current Edit-mode scene. Use a native `scene-camera` widget
and patch `sceneId`, `cameraId`, `captureWidth` and `captureHeight` to show a live
simulation counterpart. Optional `overlayImageId` and `blend` (0–1) compare it
with a real preview of the same aspect ratio. The engine owns renderer resources;
plugins never receive GPU pointers. Re-resolve the scene UUID after reloading.
`streamKind: depth` uses the engine's existing DepthPackPass (clean metric depth),
with a 7–50 cm grayscale display (white near, black invalid/outside range).
`lensModel: fisheye` selects the existing path-traced fisheye model; its default
150-degree vertical field is an estimate. `intrinsics` may supply width, height,
fx, fy, cx, cy in OpenCV pixel-centre coordinates, plus four Kannala–Brandt
coefficients for fisheye. Dimensions must match; the engine uses an asymmetric
projection and the existing lens mapping. These preview overrides do not mutate
saved scene cameras or the recorder. No optical profile verifies camera mounts,
sensor-noise matching, exposure synchronization or pixel-identical RGB appearance.

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

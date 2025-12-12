# Branch Sync Notes (elpis-newe928)

- Ported the startup race-condition guard that defers the expensive `FinalLoad` work until the loading page is active and initialization has completed. This is handled by a new `StartupCoordinator` helper that now lives in a dedicated `Elpis.Core` project referenced by the main WPF app.
- Brought over the `Elpis.Tests` project and expanded coverage for `StartupCoordinator`, including constructor guard clauses, page/state gating, and null-loading-page handling.
- Added the `Elpis.Pipeline.Tests` suite (x86) to exercise the playlist queueing behavior and new networking primitives (`Crypto.DecryptSyncTime`, `JSONResult`).
- Enabled the networking tests by exposing PandoraSharp internals through `InternalsVisibleTo` and wiring up the new test projects inside the solution alongside the existing libraries.
- Updated `MainWindow` to use the shared coordinator, call `ShowPage(_loadingPage)` immediately, and queue `FinalLoad` deterministically after each `ShowPage` call in this branch.

## Outstanding Follow-Ups

- Run `dotnet test Elpis.Tests/Elpis.Tests.csproj` and `dotnet test Elpis.Pipeline.Tests/Elpis.Pipeline.Tests.csproj -p:Platform=x86` locally (requires .NET Framework 4.8.1 targeting pack and x86 vstest host).
- Verify installers/package scripts include the new `Elpis.Core.dll`.
- Update CI definitions (if any) so the added test projects run automatically.

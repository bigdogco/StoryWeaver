# Uno Platform spike

Started 2026-09-06. Requested: create a new `uno_spike` branch and investigate
Uno Platform as the Phase 2 UI candidate because Linux support matters.

- [x] Create the `uno_spike` branch.
- [x] Install or verify Uno templates/tooling needed for a minimal desktop app.
- [x] Scaffold a small Uno UI project without wiring gameplay logic into the UI.
- [x] Add the project to the solution only if the scaffold builds cleanly enough to inspect.
- [x] Verify a Windows desktop build locally.
- [x] Record Linux support findings and remaining risks in project docs.
- [x] Record the spike outcome in a devlog before commit.

Findings:

- Installed `Uno.Templates` 6.7.22.
- Generated `src/StoryWeaver.Uno` with:
  `dotnet new unoapp -preset blank -platforms desktop -tests none -presentation none -theme simple -n StoryWeaver.Uno -o src\StoryWeaver.Uno`.
- The generated target was `net10.0-desktop` with `SimpleTheme` and `SkiaRenderer`.
  It was retargeted to `net9.0-desktop` because the installed Visual Studio 2022
  uses MSBuild 17.14, while .NET SDK 10.0.400 requires MSBuild 18.0 or newer.
- Official Uno docs list the desktop Skia path as Windows, Linux and macOS; Linux
  runs where current .NET versions are supported, with X11 and Framebuffer listed.
- Direct project build passed:
  `dotnet build src\StoryWeaver.Uno\StoryWeaver.Uno\StoryWeaver.Uno.csproj -f net9.0-desktop`.
- Windows launch smoke passed: `dotnet run --project src\StoryWeaver.Uno\StoryWeaver.Uno\StoryWeaver.Uno.csproj -f net9.0-desktop --no-build`
  started and stayed alive for 8 seconds before being stopped.
- Root solution build initially failed because `Uno.Sdk` was pinned only in the
  generated nested `global.json`. Adding root `global.json` with `Uno.Sdk` 6.7.22
  fixed solution-level SDK resolution.
- Full solution build passed:
  `dotnet build StoryWeaver.sln`.
- WSL build check passed on Ubuntu 26.04 under WSL2 using .NET SDK 10.0.400
  installed in `/home/pavel/.dotnet`, before the spike was retargeted to .NET 9.
- WSL build command:
  `export PATH="$HOME/.dotnet:$PATH" DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_CLI_HOME="$HOME" DOTNET_CLI_TELEMETRY_OPTOUT=1; unset DISPLAY WAYLAND_DISPLAY PULSE_SERVER; dotnet build StoryWeaver.sln`.
- WSL build result before the retarget: succeeded with one warning, zero errors. The warning is
  `UNOB0003`, where Uno ignored `Strings/en-US/Resources.resw` because language
  detection did not work under invariant globalization mode. Windows build of
  the same resource is clean.
- Visual Studio 2022 compatibility check passed after retargeting to .NET 9:
  both restore and build succeeded through
  `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`.
- WSL GUI launch was not verified. WSLg on this Windows install shows a Remote
  Desktop ActiveX popup: "Could not load the Remote Desktop Services ActiveX
  control. Make sure rdclientax.dll is in the path." Build checks can avoid WSLg
  by unsetting `DISPLAY`, `WAYLAND_DISPLAY` and `PULSE_SERVER`.

Moved to TODO_FUTURE_WORK.md before closing:

- Linux desktop GUI run verification.
- Runtime policy for a .NET 9 UI beside a .NET 8 engine.
- Comfort check for Uno's XAML/WinUI-style model as the pack editor and play surface.

Decision rule: this spike may make Uno the leading candidate, but it should not
lock Uno as selected until the scaffold and build behavior are known.

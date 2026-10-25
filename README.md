# Galaxy Client

A Windows desktop launcher for Minecraft Java. Galaxy Client is early access and is actively being built.

## What works now

- Browse official Minecraft Java releases and snapshots.
- Install and launch vanilla Minecraft in a Galaxy-specific folder under your Windows local app data.
- Start a clearly labeled offline profile. Offline mode does not authenticate you or grant access to online-mode servers.
- Search Modrinth mods, modpacks, resource packs, data packs, and shaders, then open project pages in your browser.
- Build six Windows packages with GitHub Actions: x64 portable, x64 framework-dependent, x86 portable, ARM64 portable, x64 uncompressed, and x64 folder package.

## Still being built

Microsoft account sign-in, Modrinth downloads and installs, Discord Rich Presence, and in-app update download, verification, safe replacement, restart, and rollback are not implemented yet. The version-tag workflow is ready to build and attach all six packages to a GitHub Release once a version tag is published.

## Download builds

Open [GitHub Actions](https://github.com/OGBONEX/GalaxyClient/actions/workflows/build-windows.yml), select a successful run, and download the package matching your Windows device. Build artifacts currently expire after 30 days. A framework-dependent build requires the .NET 8 Desktop Runtime; the portable packages include the runtime.

## Requirements to run from source

- Windows 10 or later
- .NET 8 SDK with Windows desktop support
- Git

Run locally with:

```powershell
dotnet run --project .\GalaxyClient.csproj
```

## Safety

Galaxy Client never asks for a Microsoft password directly. Online account support will use supported account authorization and protected token storage. Offline profiles are for local use. Game files are installed in a Galaxy-specific directory; existing Minecraft files are not deleted or migrated. Never commit credentials, tokens, signing keys, or personal game data.

# Galaxy Client

A Windows desktop launcher for Minecraft Java. Galaxy Client is early access and is actively being built.

## What works now

- Browse official Minecraft Java releases and snapshots.
- Install and launch vanilla Minecraft in a Galaxy-specific folder under your Windows local app data.
- Sign in through Microsoft’s supported Minecraft Java authentication flow or choose an offline profile. Microsoft sessions stay in memory and require a new sign-in each time the app is reopened.
- Search Modrinth mods, modpacks, resource packs, data packs, and shaders, then open project pages in your browser.
- Build six Windows packages with GitHub Actions: x64 portable, x64 framework-dependent, x86 portable, ARM64 portable, x64 uncompressed, and x64 folder package.

## Still being built

One-click Modrinth downloads and installs, Discord Rich Presence, and in-app update download, verification, safe replacement, restart, and rollback are not implemented yet. Pushing a version tag such as v0.1.0 triggers a pre-release build that attaches all six uniquely named packages to a GitHub Release. No versioned release has been published yet.

## Download builds

Open [GitHub Actions](https://github.com/OGBONEX/GalaxyClient/actions/workflows/build-windows.yml), select the latest successful run, and download the package matching your Windows device. Build artifacts expire after 30 days. A framework-dependent build requires the .NET 8 Desktop Runtime; portable packages include the runtime.

## Requirements to run from source

- Windows 10 or later
- .NET 8 SDK with Windows desktop support
- Git

Run locally with:

```powershell
dotnet run --project .\GalaxyClient.csproj
```

## Safety

Galaxy Client never asks you to type a Microsoft password into its own UI. Microsoft authentication is handled by CmlLib’s supported sign-in flow; account session data stays in memory. Offline profiles are for local use and do not authenticate for online-mode servers. Game files are installed in a Galaxy-specific directory; existing Minecraft files are not deleted or migrated. Never commit credentials, tokens, signing keys, or personal game data.

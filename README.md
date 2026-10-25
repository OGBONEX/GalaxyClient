# Galaxy Client

A Windows launcher for Minecraft Java. Galaxy Client is an early preview.

## Available features

- Browse Mojang's official Java Edition releases and snapshots, then install and launch vanilla Minecraft in Galaxy Client's own folder under Local AppData.
- Sign in through Microsoft's supported Minecraft Java flow or use an offline profile. Microsoft sign-in stays in memory and must be repeated after restarting the launcher.
- Search Modrinth mods, modpacks, resource packs, data packs, and shaders. One-click installation is still in development.
- Show Galaxy Client and the active Minecraft version in Discord Rich Presence when Discord is running.
- Check GitHub Releases for launcher updates, verify the release asset's SHA-256 digest before replacement, then restart with rollback handling.
- Build six Windows packages through GitHub Actions: x64 portable, x64 framework-dependent, x86 portable, ARM64 portable, x64 uncompressed, and x64 folder package.

## Download

Open [Galaxy Client releases](https://github.com/OGBONEX/GalaxyClient/releases) and choose a package for your device. The win-x64-portable executable is the recommended option for most Windows PCs. The framework-dependent package requires the .NET 8 Desktop Runtime.

Release builds are marked as previews. New tags beginning with v build all six packages and attach them to a GitHub Release.

## Run from source

Requirements: Windows 10 or later, .NET 8 SDK with Windows desktop support, and Git.

```powershell
dotnet run --project .\GalaxyClient.csproj
```

## Safety

Galaxy Client never asks for your Microsoft password in its own UI. Authentication is handled by CmlLib's Microsoft flow. Offline profiles cannot authenticate to online-mode servers. Game files are kept in a Galaxy-specific directory; existing Minecraft files are not deleted or migrated. Never commit credentials, tokens, signing keys, or personal game data.

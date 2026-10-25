# Galaxy Client

A Windows desktop launcher project for Minecraft Java. This first slice adds a WPF desktop shell; it does **not** yet authenticate accounts, install or launch game files, connect Discord, or apply launcher updates.

## Requirements

- Windows 10 or later
- .NET 8 SDK with Windows desktop support
- Git

## Run locally

```powershell
dotnet run --project .\GalaxyClient.csproj
```

## Roadmap

1. Minecraft version manifest discovery, isolated game directories, and launch process management.
2. Microsoft sign-in through the official device authorization flow; offline profiles clearly marked and limited to local use.
3. Modrinth search and safe installs for supported project types, loaders, and game versions.
4. Discord Rich Presence with user-controlled activity details.
5. GitHub Releases updates with signed or checksummed packages, staged replacement, restart, and rollback.
6. CI packaging and release workflow.

## Safety

The launcher must never ask for a Microsoft password directly. Use supported account authorization and secure token storage. Offline profiles do not grant access to online-mode servers. Install game content in a Galaxy-specific directory; do not delete or automatically migrate existing Minecraft files. Never commit credentials, tokens, signing keys, or personal game data.

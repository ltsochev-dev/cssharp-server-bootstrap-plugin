# ServerBootstrap Plugin

`ServerBootstrap` is a [CounterStrikeSharp](https://docs.cssharp.dev/) plugin for CS2 servers running under [Agones](https://agones.dev/). It watches `GameServer` updates, applies server bootstrap settings from Agones annotations, and shuts down allocated servers when they stay idle or when a match finishes.

## What It Does

- Watches Agones `GameServer` state changes through `AgonesSDK`
- Reads Agones annotations and logs them for visibility
- Controls the flow of the server

## Match And Shutdown Flow

1. Agones marks the server as `Allocated`
2. The plugin starts a 60 second idle timer
3. If a player joins before the timer expires, the timer is cancelled
4. If nobody joins, the plugin requests Agones shutdown
5. When the match ends, the plugin announces the winner in chat
6. Eight seconds later, the plugin requests Agones shutdown

## Requirements

- .NET 8 SDK
- A CounterStrikeSharp-compatible CS2 server
- Agones-managed infrastructure

NuGet dependencies:

- `CounterStrikeSharp.API` `1.0.363`
- `AgonesSDK` `1.56.0`

## Build

```bash
dotnet build ServerBootstrap.sln
```

If you use Nix, the repository includes a `shell.nix` with the .NET 8 SDK:

```bash
nix-shell
dotnet build ServerBootstrap.sln
```

## Install

Build the project and copy the plugin output into your CounterStrikeSharp plugins directory for the server environment you deploy to.

A typical release build command is:

```bash
dotnet build ServerBootstrap.csproj -c Release
```

The compiled files will be placed under:

```text
bin/Release/net8.0/
```

Deploy the generated assembly and its dependencies to the appropriate CounterStrikeSharp plugin location on your game server.

## Configuration

This project does not currently expose a separate config file. Runtime behavior is driven by Agones `GameServer` state and annotations.

Example annotations:

```yaml
metadata:
  annotations:
    map: de_mirage
    mode: competitive
```

## Notes

- The plugin metadata version is currently `0.0.1`
- There is a `TODO` in the code for future bootstrap work such as password and whitelist handling
- `mode` is captured but not yet used to configure the server

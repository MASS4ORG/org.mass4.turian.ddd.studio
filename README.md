# DataAsset-Driven Design Studio

Studio tooling for the DataAsset-Driven Design runtime: create-asset menus, type icons, live variable editing, a default-payload Raise, listener ownership, a bounded event timeline, an overview window and wire validation. It depends on `org.mass4.turian.ddd` and the engine's editor services.

## Installation

Install the brick in Studio's brick workspace together with the matching `org.mass4.turian.ddd` runtime brick. Studio discovers the plugin through the editor precast assembly; it is not compiled into Studio.

## Creating assets

In the asset browser, right-click a folder and choose **Create → DDD**. Event payloads, variable and constant value types, and runtime-set element types each have their own submenu. New assets get unique default filenames, and new enum values are named after their file.

The asset browser, its type filter and the Create menu use Studio's `IAssetPresentationProvider` extension point. DDD registers a distinct icon for events, variables, constants, sets, enum values, readers, transitions and responses, with glyph fallbacks. Menu text follows the active theme. The icons are adapted from Unity Atoms (MIT); their license is in `Editor/Resources/UnityAtoms-LICENSE.md`.

## Overview

**View → DataAsset Overview** searches and filters the project's DDD assets, finds assets with no authored incoming references, shows each asset's authored raises, listens and references, and bulk-creates 1–100 assets under Assets. An asset without incoming references is a candidate for review, not proof that nothing uses it: game code may reference it directly.

The index covers project Assets and recognizes installed-asset metadata for dangling-reference checks. Listener ownership and the event timeline are available in Play Mode only.

## Validation and Play Mode

**Validate wires / rewire report** checks GUID identity, missing targets, incompatible payload types and removed members, including nested variable-reference fields.

In Play Mode, select an event to raise its default payload and inspect each listener's owner, priority and order. Select a variable to see its live value; Int, Float and Bool values can be edited in the panel, and other types are edited in the inspector. Stopping Play Mode discards runtime state.

## Custom payloads

**View → DDD Type Generator** takes an existing C# payload type and generates an Event, Variable, Constant or Runtime Set family (reference payloads only for sets). It writes attributed classes and stable `.cs.meta` identities into Assets and requests a normal project compile. After a successful compile, create the assets under **Create → DDD → Custom**. Existing files are preserved and compiler errors appear in Output. The generator does not invent payload types.

## Building

Build and test with the matching sibling runtime brick and a Turian checkout:

```sh
dotnet build Brick~/Tests/Turian.DataAssetDriven.Studio.Tests/Turian.DataAssetDriven.Studio.Tests.csproj -p:NoLocalPackages=true -m:4
dotnet run --project Brick~/Tests/Turian.DataAssetDriven.Studio.Tests -p:NoLocalPackages=true
```

Precast output is copied only by Release builds.

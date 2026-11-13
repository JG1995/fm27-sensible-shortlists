# FM27 Sensible Shortlists

A BepInEx plugin for Football Manager 27 that aims to make the shortlist a plain list of potential signings, instead of a subscription to messages about every player on it.

## Status

**Works, with limits.** With `Mode = Quiet` the plugin marks as read each forwarded news story about a player at another club, so those stories stop arriving unread. They still appear in Messages. `Mode = Hide` also keeps them out of Messages; it is built and has not yet run in the game. The plugin does not check which shortlist a player is on: the game offers no way to ask.

| Step | State |
| ---- | ----- |
| Is there a game setting for this? | Checked: no. See below. |
| Can an add-on do it? | Yes, through the game's UI interop layer: it can read the inbox and send the game's own "mark as read" event. |
| Interop trace (the tool that gathers that data) | Built and run in the game. |
| Inbox triage (judging each message) | Built and seen working in the game. |
| Marking quiet messages read | Built and seen working in the game. |
| Keeping quiet messages out of the list | Built; not yet run in the game. The game cannot delete a message or be told not to send it, so the plugin shortens the list on its way to the screen. |

## What the game offers on its own

The Shortlist screen's top bar has two settings, and they only affect scout reports:

- **Send re-scouted player reports to Messages** (toggle)
- **Scout report updates** (how often scouts refresh reports on shortlisted players)

Nothing in FM27 controls the other messages a shortlisted player causes, such as bids from other clubs or contract news. The "active shortlists" option from FM24 is gone: its name survives in the game's files, but the simulation no longer responds to it.

## How it works

FM27 is a Unity IL2CPP game. Its simulation is a native library, and its UI talks to that library through a small set of events and named property channels. The plugin uses that same layer. It does not edit save files or the game's memory.

The code is split in two:

- `src/SensibleShortlists.Core` holds every decision. It has no game or BepInEx reference and runs under unit tests.
- `src/SensibleShortlists.Plugin` is the BepInEx plugin. It hooks the game, turns game objects into plain values, and calls the core.

## Requirements

- Football Manager 27 on Windows (Steam)
- [BepInEx 6 IL2CPP](https://github.com/BepInEx/BepInEx) installed in the game folder, and the game started once so BepInEx generates its `interop` assemblies
- .NET SDK 6 to build

## Build and install

The plugin compiles against the assemblies BepInEx generates in the game folder. If the game is not in the default Steam location, set `FM27_GAME_DIR` to the game folder first.

```bash
dotnet test tests/SensibleShortlists.Core.Tests
```

```bash
scripts/deploy.sh
```

`deploy.sh` builds the plugin and copies `SensibleShortlists.dll` and `SensibleShortlists.Core.dll` to `BepInEx/plugins/SensibleShortlists/` in the game folder. Restart the game to load it.

## Settings

The plugin writes `BepInEx/config/fm27.sensibleshortlists.cfg` the first time it loads.

| Section | Setting | Default | Meaning |
| ------- | ------- | ------- | ------- |
| `QuietShortlist` | `Mode` | `Off` | `Observe` judges each inbox message and records the verdict to `BepInEx/SensibleShortlists.triage.log`. It changes nothing in the game. `Quiet` does the same and marks as read each unread message it judges to be noise: a forwarded story about a player at another club that does not involve your club or players. `Hide` does what `Quiet` does and keeps those messages out of Messages. |
| `Diagnostics` | `TraceInterop` | `false` | Record what the UI sends to and receives from the simulation, to `BepInEx/SensibleShortlists.trace.log`. For development; leave off for normal play. |
| `Diagnostics` | `TraceNodeGraph` | `false` | With `TraceInterop` on, also record how the UI registers its bindings with the simulation. Large. |
| `Diagnostics` | `TraceFilter` | `shortlist,message,news,portal` | Comma-separated terms. A property channel is traced when its label contains one of them. Empty traces everything. |

## Limitations

- The plugin relies on undocumented game internals. A game update can break it; when a hook no longer fits, the plugin logs that and skips it.
- A hidden story is not deleted. It stays in the save, and with any other mode it is listed again, marked read. The News section still shows it.
- With `Hide`, a new message appears about a second after the game lists it: the plugin holds it back until it has been judged.
- The plugin recognises your club in a story by the story's title: your first team, your club, or one of your players. A story that names only one of your other teams, or names your club only in its body, is not recognised.
- Windows, Steam, BepInEx 6 only.

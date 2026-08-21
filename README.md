# MutePilot

A lightweight Windows utility for custom hotkey-based application audio muting. MutePilot is planned to let users mute or unmute Windows master audio and individual application audio sessions using custom global keyboard shortcuts.

## Current Status

**Planning / Initial setup**

The repository structure, documentation standards, and Git workflow are being established. No application functionality has been implemented yet.

## Planned Core Features

* Windows master mute/unmute
* Detection of applications with active Windows audio sessions
* Per-application mute/unmute
* Custom global hotkeys with mute/unmute toggle behavior
* Multiple saved application hotkey bindings
* Persistent local settings that survive program restarts

Audio volume adjustment is not currently planned.

## Technology Direction

* C#
* .NET 8
* WPF
* Windows Core Audio APIs
* Windows global hotkeys
* JSON settings persistence

Windows API integration will be kept separate from UI logic, with an emphasis on simple and maintainable design.

## Development Roadmap

1. Initialize the C#/.NET 8 WPF solution.
2. Implement Windows master mute/unmute.
3. Detect active application audio sessions.
4. Add per-application mute/unmute toggles.
5. Add configurable global hotkeys.
6. Persist application bindings and settings locally.
7. Refine the UI, validation, and release documentation.

## Author

**Made by 유성우**

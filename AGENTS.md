# MutePilot Project Instructions

## Project Identity

Project name: **MutePilot**

MutePilot is a lightweight Windows utility that lets users mute or unmute Windows master audio and individual application audio sessions using custom global keyboard shortcuts.

The project author credit must always be:

**Made by 유성우**

Do not remove, translate, abbreviate, or replace this credit unless explicitly requested by the user.

When the application UI is implemented later, display:

`Made by 유성우`

in a small, clean footer area of the main window.

Also include the same author credit in:

* README.md
* About dialog/page if one is added later
* Release documentation where appropriate

## Main Functional Scope

The planned MutePilot application should support:

* Windows master mute/unmute
* Detection of applications with active Windows audio sessions
* Per-application mute/unmute
* Custom user-defined global hotkeys
* Hotkey toggle behavior: Mute ↔ Unmute
* Multiple saved application hotkey bindings
* Persistent local settings
* Application bindings that survive program restarts

The project does NOT need audio volume adjustment.

## Explicitly Out of Scope

Do not implement:

* Game death detection
* Screen recognition
* OCR
* Computer vision
* Game memory scanning
* DLL injection
* Process injection
* DirectX hooking
* Anti-cheat interaction
* Keyboard macros sent into games

MutePilot should control audio only through normal Windows APIs.

## Technology Direction

Preferred stack:

* C#
* .NET 8
* WPF
* Windows Core Audio APIs
* Windows global hotkeys
* JSON for persistent settings

Keep Windows API logic separated from UI logic.

Avoid unnecessary frameworks and over-engineering.

## Git Workflow

The `main` branch is the stable branch.

Do not perform normal development directly on `main`.

For development tasks, create meaningful branches such as:

* `feature/v0.1-core`
* `feature/v0.2-tray`
* `feature/v0.3-ui`
* `fix/hotkey-conflict`
* `docs/readme-update`

Use clear, professional commit messages.

Examples:

* `chore: initialize MutePilot project`
* `feat: add application audio session detection`
* `feat: implement per-app mute toggle`
* `feat: add global hotkey support`
* `feat: persist hotkey bindings`
* `fix: handle unavailable audio sessions`
* `docs: update development log`

Do not create meaningless commits simply to increase commit count.

Never commit:

* passwords
* API keys
* access tokens
* secrets
* user credentials
* build output
* temporary files
* machine-specific files that should be ignored

## Development Log

This GitHub repository is also intended to serve as a learning record and portfolio.

Maintain:

`docs/devlog/`

For each day on which meaningful development work is completed, create or update:

`docs/devlog/YYYY-MM-DD.md`

Use the actual local development date.

Each daily log should contain:

### Goal

What was intended for the development session.

### Implemented

What was actually implemented.

### Technical Learning

Important APIs, concepts, architecture, or tools learned during the work.

### Problems Encountered

Real problems or limitations encountered.

### Solutions

How those problems were solved or mitigated.

### Validation

Build and test results.

### Next Step

The next logical development task.

Keep development logs factual, concise, and professional.

Do not exaggerate what was implemented or tested.

## CHANGELOG

Maintain `CHANGELOG.md` as the project develops.

Update it when meaningful user-facing functionality is added, changed, or fixed.

Do not update the changelog for trivial formatting-only changes unless appropriate.

## README

Maintain a professional `README.md`.

Eventually it should contain:

* MutePilot overview
* Key features
* Screenshots
* Technology stack
* Architecture overview
* Build instructions
* Usage instructions
* Known limitations
* Development roadmap
* Author section

The author section must contain:

**Made by 유성우**

## Build and Validation Rules

Before declaring a programming task complete:

1. Build the entire solution.
2. Fix compilation errors.
3. Run available relevant tests.
4. Check for obvious runtime errors where possible.
5. Review changed files.
6. Report what was actually validated.
7. Do not claim something works if it was not tested.

## GitHub Publishing Workflow

For meaningful development tasks, after the implementation has been successfully built and validated:

1. Update the daily development log.
2. Update CHANGELOG.md when appropriate.
3. Review the Git diff.
4. Create logical Git commits.
5. Push the current development branch to `origin` when Git authentication and repository permissions allow it.
6. Report the branch name and commit hash(es).

Never force-push unless the user explicitly requests it.

Never automatically overwrite the `main` branch.

If a push fails, keep the local commits intact and clearly report the reason.

## General Development Behavior

Prefer simple, maintainable solutions.

Do not add unrelated features.

Do not silently change project scope.

Do not remove existing working functionality without a clear reason.

When a technical limitation is discovered, document it instead of hiding it.

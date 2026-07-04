# MHRebornLauncher

A lightweight Windows launcher for Marvel Heroes private servers.

## Features

- Detects the Marvel Heroes game executable from the launcher location.
- Launches the game with private-server command line arguments.
- Supports automatic login through game launch parameters.
- Account validation through a server-side login endpoint.
- Encrypted local “Remember Me” support using Windows user-protected storage.
- Displays launcher news and patch notes from a JSON feed.
- Includes an account creation link on the login screen.
- Publishes as a single Windows executable.

## Installation

Place the launcher executable in the root of the Marvel Heroes install folder, next to the `UnrealEngine3` folder.

Example layout:

```text
Marvel Heroes/
├─ MHRebornLauncher.exe
└─ UnrealEngine3/
   └─ Binaries/
      └─ Win64/
         └─ MarvelHeroesOmega.exe
```

The launcher searches for supported executables under `UnrealEngine3` and launches the first valid client it finds.

## Building

Requirements:

- Windows
- Visual Studio 2022 or Visual Studio Insiders
- .NET 8 SDK
- .NET desktop development workload

Open:

```text
MHRebornLauncher.sln
```

Then build or publish from Visual Studio.

## Publishing

### Standalone build

Creates a larger single executable that does not require users to install the .NET runtime.

```powershell
dotnet publish -p:PublishProfile=StandaloneCompressed
```

### Smaller build

Creates a smaller single executable, but users must have the .NET 8 Desktop Runtime installed.

```powershell
dotnet publish -p:PublishProfile=SmallRuntimeRequired
```

## News feed

The news panel reads a JSON array of posts from https://play.omeganode.org/launcher/news.json

Example format:

```json
[
  {
    "title": "Launcher Update",
    "date": "2026-01-01",
    "category": "Patch Notes",
    "body": "Patch details go here."
  }
]
```

## Server login validation

The launcher can validate credentials before starting the game by sending a POST request to a configured login endpoint.

Expected request body:

```json
{
  "EmailAddress": "user@example.com",
  "Password": "password"
}
```

Expected success response:

```json
{
  "Success": true,
  "PlayerName": "PlayerName",
  "UserLevel": 0,
  "Error": ""
}
```

Expected failure response:

```json
{
  "Success": false,
  "PlayerName": "",
  "UserLevel": 0,
  "Error": "Invalid email or password."
}
```

## Credits

This launcher was built as a clean WPF implementation for Marvel Heroes private-server use.

Bifrost Launcher was used as a reference for relevant Marvel Heroes launcher behavior, including executable detection concepts and launch-argument handling. No Bifrost artwork, branding, or source files are included in this project.

## License

MIT License

Copyright (c) 2026-2026 Xion28080 (Known as Omega)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
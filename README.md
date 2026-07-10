# MHRebornLauncher

A Windows launcher for the Marvel Heroes Reborn private server.

MHRebornLauncher is designed to make connecting to the server easier by launching the game with the correct server settings, supporting account login, displaying recent server news, and keeping itself updated through GitHub Releases.

## What the Launcher Does

- Detects the Marvel Heroes game client from the install folder.
- Launches the game with the correct private-server connection settings.
- Lets players sign in with their Marvel Heroes Reborn account.
- Supports “Remember Me” using Windows-protected local storage.
- Provides a logout option for switching accounts.
- Displays recent server news and patch note previews.
- Links players to the full website news posts.
- Checks for launcher updates and can update itself when a new version is available.

## Installation

Download the latest release from the GitHub Releases page.

Extract the release zip into the root of your Marvel Heroes install folder.

The launcher must be placed next to the `UnrealEngine3` folder.

Example layout:

    Marvel Heroes/
    ├─ MHRebornLauncher.exe
    ├─ MHRebornLauncher.Updater.exe
    └─ UnrealEngine3/
       └─ Binaries/
          └─ Win64/
             └─ MarvelHeroesOmega.exe

Run:

    MHRebornLauncher.exe

Do not run the updater manually. `MHRebornLauncher.Updater.exe` is used only when the launcher installs an update.

## Account Creation

If you do not have an account, use the account creation link inside the launcher or visit the Marvel Heroes Reborn website.

## Updates

Starting with version `1.0.1`, the launcher can check for new versions when it opens.

If an update is available, the launcher will ask whether you want to install it. If accepted, the updater will download the newest release, replace the old launcher, and reopen it automatically.

Players using version `1.0.0` must manually install version `1.0.1` or newer once before automatic updates are available.

## Safety and Source Code

This project is open source so players can review what the launcher does before running it.

The launcher is intended to:

- start the Marvel Heroes client,
- pass the required private-server launch settings,
- optionally remember login information using Windows-protected storage,
- display server news,
- and check GitHub Releases for launcher updates.

The launcher does not install game files, modify the Marvel Heroes client, or run in the background after it is closed.

## Credits

Bifrost Launcher was used as a reference for relevant Marvel Heroes launcher behavior, including executable detection concepts and launch-argument handling.

No Bifrost artwork, branding, or source files are included in this project.

## License

MIT License

Copyright (c) 2026 Xion28080, also known as Omega

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files, to deal in the software
without restriction, including without limitation the rights to use, copy,
modify, merge, publish, distribute, sublicense, and/or sell copies of the
software, and to permit persons to whom the software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.


## WebView2 data location

The embedded news viewer stores its browser data under:

`%LOCALAPPDATA%\OmegaNode\MHRebornLauncher\WebView2`
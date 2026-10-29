# Building MonoSearch

## You do not need to build it

Ready-made programs for Windows, macOS and Linux are attached to every release on the
[Releases page](https://github.com/jaruiz6363/MonoSearch/releases):

| File | System |
|---|---|
| `MonoSearch-Setup-<version>.exe` | Windows 10 and 11, x64: installer |
| `MonoSearch-<version>-macOS-arm64.zip` | macOS 11 or later on Apple Silicon: `MonoSearch.app` |
| `MonoSearch-<version>-x86_64.AppImage` | Linux x86_64: one file, no installation |
| `SHA256SUMS.txt` | Checksums of the three |

Each contains the GUI, the command line, the glass catalogs and these documents. The .NET runtime is included,
so nothing else needs installing. The [user guide](USER_GUIDE.md#1-installing-and-starting) explains how to
install each one.

Build from source only to change MonoSearch or to run it on a system without a ready-made package.

## What you need

- Either of:
  - on Windows, **Visual Studio 2022**, version 17.8 or later, with the ".NET desktop development" workload;
  - on Windows, macOS or Linux, the **.NET 8 SDK** or newer.
- **git**.

Nothing else needs fetching. AberrationCalculator is part of this repository: it is a `git subtree` in
`external/AberrationCalculator` (see [AberrationCalculator](#aberrationcalculator)). There are no machine-specific paths, and a plain clone builds as
it is.

AberrationCalculator's IO library writes all five export formats: ZEMAX `.zmx`, OSLO `.len`, Optalix `.otx`,
Optiland `.json` and LensHH-LT `.lhlt`. MonoSearch builds one AberrationCalculator lens model per design and
hands it to each writer. For the ZMX it first renames the glasses to the names your OpticStudio Glasscat folder
uses. `MonoSearch.exe readback` reads all five back with AberrationCalculator's readers.

## Get the source

```
git clone https://github.com/jaruiz6363/MonoSearch.git
```

## Build in Visual Studio

1. Open `MonoSearch.sln`.
2. Set **MonoSearch.App** as the startup project for the GUI, or **MonoSearch** for the command line.
3. Build and run (F5).

The solution has two folders:

| Folder | Projects |
|---|---|
| `src` | MonoSearch.Core, MonoSearch (command line), MonoSearch.App (GUI) |
| `external` | AberrationCalculator.Core.Series, AberrationCalculator.Core, AberrationCalculator.IO |

## Build from the command line

```
dotnet build MonoSearch.sln -c Release
```

To run:

```
src\MonoSearch.App\bin\Release\net8.0\MonoSearchApp.exe
src\MonoSearch\bin\Release\net8.0\MonoSearch.exe search --f 12 --fno 1.71 --export 10 --out results
```

The `results\` folder at the repository root is ignored by git.

## Projects

| Project | Output | |
|---|---|---|
| `src\MonoSearch.Core` | library | The searches (2GS, 4GA-8), ray traces, glass catalogs, settings file, exporters, layout |
| `src\MonoSearch` | `MonoSearch.exe` | Command line |
| `src\MonoSearch.App` | `MonoSearchApp.exe` | GUI (Avalonia 11.2.3) |

- **Target:** every project targets **net8.0**, as AberrationCalculator does. The SDK of a newer .NET builds
  them too.
- **Catalogs:** the `catalogs\*.AGF` files are copied next to every program that references MonoSearch.Core,
  and the programs read them from there.

## AberrationCalculator

AberrationCalculator is a squashed `git subtree` of `https://github.com/jaruiz6363/AberrationCalculator` at a
known commit, the same way TolerancingModule and AthermalizeModule carry it.

- **How the projects find it:** `AberrationCalculator.Location.props` at the root sets `AberrationCalculatorDir`
  to `external/AberrationCalculator/`. `Directory.Build.props` imports that file into every project, and
  MonoSearch.Core references `$(AberrationCalculatorDir)src/AberrationCalculator.IO`.
- **To see which commit is in use:**

  ```
  git log -1 --format=%b --grep="git-subtree-dir: external/AberrationCalculator"
  ```

  The `git-subtree-split:` line is the AberrationCalculator commit.
- **To move to its latest version:**

  ```
  git subtree pull --prefix=external/AberrationCalculator https://github.com/jaruiz6363/AberrationCalculator.git main --squash
  ```

  Then build and run the [quick checks](#quick-checks-after-a-change). Commit fixes to AberrationCalculator in its own repository and pull
  them here; do not edit `external/` directly.

## Quick checks after a change

```
MonoSearch.exe check
MonoSearch.exe search --f 12 --fno 1.71 --top 5 --export 1 --no-optiland-install --out %TEMP%\ms
MonoSearch.exe readback --dir %TEMP%\ms\rank001_S-LAH79_K-LaSFn9
MonoSearch.exe search --arch 4GA-8 --cores 1 --grouping coarse --top 20 --export 1 --formats zmx,lhlt --no-optiland-install --out %TEMP%\ms4
```

| Command | Expected |
|---|---|
| `check` | 1642 glasses, 12 with an nd difference above 5×10⁻⁵ |
| 2GS search | Rank 1 is OHARA:S-LAH79 / SUMITA:K-LaSFn9, r1 = 9.065 |
| `readback` | All five files give the same prescription and EFL 11.99663 |
| 4GA-8 search | About 2½ minutes. No "could not be started" line. Rank 1 is LASF35 / K-LaSFn9 / TAFD30 / S-LAH79, C = 0.00059, RMS 0.0634. |

The release smoke tests check the 2GS and 4GA-8 rank 1 (see [The release packages](#the-release-packages)). [VERIFICATION.md](VERIFICATION.md) has the
full checks.

### The icon

`src\MonoSearch.App\Assets\make-icon.ps1` draws `icon.png` and `icon.ico`. Both are committed, so run it again
only to change the picture:

```
pwsh src/MonoSearch.App/Assets/make-icon.ps1
```

### A stand-alone copy for another system

To make a copy that runs without .NET installed, publish self-contained for the system's runtime identifier
(`win-x64`, `linux-x64`, `osx-arm64`, `osx-x64` and so on):

```
dotnet publish src/MonoSearch.App -c Release -r linux-x64 --self-contained -o publish/linux-x64
dotnet publish src/MonoSearch -c Release -r linux-x64 --self-contained -o publish/linux-x64
```

Copy the whole output folder, `catalogs` included.

## The release packages

The three packages are built on Windows from the private repository
`https://github.com/jaruiz6363/MonoSearch-Release` by its `build.ps1`, in the same way as ThinFilmDesigner's.
Building them needs:

- **Inno Setup 6**, for the Windows installer;
- **WSL with Ubuntu-22.04**, to run `appimagetool` for the AppImage;
- **gh**, logged in to an account that can run the release repository's workflows. The macOS app is signed and
  checked on a GitHub-hosted Apple Silicon Mac, so no Mac of your own is needed.

```
pwsh .\build.ps1                            all three, version from VERSION, MonoSearch from GitHub at main
pwsh .\build.ps1 -Platforms win,linux       any of win, linux, mac
pwsh .\build.ps1 -Version 0.2.3 -Ref v0.2.3
pwsh .\build.ps1 -Source <local clone>      a local clone instead, to test before pushing
```

`build.ps1` works in these steps.

1. **Clone.** It clones MonoSearch, with its AberrationCalculator subtree, into `work\src` at the given
   commit.
   - Uncommitted edits are not built. The script warns about them.
   - The MonoSearch commit and the AberrationCalculator subtree commit are written to `BUILDINFO.txt` in each
     package.
2. **Publish.** For each platform it publishes the GUI and the command line self-contained into
   `work\publish-<version>\<runtime>`: one folder, with `catalogs\`, `README.md` and `docs\` beside the
   programs.
   - The version shows in Help > About.
   - Older publish folders are deleted, except any a running program uses.
3. **Windows.** It runs the 2GS and 4GA-8 smoke tests on the published `MonoSearch.exe`, then packages the
   folder with `windows\MonoSearch.iss` into `MonoSearch-Setup-<version>.exe`.
4. **Linux.** It assembles the AppImage in WSL, with real Unix file modes. It then runs the AppImage's command
   line there: the 2GS rank 1 must be the same line as on Windows, and all five formats must be written.
5. **macOS.** It builds `MonoSearch.app` and zips it with Unix file modes. The zip goes to a temporary
   prerelease of the release repository, and the `mac-package` workflow runs on a GitHub-hosted Mac:
   - it signs the app ad hoc with Apple's `codesign` and verifies it as Gatekeeper does;
   - it runs the command line, whose 2GS rank 1 must match the Windows one, and the five exports;
   - it opens a `.monosearch` file in the GUI through Launch Services;
   - it zips the app with `ditto` and verifies the zip again.

   The build downloads the signed zip and deletes the prerelease.
6. **Checksums.** It writes `SHA256SUMS.txt`, with Unix line endings so `sha256sum -c` works.

The output is in `out\<version>\`.

### Release checklist

1. Commit MonoSearch, including its documentation, and push it.
2. Bump `VERSION` in MonoSearch-Release and commit it.
3. Run `pwsh .\build.ps1`.
4. Install the Windows package on a test machine. Run a search, export it, save the table as CSV, and open a
   `.monosearch` file by double-clicking it. A look at the macOS app on a real Mac is still worth it before a
   release.
5. Attach the packages to a GitHub release of MonoSearch:

   ```
   gh release create v<version> out\<version>\* -R jaruiz6363/MonoSearch --title "MonoSearch <version>"
   ```

Do not run the GUI from `work\publish-<version>` while building that same version. A program running from a
folder locks its files, and the build stops rather than overwrite it.

### Known limits

- **Windows is not code-signed.** SmartScreen warns on first run.
- **macOS is signed ad hoc, not notarized.** The first launch needs right-click > Open (`README-macOS.txt` in
  the zip explains). Apple Silicon only; an `osx-x64` build would cover Intel Macs.
- **Linux: the GUI needs the usual X11 desktop libraries** (libX11, libICE, libSM, fontconfig). Every desktop
  distribution has them. A minimal system or WSL distribution may not: `sudo apt install libice6 libsm6`.
- **No licence page.** There is no LICENSE file yet.

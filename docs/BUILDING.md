# Building MonoSearch

## What you need

- Windows x64.
- Either of:
  - **Visual Studio 2022**, version 17.8 or later, with the ".NET desktop development" workload;
  - the **.NET 8 SDK** or newer.
- **git**.

Nothing else needs fetching. AberrationCalculator, whose IO library writes the OSLO, Optalix, Optiland and LHLT
files, is part of this repository: it is a `git subtree` in `external/AberrationCalculator` (see below). There
are no machine-specific paths. A plain clone builds as it is.

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

  Then build and run the quick checks below. Commit fixes to AberrationCalculator in its own repository and pull
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

The release smoke tests check the 2GS and 4GA-8 rank 1 (below). [VERIFICATION.md](VERIFICATION.md) has the
full checks.

### The icon

`src\MonoSearch.App\Assets\make-icon.ps1` draws `icon.png` and `icon.ico`. Both are committed, so run it again
only to change the picture:

```
pwsh src/MonoSearch.App/Assets/make-icon.ps1
```

## Windows installer

The installer is built from the private repository `https://github.com/jaruiz6363/MonoSearch-Release` by its
`build.ps1`. Building it also needs **Inno Setup 6**.

```
pwsh .\build.ps1                            version from VERSION, MonoSearch from GitHub at main
pwsh .\build.ps1 -Version 0.2.3 -Ref v0.2.3
pwsh .\build.ps1 -Source <local clone>      a local clone instead, to test before pushing
```

`build.ps1` works in four steps.

1. **Clone.** It clones MonoSearch, with its AberrationCalculator subtree, into `work\src` at the given
   commit.
   - Uncommitted edits are not built. The script warns about them.
   - The MonoSearch commit and the AberrationCalculator subtree commit are written to `BUILDINFO.txt` in the
     installed folder.
2. **Publish.** It publishes the GUI and the command line self-contained for win-x64 into
   `work\publish-<version>`: one folder, with `catalogs\`, `README.md` and `docs\` beside the programs. Users
   install no .NET runtime.
   - The version shows in Help > About.
   - Older publish folders are deleted, except any a running program uses.
3. **Smoke test.** It runs the 2GS and 4GA-8 smoke tests on the published `MonoSearch.exe`. A failure stops the
   build.
4. **Package.** It packages the folder with `windows\MonoSearch.iss` into
   `out\<version>\MonoSearch-Setup-<version>.exe`, and writes `SHA256SUMS.txt`.

### Release checklist

1. Commit MonoSearch, including its documentation, and push it.
2. Bump `VERSION` in MonoSearch-Release and commit it.
3. Run `pwsh .\build.ps1`.
4. Install the result on a test machine. Run a search, export it, save the table as CSV, and open a
   `.monosearch` file by double-clicking it.

Do not run the GUI from `work\publish-<version>` while building that same version. A program running from a
folder locks its files, and the build stops rather than overwrite it.

### Known limits

- **Not code-signed.** SmartScreen warns on first run.
- **Windows x64 only.** Avalonia would allow Linux and macOS packages.
- **No licence page.** There is no LICENSE file yet.

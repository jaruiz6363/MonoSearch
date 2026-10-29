# Building MonoSearch

## Prerequisites

- Windows x64.
- **.NET 9 SDK.**
- **git.**
- **AberrationCalculator**, cloned from `https://github.com/jaruiz6363/AberrationCalculator`. Its IO library
  writes the OSLO, Optalix, Optiland and LHLT files. It is referenced by path, not as a package:
  - the default path is `C:\GIT\AberrationCalculator`;
  - another location is passed with `-p:AberrationCalculatorDir=<folder>`.

## Projects

| Project | Output | |
|---|---|---|
| `src\MonoSearch.Core` | library | The searches (2GS, 4GA-8), ray traces, glass catalogs, settings file, exporters, layout |
| `src\MonoSearch` | `MonoSearch.exe` | Command line |
| `src\MonoSearch.App` | `MonoSearchApp.exe` | GUI (Avalonia 11.2.3) |

The `catalogs\*.AGF` files are copied next to every program that references MonoSearch.Core. The programs read
them from there.

## Build and run

```
dotnet build MonoSearch.sln -c Release
```

To use AberrationCalculator from another folder:

```
dotnet build MonoSearch.sln -c Release -p:AberrationCalculatorDir=D:\src\AberrationCalculator
```

To run:

```
src\MonoSearch.App\bin\Release\net9.0\MonoSearchApp.exe
src\MonoSearch\bin\Release\net9.0\MonoSearch.exe search --f 12 --fno 1.71 --export 10 --out results
```

The `results\` folder at the repository root is ignored by git.

### Quick checks after a change

```
MonoSearch.exe check
MonoSearch.exe search --f 12 --fno 1.71 --top 5 --export 1 --no-optiland-install --out %TEMP%\ms
MonoSearch.exe readback --dir %TEMP%\ms
MonoSearch.exe search --arch 4GA-8 --cores 1 --grouping coarse --top 20 --export 1 --formats zmx,lhlt --no-optiland-install --out %TEMP%\ms4
```

| Command | Expected |
|---|---|
| `check` | 1787 of 1799 glasses agree with their catalog nd |
| 2GS search | Rank 1 is OHARA:S-LAH79 / SUMITA:K-LaSFn9 |
| `readback` | Every format gives the same prescription and focal length |
| 4GA-8 search | Takes about 2 minutes. No "could not be started" line, and rank 1 is on the K-LaSFn9 core. |

These are the release smoke tests (below). [VERIFICATION.md](VERIFICATION.md) has the full checks.

### The icon

`src\MonoSearch.App\Assets\make-icon.ps1` draws `icon.png` and `icon.ico`. Both are committed, so run it again
only to change the picture:

```
pwsh src/MonoSearch.App/Assets/make-icon.ps1
```

## Windows installer

The installer is built from the private repository `jaruiz6363/MonoSearch-Release`, kept at
`C:\GIT\MonoSearch-Release`, by its `build.ps1`. Building it also needs **Inno Setup 6**.

```
pwsh .\build.ps1                            version from VERSION, both sources at HEAD
pwsh .\build.ps1 -Version 0.2.3 -Ref v0.2.3
pwsh .\build.ps1 -AcRef 7f03e09             AberrationCalculator at a given commit
```

`build.ps1` works in four steps.

1. **Clone.** It clones MonoSearch and AberrationCalculator into `work\` at the given commits.
   - Uncommitted edits are not built. The script warns about them.
   - Both commits are written to `BUILDINFO.txt` in the installed folder.
2. **Publish.** It publishes the GUI and the command line self-contained for win-x64 into
   `work\publish-<version>`: one folder, with `catalogs\` and the documentation beside the programs.
   - The version shows in Help > About.
   - Older publish folders are deleted, except any a running program uses.
3. **Smoke test.** It runs the 2GS and 4GA-8 smoke tests on the published `MonoSearch.exe`. A failure stops the
   build.
4. **Package.** It packages the folder with `windows\MonoSearch.iss` into
   `out\<version>\MonoSearch-Setup-<version>.exe`, and writes `SHA256SUMS.txt`.

### Release checklist

1. Commit MonoSearch, including its documentation, and AberrationCalculator if it changed. Push both.
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

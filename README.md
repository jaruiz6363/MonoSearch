# MonoSearch

MonoSearch is a global glass search for monocentric lenses: lenses whose surfaces are all spheres about one
centre, with the stop at the centre and a spherical image. It tries every combination of glasses from the
vendor catalogs, optimises the radii of each, ranks them, and exports the best as lens files for
OpticStudio, OSLO, Optalix, Optiland and LensHH-LT.

| Architecture | Lens | After |
|---|---|---|
| **2GS** | Two-glass symmetric: a ball inside a shell | Stamenov, Agurok and Ford, Appl. Opt. 51, 7648 (2012) |
| **4GA-8** | Four-glass asymmetric with an air gap: front shell, core, rear shell, air, meniscus | Stamenov, Agurok and Ford, Appl. Opt. 52, 8287 (2013) |

For the 2012 paper's 12 mm F/1.71 lens, MonoSearch finds the paper's best glass pair, S-LAH79 / K-LaSFn9,
first among about a million pairs in a few seconds. Exported to OpticStudio, it gives MTF 0.646 at 200 lp/mm,
against the paper's 0.65.

## Download

Ready-made programs are attached to each release on the
[Releases page](https://github.com/jaruiz6363/MonoSearch/releases). There is no need to build MonoSearch.

| File | System |
|---|---|
| `MonoSearch-Setup-<version>.exe` | Windows 10 and 11 (x64): installer |
| `MonoSearch-<version>-macOS-arm64.zip` | macOS 11 or later, Apple Silicon |
| `MonoSearch-<version>-x86_64.AppImage` | Linux x86_64 |

Each includes the .NET runtime, the glass catalogs and the documents below. The
[user guide](docs/USER_GUIDE.md#1-installing-and-starting) explains how to install each one.

## Documents

| | |
|---|---|
| [User guide](docs/USER_GUIDE.md) | Installing, every setting, running 2GS and 4GA-8 searches, exports and CSV, the command line, common questions |
| [Verification](docs/VERIFICATION.md) | What has been checked against the papers and OpticStudio, with the results, and what is not yet checked |
| [References](docs/REFERENCES.md) | The papers and which of their equations are used, numerical methods, glass data sources, file formats, software |
| [Building](docs/BUILDING.md) | Building from source, quick checks, and building the Windows installer |

## Quick start

GUI: start **MonoSearch**, keep the defaults (f = 12 mm, F/1.71, 470/550/650 nm), press **Run** (F5) and
select a row to see the lens drawn. **Export Top N** writes lens files to `Documents\MonoSearch\results`.

Command line:

```
MonoSearch.exe search --f 12 --fno 1.71 --lambda 0.47,0.55,0.65 --export 10 --out results
MonoSearch.exe search --arch 4GA-8 --cores 1 --grouping coarse
MonoSearch.exe --help
```

## Repository

```
catalogs\              vendor glass catalogs (AGF) and MISC.AGF
src\MonoSearch.Core\   searches, ray traces, catalogs, settings, exporters
src\MonoSearch\        command line (MonoSearch.exe)
src\MonoSearch.App\    GUI (MonoSearchApp.exe, Avalonia)
docs\                  the documents above
external\AberrationCalculator\   git subtree: lens model, and the writers and readers of all five export formats
```

To build it yourself, open `MonoSearch.sln` in Visual Studio 2022, or run
`dotnet build MonoSearch.sln -c Release` with the .NET 8 SDK or newer on Windows, macOS or Linux. A plain
clone builds as it is: AberrationCalculator comes with it. See [Building](docs/BUILDING.md).

## Glass data

The catalogs are the glass makers' own AGF files: SCHOTT, OHARA, HOYA, SUMITA and CDGM. `MISC.AGF` adds
Corning HPFS 7980 fused silica from Corning's data sheet. Nothing from an OpticStudio installation is bundled.
On export, MonoSearch reads your own OpticStudio Glasscat folder, read only, so the ZMX files name glasses
OpticStudio can find.

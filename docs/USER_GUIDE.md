# MonoSearch user guide

MonoSearch finds the best glass combinations for monocentric lenses: lenses whose surfaces are all spheres
about one centre, with the stop at that centre and a spherical image. It searches two designs:

| Architecture | Lens | Paper |
|---|---|---|
| **2GS** | Two-glass symmetric: a glass ball inside a glass shell, the same shell on both sides. Two radii, r1 (shell) and r2 (ball). | Stamenov, Agurok and Ford, Appl. Opt. 51, 7648 (2012) |
| **4GA-8** | Four-glass asymmetric with an air gap: front shell, core ball, rear shell, air, meniscus. Five radii R1, R2, R4, R5, R6. | Stamenov, Agurok and Ford, Appl. Opt. 52, 8287 (2013), Fig. 9 |

Every combination of glasses from the vendor catalogs is tried, and the radii of each are optimised. The
result is a ranked table, which can be exported as lens files for OpticStudio, Code V, OSLO, Optalix, Optiland and
LensHH-LT.

See [REFERENCES.md](REFERENCES.md) for the papers and data sources, and [VERIFICATION.md](VERIFICATION.md)
for what has been checked against the papers and OpticStudio.

---

## 1. Installing and starting

Download the package for your system from the
[Releases page](https://github.com/jaruiz6363/MonoSearch/releases). Each package holds the window (GUI), the
command line (section 8), the glass catalogs and this documentation. Nothing else needs installing: the .NET
runtime is included. There is no need to build MonoSearch yourself.

### Windows 10 and 11

Run `MonoSearch-Setup-<version>.exe`. The installer is not code-signed, so Windows may show "Windows
protected your PC". Click **More info**, then **Run anyway**.

The installer installs for the current user by default. Its optional tasks are:

- a desktop icon (off);
- opening `.monosearch` files with MonoSearch (on);
- adding the command line `MonoSearch.exe` to PATH (off).

The Start menu gets MonoSearch, the README, this user guide, the glass catalogs folder and the uninstaller.
Uninstalling leaves your settings files and results alone.

`MonoSearchApp.exe` is the window. `MonoSearch.exe` in the same folder is the command line.

### macOS 11 or later, Apple Silicon

Unzip `MonoSearch-<version>-macOS-arm64.zip` and drag `MonoSearch.app` into Applications.

- **First launch:** the app is signed but not notarized by Apple, so macOS says it cannot verify the developer.
  Right-click (or Control-click) the app, choose **Open**, then **Open** again. After that a double-click is
  enough.
- **If macOS says the app "is damaged"**, the download was quarantined. In Terminal:
  `xattr -dr com.apple.quarantine /Applications/MonoSearch.app`
- **Command line:** `/Applications/MonoSearch.app/Contents/MacOS/MonoSearch`.
- **Shortcuts:** Cmd works as well as Ctrl, so Cmd+S saves.

Double-clicking a `.monosearch` file opens it in MonoSearch. `README-macOS.txt` in the zip repeats these
steps.

### Linux x86_64

`MonoSearch-<version>-x86_64.AppImage` is the whole program in one file. Make it executable and run it:

```
chmod +x MonoSearch-<version>-x86_64.AppImage
./MonoSearch-<version>-x86_64.AppImage                     the window
./MonoSearch-<version>-x86_64.AppImage --cli search ...    the command line
```

- **Libraries:** the window needs the usual X11 desktop libraries (libX11, libICE, libSM, fontconfig), which
  every desktop distribution has. On a minimal system: `sudo apt install libice6 libsm6`.
- **If the AppImage will not start because FUSE is missing**, add `--appimage-extract-and-run` before the other
  arguments.
- **To type `monosearch` in a terminal**, put a two-line wrapper on your PATH:
  `exec /path/to/MonoSearch-<version>-x86_64.AppImage --cli "$@"`.

### Where results go on macOS and Linux

The default output folder is `~/Documents/MonoSearch/results`, or `~/MonoSearch/results` if you have no
`Documents` folder. On Windows it is `Documents\MonoSearch\results`.

---

## 2. The window

Settings are on the left and results on the right.

The right side, from top to bottom:

- **Buttons:** Run, Stop, Export Top N, Export Selected, Save Table as CSV..., Open Output Folder, Use Cores
  for 4GA-8.
- **Summary:** what the search is doing, or what it found.
- **Results table.**
- **Drawing** of the selected design.

The bottom line of the window is the status bar.

### Menus and keys

| | |
|---|---|
| File > New (Ctrl+N), Open (Ctrl+O), Save (Ctrl+S), Save As (Ctrl+Shift+S), Exit | Settings files (section 6) |
| Search > Run (F5) | Start a search |
| Search > Stop (keep results so far) | Stop and keep what has been found |
| Search > Export Top N, Export Selected, Save Table as CSV..., Open Output Folder | Same as the buttons |
| Help > About | Version |

---

## 3. Settings

### Lens

| Setting | Default | Meaning |
|---|---|---|
| Focal length (mm) | 12 | Effective focal length f. |
| F-number | 1.71 | Entrance pupil diameter = f / F-number, as OpticStudio's image-space F/#. The 2013 paper uses a different convention; see [VERIFICATION.md](VERIFICATION.md#aperture-convention). |
| Min back focus (mm) | 0 | Clearance needed from the last surface to the image, for example for a sensor cover or fibre bundle. 0 accepts any image outside the lens. Must be below 0.85 f. In 2GS the r1 scan stops at f − min back focus, so each pair's best design within the limit is found rather than its unconstrained best rejected. The refocused back focus is then checked exactly. |
| Half field of view (deg) | 60 | The fields written to the exported files (on axis, half and full field) and drawn. It does **not** change the ranking (section 9). Must be below 90. |

### Wavelengths and weights

This section is a table of wavelengths in micrometres, each with a weight. You can give 1 to 24 wavelengths.
**Add**, **Remove** and **Sort** edit the table. The default is 0.47, 0.55 and 0.65 µm with equal weights.

- **Primary (row number):** the reference wavelength for the drawing and the exported files.
- **Automatic design wavelength:** the wavelength at which the focal length is met. When this is ticked, it
  is the d line (0.5875618 µm) if the band contains it, otherwise the primary. Untick it to type your own
  value.
- **One wavelength** makes a monochromatic search, for a laser or a narrow band. Dispersion then plays no
  part, so high-index shells win.
- Every glass must have data over the whole band. Glasses that do not are left out.

### Glass catalogs

The bundled catalogs are SCHOTT, OHARA, HOYA, SUMITA, CDGM and MISC. Tick the ones to search.

- **Include obsolete glasses:** off by default. Obsolete glasses are those the vendor's catalog marks
  obsolete.
- **Max CTE difference (10⁻⁶/K):** off by default. When it is on, only combinations whose cemented glasses
  differ in thermal expansion by at most this much are kept, because a large difference strains or breaks a
  cemented surface as the temperature changes.
  - The 2013 paper cites below 1.5 for cemented surfaces in outdoor optics.
  - The CTE is the −30 to +70 °C value from each catalog.
  - Glasses whose catalog gives no CTE are left out while the limit is on (6 of the bundled usable glasses).
  - In 2GS the shell and ball are tested. In 4GA-8 both shells are tested against the core; the meniscus is
    in air and is free.

### Architecture

Choose **2GS** or **4GA-8**. The 4GA-8 settings below apply only to 4GA-8.

| Setting | Default | Meaning |
|---|---|---|
| Core glasses | empty | Ball glasses as `CATALOG:NAME`, comma separated, for example `SUMITA:K-GFK68`. Empty: the best ball glasses of a 2GS search with the same settings, as the paper seeds its search. |
| Cores from 2GS (if empty) | 1 | How many distinct ball glasses to take from that 2GS search. |
| Glass grouping | normal | Near-identical glasses are searched as one, and the rest of each group is tried on the best designs afterwards. Coarse is fastest and fine is most thorough (section 5). |

### Search

These settings apply to 2GS.

| Setting | Default | Meaning |
|---|---|---|
| r1 from | paper | **paper:** r1 at the global minimum of the paper's Q (Eq. 31), then ranked by wavefront, which reproduces the paper's Table 3. **direct:** r1 at the minimum of the wavefront itself. |
| Refocus | poly | **poly:** best focus by a weighted least-squares fit over all wavelengths. **eq36:** the paper's literal Eq. 36, which gives about twice the paper's published (ΔΦ)² values. |
| r1 scan points | 400 | Points in the r1 scan for each glass pair. |
| Keep top | 200 | Rows kept in the results table. |

### Export

| Setting | Default | Meaning |
|---|---|---|
| Export top N | 10 | How many designs Export Top N writes. |
| Formats | all six | ZEMAX `.zmx`, Code V `.seq`, OSLO `.len`, Optalix `.otx`, Optiland `.json`, LensHH-LT `.lhlt`. |
| Output folder | `results` | See below. |
| Install Optiland glasses into ~/.optiland/catalogs | on | See section 7. |
| OpticStudio Glasscat folder | `Documents\Zemax\Glasscat` | Read only, to write glass names OpticStudio will find. |

**Where results go.** A full path is used as it is. A relative folder such as the default `results` is taken
from the folder of the settings file. If the settings have not been saved yet, it is taken from
`Documents\MonoSearch`, so the default is `Documents\MonoSearch\results`. **Open Output Folder** opens it.

---

## 4. Running a 2GS search

1. Set the lens, wavelengths and catalogs.
2. Press **Run** (F5).

A full 2GS search tries about a million glass pairs and takes a few seconds. For each pair it:

- scans the outer radius r1, with the ball radius r2 following from the focal length (Eq. 9);
- traces the design exactly at nine pupil heights and every wavelength;
- fits the longitudinal aberration to Zernike terms and refocuses;
- scores the design by the weighted mean-square wavefront error (Eq. 38).

### The 2GS table

| Column | |
|---|---|
| # | Rank |
| Outer shell, nd, Vd | Shell glass |
| Inner ball, nd, Vd | Ball glass |
| r1, r2 | Shell and ball radii, mm |
| BFL | Back focus from the shell vertex to the image, mm |
| Image R | Radius of the spherical image, mm |
| (dPhi)^2 | The paper's polychromatic mean-square wavefront deformation, waves² |
| RMS (waves) | RMS wavefront error after refocus |
| dCTE | Shell/ball CTE difference, 10⁻⁶/K |

Click a column header to sort.

---

## 5. Running a 4GA-8 search

1. Set **Architecture** to 4GA-8.
2. Choose the core in one of two ways:
   - type the core glasses; or
   - run a 2GS search first, select one or more of its rows and press **Use Cores for 4GA-8**. Their ball
     glasses become the cores and the architecture switches to 4GA-8.

   If you leave the core empty, MonoSearch runs a 2GS search first and takes the best ball glass(es).
3. Press **Run**.

For each core, every glass is tried as the front shell, the rear shell and the meniscus. For each combination
the five radii are optimised to minimise the paper's criterion C (Eq. 11). C holds the lateral aberration at
nine pupil zones and every wavelength, the longitudinal colour between the ends of the band, and the focal
length. The search follows the paper's section 3.D, in three stages:

1. **Screen:** every combination of representative glasses, briefly optimised.
2. **Refine:** the best 400 optimised fully from three starting points.
3. **Expand:** the best 40 families retried with every glass of their groups (the paper's replacement
   glasses).

### 4GA-8 method details

- **Trace:** the exact concentric trace (the 2013 paper's Eq. 7, written for any number of shells).
- **Optimiser:** Levenberg–Marquardt with analytic derivatives, in variables that keep R2 < R1 and
  R2 < R4 < R5 < R6, with at least 0.03 f of glass and 0.01 f of air.
- **Criterion C:** lateral-aberration terms p_i ΔS(h_i, λ_j)/λ_j at nine pupil zones and every wavelength,
  plus the longitudinal colour between the band's ends at zones 1, 0.88 and 0.05. ΔS is measured from f, so C
  also holds the focal length. Wavelength weights multiply the first sum; with equal weights C is the paper's.
- **Starting radii:**
  - First, the paper's M0 = (7.0, 2.9, 4.2, 4.5, 7.8) mm, scaled to f, and the same with R1 = 6.5 and 7.5.
    M0 suits the paper's K-GFK68 core (n 1.59); with a denser core it focuses inside the lens.
  - Then the paper's element thicknesses and three thinner sets, each with the core radius solved so the
    paraxial focal length is f.
  - A combination that none of these starts is counted and reported.
- **Glass grouping:** glasses whose nd, Vd and PgF all lie within these tolerances are searched as one.

  | Grouping | nd | Vd | PgF |
  |---|---|---|---|
  | fine | 0.005 | 0.5 | 0.002 |
  | normal | 0.01 | 1 | 0.003 |
  | coarse | 0.02 | 1.5 | 0.005 |
- **Screen:** 15 iterations from the first valid start. A combination is given up after 5 iterations if it is
  still three times worse than the 400th best.
- **Refine:** the best 400, each run to convergence from three starts (R1 = 6.5, 7.0 and 7.5 mm at f = 12).
- **Ranking:** by C. Each design also gets:
  - the refocused wavefront (13 zones, Zernike C20 to C100), for the RMS column;
  - its image at best focus;
  - the back-focus check.

### How long it takes

With 9 wavelengths on 16 threads, one core takes about 4 minutes with coarse grouping, 15 minutes with
normal grouping and about an hour with fine grouping. Fewer wavelengths make it proportionally faster.

While it runs, the summary shows:

- the stage;
- the combinations screened;
- the time elapsed and the estimated time left;
- the best C so far.

### Stopping

**Stop** ends the search and keeps what it has found. The combinations screened so far are refined and ranked,
which takes a few seconds, and the summary says how much was screened. Stop does not discard anything.

MonoSearch keeps full speed when its window is in the background. It opts out of Windows 11 power
throttling (EcoQoS), which otherwise slows a program whose window is not in front.

### The 4GA-8 table

| Column | |
|---|---|
| # | Rank, by C |
| Fam | Family: designs whose glasses fall in the same groups of near-identical glasses share a family, numbered in the order of their best design |
| Front shell, Core, Rear shell, Meniscus | The four glasses |
| R1, R2, R4, R5, R6 | Radii, mm: front of the front shell, core, back of the rear shell, front and back of the meniscus |
| BFL | Back focus from the meniscus to the image, mm |
| C | The paper's criterion, Eq. 11 |
| RMS (waves) | RMS wavefront error after refocus |
| dCTE | Largest CTE difference across the two cemented surfaces, 10⁻⁶/K |

The summary also reports the best 2GS design with the same core, so you can see what the extra glasses gain.
Expect little gain for the default 12 mm F/1.71 visible lens. 4GA-8 pays off with wider bands, faster
apertures or longer focal lengths ([VERIFICATION.md](VERIFICATION.md#4ga-8)).

If some combinations could not be started, the summary gives their count. In those cases none of the starting
radii gave a valid lens.

---

## 6. The drawing and settings files

### The drawing

Select a row and its design is drawn below the table. The drawing is a real meridional ray trace at the
primary wavelength, with seven rays per field filling the stop at the centre. It shows each surface as wide as
its rays need, the stop and the spherical image. The line under the drawing gives the design and its stop
diameter. Changing **Half field of view** redraws it without a new search.

### Settings files

File > Save keeps every setting in a `.monosearch` file, which is plain JSON.

- The window title shows the file name, with `*` when there are unsaved changes.
- New, Open and closing the window ask before discarding changes.
- Double-clicking a `.monosearch` file opens it, if the installer's file association is on.
- The command line can run the same file (section 8).

---

## 7. Exporting

| Action | Writes |
|---|---|
| **Export Top N** | The ranked CSV plus the top N designs |
| **Export Selected** | The selected rows (Ctrl-click or Shift-click to select several) |
| **Save Table as CSV...** | The whole table, every ranked design in rank order, to a file you choose |

### Design folders

Each design gets its own folder in the output folder:

- 2GS: `rank001_S-LAH79_K-LaSFn9`
- 4GA-8: `rank001_<front>_<core>_<rear>_<meniscus>`

The folder holds one file per chosen format, named after the specification, for example
`f12_F1.71_paper_rank001_S-LAH79_K-LaSFn9.zmx`.

| Format | Notes |
|---|---|
| ZEMAX `.zmx` | Opens in OpticStudio. MonoSearch reads your OpticStudio Glasscat folder (read only) so the file names glasses OpticStudio has, and warns about any it does not have. |
| Code V `.seq` | Each glass is written as Code V names it, the name without punctuation plus its catalog, for example `SLAH79_OHARA`, so the vendor travels with the name. |
| OSLO `.len`, Optalix `.otx`, LensHH-LT `.lhlt` | These programs look the glasses up in their own catalogs. MISC glasses (Corning HPFS 7980) are not in LensHH-LT's. The Optalix file also gives every surface its clear aperture (as wide as the full field's rays need), since Optalix's own sizes are far too small for these lenses. |
| Optiland `.json` | Comes with a `_glass` folder holding the exact glass data, and a README. With the install option on, the glasses are also copied into `~/.optiland/catalogs`, so Optiland finds them at its next start. |

The exported files have the stop at the centre and three fields: on axis, half field and full field. The
exception is OSLO, whose `.len` file has no field list: `ANG` is the full field angle, and each OSLO analysis
asks for its field as a fraction of it (FBY). OSLO Premium can also keep a field points set; OSLO EDU cannot,
and OSLO did not save one into a `.len` in testing. Without wide-angle ray aiming, FBY is a fraction of the
object height, which for an object at infinity goes with tan θ, so a 30° field of a 60° lens is
FBY = tan 30° / tan 60° ≈ 0.333, not 0.5. The three fields do reach OSLO's lens drawing: the file sets three
drawing fans of 15 rays each, at 0, 0.333 and 1.0, drawn to the curved image surface, which is drawn too.

### CSV files

The CSV uses a decimal point and commas between columns whatever the Windows regional settings, so it opens
correctly in Excel. If Excel has the file open, saving fails with a message: close it in Excel and save again.

- 2GS columns: `rank, outer, outer_nd, outer_vd, outer_status, inner, inner_nd, inner_vd, inner_status, r1,
  r2, bfl, Q, dPhi2, rms_waves, C20, C40, C60, C80, image_radius, outer_cte, inner_cte, dcte`
- 4GA-8 columns: `rank, family, front, core, rear, meniscus, R1, R2, R4, R5, R6, image_radius, bfl, C, dPhi2,
  rms_waves, dcte`

Glasses are written as `CATALOG:NAME`, because vendors reuse names: SUMITA LASFN9 is not SCHOTT LASFN9.

---

## 8. Command line

`MonoSearch.exe` runs the same searches without the window. `MonoSearch.exe --help` lists everything.

```
MonoSearch.exe search --f 12 --fno 1.71 --lambda 0.47,0.55,0.65 --top 300 --export 10 --out results
MonoSearch.exe search --settings my.monosearch            (any option given as well overrides the file)
MonoSearch.exe search --arch 4GA-8 --core SUMITA:K-GFK68 --lambda 0.4,0.475,0.55,0.625,0.7,0.775,0.85,0.925,1.0 --fno 1.7
MonoSearch.exe search --arch 4GA-8 --cores 2 --grouping coarse
MonoSearch.exe eval --outer OHARA:S-LAH79 --inner SUMITA:K-LaSFn9 [--r1 9.049]
MonoSearch.exe check                                      (computed nd against catalog nd, every glass)
MonoSearch.exe readback --dir <folder>                    (read exported files back and compare them)
MonoSearch.exe save-settings --file my.monosearch --f 70 --fno 3.5
```

| Option | GUI setting |
|---|---|
| `--f`, `--fno`, `--min-bfl`, `--fov` | Lens |
| `--lambda`, `--weights`, `--primary`, `--design-lambda` | Wavelengths |
| `--catalogs SCHOTT,OHARA,...`, `--include-obsolete`, `--max-dcte` | Glass catalogs |
| `--arch 2GS\|4GA-8`, `--core`, `--cores`, `--grouping fine\|normal\|coarse` | Architecture |
| `--mode paper\|direct`, `--focus poly\|eq36`, `--scan`, `--top` | Search |
| `--export`, `--formats zmx,seq,len,otx,json,lhlt`, `--out`, `--no-optiland-install`, `--zemax-glasscat` | Export |

The command line takes a relative `--out` from the current folder. It always writes the ranked CSV and prints
the top designs.

---

## 9. Questions

**Why doesn't the field of view change the ranking?**
Every field sees the same lens about its own chief ray. The flat stop at the centre only foreshortens the pupil
of an oblique beam, by the cosine of the field angle in the meridional section. The image quality is therefore
the same at every field, and the search uses the on-axis beam.

**Seidel distortion is almost zero, yet OpticStudio's distortion plot shows −50 % at 60°. Which is right?**
Both are. Every chief ray passes through the centre undeviated, so the lens itself adds no distortion, and the
Seidel sum is about zero. The image, however, is a sphere of radius about f, so a field at angle θ lands at
height f·sin θ from the axis. OpticStudio's distortion plot compares that height with a reference mapping:

| Reference | Distortion | At 60° |
|---|---|---|
| F-Tan(Theta), h = f·tan θ | cos θ − 1 | −50 % |
| F-Theta, h = f·θ | sin θ / θ − 1 | −17.3 % |

This is the mapping of a spherical image, not an aberration of the lens.

**Why are my ranks different from the 2012 paper's?**
Today's catalogs hold many more glasses than the paper's: 773 non-obsolete glasses against its 446. Some of the
paper's glasses are discontinued. The paper's best family still ranks first for its specification. See
[VERIFICATION.md](VERIFICATION.md#2gs).

**How do I reproduce the 2013 paper's numbers?**
Enter F/1.77 where the paper says F/1.7. The paper's C values follow from a different aperture convention
([VERIFICATION.md](VERIFICATION.md#aperture-convention)).

**Is the result the final design?**
No. MonoSearch picks glasses and starting radii. Open the export in your design program to check it and to add
what the search does not model, such as a sensor cover, mechanical limits and tolerances.

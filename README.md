# MonoSearch

Global glass search for two-glass symmetric monocentric lenses, after
Stamenov, Agurok and Ford, "Optimization of two-glass monocentric lenses for compact panoramic imagers",
Appl. Opt. 51, 7648 (2012).

For every ordered pair of glasses (outer shell, inner ball) the outer radius r1 is scanned; r2 follows from the
focal length (Eq. 9). Each design is traced exactly with the closed-form monocentric ray trace (Eq. 29) at nine pupil
heights and three wavelengths, fitted to Zernike C20..C80 (Eq. 35), refocused, and ranked by polychromatic mean-square
wavefront deformation (Eq. 38). Pairs run in parallel (`Parallel.For`); indices are computed once before the loop.

## Projects

- `src\MonoSearch.Core` - the search, glass catalogs, settings file and exporters (shared).
- `src\MonoSearch` - the command line.
- `src\MonoSearch.App` - the GUI (Avalonia 11.2, as ARCoatingDesigner), `MonoSearchApp.exe`.

`MonoSearch.sln` builds all three: `dotnet build MonoSearch.sln -c Release`.

## GUI

`src\MonoSearch.App\bin\Release\net9.0\MonoSearchApp.exe`. Settings on the left (lens, wavelengths and weights,
catalogs, search, export), results on the right. File > New / Open / Save / Save As keep the settings in a
`.monosearch` file (JSON); the title shows the file and `*` when it has unsaved changes, and New, Open and
closing ask before discarding them. Run (F5) searches in the background with a progress bar and Cancel; the
table sorts by any column. Below the table the selected design is drawn: a real meridional trace at the primary
wavelength, 7 rays per field filling the stop at the centre (as OpticStudio's real ray aiming), each surface as wide
as its rays need, the stop, and the concentric image; the half field of view can be changed without a new run.
Export Top N writes the ranked CSV and the top N designs; Export Selected writes the
rows selected in the table (Ctrl/Shift for several). A relative output folder is taken from the settings file's
folder, or Documents\MonoSearch before the settings are saved. A .monosearch file passed on the command line
opens at start.

Architecture chooses 2GS or 4GA-8 (see below). For 4GA-8, type the core glasses or select 2GS results and press
Use Cores for 4GA-8; the results table then lists the four glasses, family and five radii, and the drawing shows
the four elements.

The command line runs the same files: `MonoSearch.exe search --settings my.monosearch` (any option given as well
overrides the file), and `MonoSearch.exe save-settings --file my.monosearch --f 70 --fno 3.5` writes one.

## Build and run

```
cd src\MonoSearch
dotnet build -c Release
bin\Release\net9.0\MonoSearch.exe search --f 12 --fno 1.71 --lambda 0.47,0.55,0.65 --top 300 --export 10 --out ..\..\results
bin\Release\net9.0\MonoSearch.exe eval --outer OHARA:S-LAH79 --inner SUMITA:K-LaSFn9
bin\Release\net9.0\MonoSearch.exe check
```

`search --export 10` writes the top 10, one folder each, as ZEMAX (.zmx), OSLO (.len), Optalix (.otx),
Optiland (.json, with its glasses in a `_glass` folder and installed into `~/.optiland/catalogs` unless
`--no-optiland-install`) and LensHH-LT (.lhlt). `--formats zmx,lhlt` picks a subset. ZMX uses MonoSearch's own
writer; the others use AberrationCalculator.IO (`C:\GIT\AberrationCalculator`, referenced by path).
`readback --dir <folder>` reads every exported file back with AberrationCalculator's readers and prints the
prescription and focal length of each, so the formats can be compared.

Wavelengths: `--lambda` takes 1 to 24 values in micrometres, `--weights` one weight each (default all 1),
`--primary` the 1-based reference wavelength (default the middle one). The score is 1.5 x the weighted mean of
the per-wavelength mean-square wavefront, which is the paper's Eq. 38 for three equal weights, and the focus is
a weighted least-squares fit. One wavelength is a monochromatic (laser or narrowband) search: dispersion drops
out and high-index shells win. `--design-lambda` (Eq. 9 and Q) defaults to the d line when it lies inside the
band, else to the primary wavelength; every glass must be defined over the band and at that wavelength.

Back focus: `--min-bfl <mm>` (default 0) requires that much clearance from the rear shell vertex to the image.
The r1 search is capped at f - min-bfl, so each pair's best design under the limit is found rather than its
unconstrained best rejected, and the refocused back focus is checked exactly. The ranked CSV has a `bfl` column.

Thermal expansion: `--max-dcte <1e-6/K>` (GUI: Max CTE difference; default off) keeps only pairs whose shell
and ball CTEs (-30 to +70 C, from each catalog's ED line) differ by at most that much, since the two are cemented.
Stamenov, Agurok and Ford, Appl. Opt. 52, 8287 (2013) cite below 1.5 for cemented surfaces in outdoor optics.
Glasses whose catalog gives no CTE are left out while the limit is on (6 of the bundled usable glasses). The
ranked CSV has `outer_cte`, `inner_cte` and `dcte` columns, and the GUI table a dCTE column.

Field of view: `--fov <deg>` (half field, default 60) sets the fields of the exported files (on axis, half and
full field) and of the drawing. It does not change the ranking: every field sees the same lens about its own chief
ray, and the flat stop at the centre only foreshortens the pupil (by cos of the field angle in the meridional section).

Options: `--catalogs SCHOTT,OHARA,HOYA,SUMITA,CDGM,MISC` (default: all bundled), `--mode paper|direct`,
`--focus poly|eq36`, `--design-lambda 0.5875618`, `--include-obsolete` (obsolete glasses are excluded by default),
`--scan 400`, `--zemax-glasscat <folder>`.

- `--mode paper`: r1 at the global minimum of Q (Eq. 31), then ranked by wavefront. `--mode direct`: r1 at the
  minimum of the wavefront itself.
- `--focus poly` (default): least-squares focus over all wavelengths. `--focus eq36`: the paper's literal Eq. 36
  (zero C20 at the centre wavelength), which gives about twice the paper's published (dPhi)^2 values.
- Glasses are identified by CATALOG:NAME. Vendors reuse names (SUMITA LASFN9 is not SCHOTT LASFN9).

## Four-glass asymmetric lens with an air gap (4GA-8)

After Stamenov, Agurok and Ford, "Optimization of high-performance monocentric lenses", Appl. Opt. 52, 8287 (2013).
That paper finds that breaking the symmetry of a two-glass lens, or adding an air gap to it, converges back to the
symmetric design in air. The gain comes with more glasses plus asymmetry and an air gap. Its 4GA-8 (Fig. 9) is a front
shell (glass 1) cemented to a core ball (glass 2, stop at the centre), a rear shell (glass 3) cemented to the back of
the core, an air gap, and a rear meniscus (glass 4): five radii R1, R2, R4, R5, R6.

```
MonoSearch.exe search --arch 4GA-8 --core SUMITA:K-GFK68 --lambda 0.4,0.475,0.55,0.625,0.7,0.775,0.85,0.925,1.0 --fno 1.7
MonoSearch.exe search --arch 4GA-8 --cores 2 --grouping coarse      (cores from a 2GS search)
```

In the GUI: Architecture = 4GA-8, then either type the core glasses or select 2GS results and press Use Cores for 4GA-8.

How it searches, as the paper's section 3.D: the core comes from the best 2GS designs; every glass is tried in the front
shell, the rear shell and the meniscus; for each combination the radii minimise the paper's criterion C (Eq. 11),
lateral-aberration terms p_i dS(h_i, lambda_j)/lambda_j at nine pupil zones and every wavelength plus the longitudinal
colour between the band's ends at zones 1, 0.88 and 0.05, with dS measured from f (so C also holds the focal length).
Wavelength weights multiply the first sum; with equal weights C is the paper's.

- The trace is the exact concentric one (the paper's Eq. 7, written for any number of shells); the radii are found by
  Levenberg-Marquardt with analytic derivatives, in variables that keep R2 < R1, R2 < R4 < R5 < R6 with at least
  0.03 f of glass and 0.01 f of air.
- Starting radii: the paper's M0 = (7.0, 2.9, 4.2, 4.5, 7.8) mm scaled to f (and R1 = 6.5, 7.5). M0 suits its
  K-GFK68 core (n 1.59); with a denser core it focuses inside the lens. Then each element-thickness profile (the
  paper's, and three thinner ones) is tried with the core radius solved so the paraxial focal length is f
  (1/f = sum of +-(1/n_before - 1/n_after)/R). A combination none of these starts is counted and reported.
- Screen: every combination of representative glasses (glasses within nd/Vd/PgF tolerances grouped: fine
  0.005/0.5/0.002, normal 0.01/1/0.003, coarse 0.02/1.5/0.005), 15 iterations from the first valid starting point,
  a combination given up after 5 iterations if still 3x worse than the 400th best. Refine: the 400 best to convergence from three starting points (R1 = 6.5, 7.0, 7.5 mm at f = 12).
  Expand: the 40 best families with every member of their glass groups (the paper's replacement glasses).
- Ranked by C. Each design also gets the refocused wavefront (13 zones, C20..C100) for the RMS column, its image at
  best focus, and the back-focus check. The best 2GS design with the same core is reported on the same measure.
- CTE limit: the front and rear shells must match the core (both cemented); the meniscus is air-spaced and free.
- Exports, drawing and CSV as for 2GS (8 surfaces).

Cost: about 4 min per core with coarse grouping and 15 min with normal, with 9 wavelengths on 16 threads; fewer
wavelengths are proportionally faster.

### Validation (the paper's 12 mm, 400-1000 nm, 9 wavelengths, core K-GFK68)

- The generic concentric trace reproduces the 2GS trace to 5e-15 mm and its (dPhi)^2 exactly.
- Local minima: for the paper's seven families and our two best, the three starting points reach the same C as 300
  random starts, so the search finds each combination's optimum.
- Aperture convention: the paper's C values (Table 6) are reproduced within 1-12% when its F/1.7 is taken as
  NA = sin(atan(1/(2 F/#))), i.e. an entrance pupil of 6.78 mm, F/1.77 in the usual sense; with NA = 1/(2 F/#) they come
  out 15-55% higher. MonoSearch keeps the usual definition (entrance pupil = f / F/#, OpticStudio's image-space F/#),
  so enter F/1.77 to reproduce the paper. The same convention moves the 2012 lens's r1 from 9.065 to 9.055 (paper 9.049).
  In that convention family I (P-LASF50 / K-GFK68 / TAF1 / E-ADF10) is the best of the nine, as in the paper
  (C 0.00321 against its 0.003253).
- Not reproduced: C at the paper's radii M0..M5 (ours 1.3x to 6x higher); the valley the paper describes is long and
  narrow, so today's glass data move its bottom (our optimum for the Table 3 glasses is C 0.00427 at R1 6.59, R6 8.71,
  along the paper's eigenvector E1 from its M5).
- OpticStudio, the exported Table 3 glasses at our optimum (F/1.7 usual sense): EFL 12.036 (paraxial, 0.7 um); on-axis
  RMS wavefront 0.112 / 0.139 / 0.062 / 0.084 waves at 0.4 / 0.475 / 0.7 / 1.0 um against ours 0.099 / 0.137 / 0.057 /
  0.081 (2-11%; the wavefront from longitudinal aberration is an approximation at this aperture); MTF at 200 lp/mm
  0.485 on axis, 0.47 at 30 deg, 0.35 T / 0.40 S at 60 deg (diffraction limit 0.70).
- Versus 2GS: the best 4GA-8 has RMS 0.085-0.094 waves against 0.160 for the best 2GS with the same core.
- Full search (normal grouping, 23.9 million combinations, 15.7 min) in the paper's convention (F/1.769): the best
  C is 0.00308, below the paper's family I under the same model (0.00321); 105 designs from about 20 families
  score below it. Margins are about 4%, within what catalog differences can move; the paper's own optimiser could
  stall in the valleys it describes, ours reaches the multi-start optimum. OpticStudio is the arbiter between them.
- 12 mm F/1.71, 470/550/650 nm, core K-LaSFn9 (the 2012 lens): the best 4GA-8 (LASF35 / K-LaSFn9 / TAFD30 /
  S-LAH79) has RMS 0.0634 waves against 0.0643 for 2GS, a slight improvement only, as the paper's Fig. 8(a) shows
  for this specification; 4GA-8 pays off with wider bands, faster apertures or longer focal lengths.

## Catalogs

`catalogs\` holds vendor-published AGF files (SCHOTT, OHARA, HOYA, SUMITA, CDGM), the same files LensHH-LT ships,
plus `MISC.AGF` for materials those catalogs lack, each from the vendor's own data: Corning HPFS 7980 fused silica
(alias F_SILICA), from Corning's July 2015 product sheet via the Synapse Optics CORNING_FS.AGF.
Nothing from an OpticStudio installation is bundled. On export the program reads the user's own OpticStudio
Glasscat folder (read only) to write catalog and glass names that OpticStudio resolves, and warns when a glass is
missing there.

## Validation (f = 12 mm, f/1.71, 470-650 nm)

- `check`: 1787 of 1799 glasses reproduce the catalog nd to 5e-5; the rest are CDGM IR glasses with placeholder nd
  and three entries whose formula disagrees with their own nd (excluded from the search).
- Per-wavelength RMS wavefront of the paper's Table 4 lens matches OpticStudio to about 3%
  (0.0347/0.0781/0.0808 vs 0.0359/0.0805/0.0810 waves).
- Rank 1 is the paper's family 1, S-LAH79 / K-LaSFn9 (r1 9.065 vs 9.049 in the paper). Its export gives MTF 0.646 at
  200 lp/mm in OpticStudio (paper: 0.65).
- r1 from Q is about 0.018 mm larger than the paper's for every family checked; not resolved.
- The paper's families 2, 3, 5 and 7 rank 492-1201 here (without obsolete glasses), because today's catalogs hold
  773 non-obsolete glasses against the paper's 446 and many newer pairs score better. Checked in OpticStudio:
  rank 3 (S-LAH79 / TAF48) MTF 0.641, rank 8 0.642, rank 9 0.627, against 0.56 for the paper's family 3.
- Families 4 and 6 (K-PSFN2, K-PSFN203) are not in the current SUMITA catalog.

## Validation of the exported files

- ZMX: opened in OpticStudio; ranks 1, 3, 8, 9 give EFL 11.997 and the expected MTF.
- Optiland: ranks 1 and 3 loaded in Optiland 0.6; EFL 11.9966 / 11.9971 as in OpticStudio, and per-wavelength
  RMS wavefront within about 0.007 waves of OpticStudio's.
- OSLO, Optalix, LHLT: read back with AberrationCalculator's readers; same prescription and EFL as the ZMX and
  Optiland files. Not yet opened in OSLO, Optalix or LensHH-LT themselves. Those programs resolve the glasses from
  their own catalogs; MISC glasses (HPFS_7980) are not in LensHH-LT's.

## Validation (AWARE2, f = 70 mm, F/3.5)

- The paper's Table 2 pair N-KZFS2 / S-FPL53 comes out with r1 34.840, r2 16.983 (paper 34.913, 16.958); rank 14
  with the default catalogs. The new rank 1 (LAF50 / K-PFK90) gives MTF 0.297 at 300 lp/mm in OpticStudio against
  0.287 for the paper's pair (470/550/650 nm).
- With 486 / 587.6 / 656 nm the new rank 1-3 are M-LAF81, L-LAM69 and D-LAF79 over a K-GFK68 ball, the family the
  2013 paper (Appl. Opt. 52, 8287, Fig. 4(b)) reports as better than the 2012 one. Their shell/ball CTE difference is
  about 4e-6/K; with `--max-dcte 1.5` the best pairs become the same shells over K-PSK400, with (dPhi)^2 0.0012
  against 0.0008.
- The fabricated lens (S-NBH8 / fused silica, asymmetric shells 31.8 and 33.378, ball 18.187) is matched by the
  symmetric optimum for that pair: r1 32.53, r2 18.22.

Full search with the defaults: 974 usable glasses, 0.95 million pairs. With obsolete glasses included
(1.76 million pairs) it took 3.4 s on 16 threads.

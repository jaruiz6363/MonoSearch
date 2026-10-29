# Verification

This document lists what has been checked, against what, and with what result, together with what is not
reproduced or not yet checked. Each figure was measured on the case named with it and says nothing beyond
that case.

Every OpticStudio check below was made on files exported by MonoSearch and opened in OpticStudio. The papers
are listed in [REFERENCES.md](REFERENCES.md): "2012" is Appl. Opt. 51, 7648 and "2013" is Appl. Opt. 52,
8287.

---

## Glass data

`MonoSearch.exe check` computes each glass's nd from its dispersion formula and compares it with the nd the
catalog states.

- 1630 of the 1642 glasses in the bundled catalogs agree to 5×10⁻⁵.
- The other 12 are nine CDGM infrared glasses (HWS1–HWS9, HWS27) with placeholder nd = 1, plus three
  entries whose formula disagrees with their own nd: CDGM H-TK9, HOYA MC-FAD61 and MC-TAF115. Those three are
  excluded from the search.

---

## 2GS

### The 2012 paper's lens: f = 12 mm, F/1.71, 470–650 nm

| Check | MonoSearch | Reference |
|---|---|---|
| Per-wavelength RMS wavefront of the paper's Table 4 lens, waves | 0.0347 / 0.0781 / 0.0808 | OpticStudio 0.0359 / 0.0805 / 0.0810 (about 3 %) |
| Rank 1 | S-LAH79 / K-LaSFn9 | the paper's family 1 |
| r1 of rank 1, mm | 9.065 | paper 9.049 |
| MTF of the rank 1 export at 200 lp/mm | 0.646 (OpticStudio) | paper 0.65 |

Further results:

- **r1 offset:** r1 from Q comes out about 0.018 mm larger than the paper's for every family checked. This
  is not resolved. Under the 2013 aperture convention (below), rank 1's r1 becomes 9.055.
- **The paper's other families:** families 2, 3, 5 and 7 rank between 492 and 1201 here, without obsolete
  glasses. Today's catalogs hold 773 non-obsolete glasses against the paper's 446, and many newer pairs score
  better. Checked in OpticStudio at 200 lp/mm:

  | Design | MTF |
  |---|---|
  | Rank 3 (S-LAH79 / TAF48) | 0.641 |
  | Rank 8 | 0.642 |
  | Rank 9 | 0.627 |
  | The paper's family 3 | 0.56 |
- **Families 4 and 6** use K-PSFN2 and K-PSFN203, which are not in the current SUMITA catalog.
- **Distortion:** in OpticStudio the rank 1 export has Seidel distortion of about zero. Its distortion plot
  shows cos θ − 1 against f·tan θ (−50 % at 60°) and sin θ/θ − 1 against f·θ (−17.3 %). This is the mapping of
  the spherical image, as expected ([USER_GUIDE.md](USER_GUIDE.md#9-questions)).

### AWARE2: f = 70 mm, F/3.5

- **The paper's Table 2 pair, N-KZFS2 / S-FPL53:**
  - r1 34.840 and r2 16.983 mm, against the paper's 34.913 and 16.958;
  - rank 14 with the default catalogs.
- **The new rank 1, LAF50 / K-PFK90:** MTF 0.297 at 300 lp/mm in OpticStudio, against 0.287 for the paper's
  pair (470/550/650 nm).
- **At 486 / 587.6 / 656 nm:** ranks 1–3 are M-LAF81, L-LAM69 and D-LAF79 over a K-GFK68 ball. The 2013 paper
  (Fig. 4(b)) reports this family as better than the 2012 one.
  - Their shell/ball CTE difference is about 4×10⁻⁶/K.
  - With `--max-dcte 1.5`, the best pairs become the same shells over K-PSK400, with (ΔΦ)² 0.0012 against
    0.0008.
- **The fabricated lens** (S-NBH8 / fused silica, asymmetric shells 31.8 and 33.378, ball 18.187) is matched by
  the symmetric optimum for that pair: r1 32.53, r2 18.22.

### Speed

A full search with the defaults uses 974 usable glasses, about 0.95 million pairs. With obsolete glasses
included (1.76 million pairs) it took 3.4 s on 16 threads.

---

## 4GA-8

The test case is the 2013 paper's: 12 mm, 400–1000 nm, 9 wavelengths, core K-GFK68, unless stated otherwise.

### Ray trace and optimiser

- The general concentric trace, written for any number of shells, reproduces the 2GS trace to 5×10⁻¹⁵ mm and
  its (ΔΦ)² exactly.
- **Local minima:** for the paper's seven families and MonoSearch's two best, the three starting points used
  by the search reach the same C as 300 random starts. The search therefore finds each combination's optimum
  for these nine.
- **2GS refactor:** after 4GA-8 was added, the 2GS exports were byte-identical to those written before.

### Aperture convention

The paper's C values (Table 6) are reproduced within 1–12 % when its F/1.7 is taken as
NA = sin(atan(1/(2 F/#))). That is an entrance pupil of 6.78 mm, F/1.77 in the usual sense. With
NA = 1/(2 F/#) they come out 15–55 % higher.

MonoSearch keeps the usual definition (entrance pupil = f / F/#, OpticStudio's image-space F/#). Enter F/1.77
to reproduce the paper. In that convention, family I (P-LASF50 / K-GFK68 / TAF1 / E-ADF10) is the best of the
paper's nine, as in the paper: C 0.00321 against its 0.003253.

### Not reproduced

C at the paper's radii M0 to M5 is 1.3 to 6 times higher here. The valley the paper describes is long and
narrow, so today's glass data move its bottom. MonoSearch's optimum for the Table 3 glasses is C 0.00427 at
R1 6.59, R6 8.71. It lies along the paper's eigenvector E1 from its M5.

### OpticStudio

The Table 3 glasses at MonoSearch's optimum were exported at F/1.7 in the usual sense.

- EFL 12.036 mm (paraxial, at 0.7 µm).
- On-axis RMS wavefront:

  | Wavelength (µm) | 0.4 | 0.475 | 0.7 | 1.0 |
  |---|---|---|---|---|
  | OpticStudio (waves) | 0.112 | 0.139 | 0.062 | 0.084 |
  | MonoSearch (waves) | 0.099 | 0.137 | 0.057 | 0.081 |

  They agree to 2–11 %. At this aperture the wavefront computed from the longitudinal aberration is an
  approximation.
- MTF at 200 lp/mm: 0.485 on axis, 0.47 at 30°, 0.35 T / 0.40 S at 60°. The diffraction limit is 0.70.

### Against 2GS

- **400–1000 nm:** the best 4GA-8 has RMS 0.085–0.094 waves, against 0.160 for the best 2GS with the same core.
- **12 mm F/1.71, 470/550/650 nm, core K-LaSFn9** (the 2012 lens): the best 4GA-8 (LASF35 / K-LaSFn9 /
  TAFD30 / S-LAH79) has RMS 0.0634 waves against 0.0643 for 2GS. This is a slight improvement only, as the
  paper's Fig. 8(a) shows for this specification.

### Full search

Normal grouping in the paper's convention (F/1.769) screened 23.9 million combinations in 15.7 minutes.

- The best C is 0.00308, below the paper's family I under the same model (0.00321). 105 designs from about 20
  families score below family I.
- The margins are about 4 %, within what catalog differences can move. The paper's own optimiser could stall in
  the valleys it describes, while MonoSearch reaches the multi-start optimum. OpticStudio is the arbiter between
  them, and this comparison has not been made in OpticStudio.

### Starting

With the paper's M0 radii alone, about 90 % of combinations with a dense core could not start,
because the lens focused inside itself. The fitted starting radii fixed this. The release smoke test (below)
requires every combination to start.

---

## Exported files

| Format | Checked | Result |
|---|---|---|
| ZMX | Opened in OpticStudio, 2GS ranks 1, 3, 8 and 9 | EFL 11.997 mm and the MTF given above |
| ZMX after the move to AberrationCalculator's writer | Old and new files of 2GS ranks 1–3 and 4GA-8 rank 1 opened in OpticStudio (0.2.3) | Same radii, thicknesses, glasses, catalogs, aperture, fields and wavelengths. EFL identical: 11.99663, 11.99692, 11.99706 and 12.00552 mm. Rank 2 (HOYA M-TAFD405, an alias case) gave the same MTF curves at every field, 0–200 lp/mm. |
| Optiland `.json` | Ranks 1 and 3 loaded in Optiland 0.6 | EFL 11.9966 / 11.9971 mm, as in OpticStudio; per-wavelength RMS wavefront within about 0.007 waves of OpticStudio's |
| OSLO `.len`, Optalix `.otx`, LensHH-LT `.lhlt` | Read back with AberrationCalculator's readers (`MonoSearch.exe readback`) | Same prescription and EFL as the ZMX and Optiland files |
| LensHH-LT `.lhlt` | A few exported files opened in LensHH-LT by the author | What was looked at matched OpticStudio. Which files and which analyses were compared was not recorded. |

---

## CSV

Both tables were saved under German regional settings, where the decimal separator is a comma. The 2GS file
had 200 rows and the 4GA-8 file 30, and every row had as many columns as the header. Rank 1's r1 was written
as `9.0654`, with a decimal point.

---

## Release smoke tests

`build.ps1` in the release repository runs these on each package before it is released. A failure stops the
build.

1. **2GS, on all three systems:** f = 12 mm, F/1.71, the bundled catalogs. Rank 1 must be S-LAH79 /
   K-LaSFn9, and its export must write all five formats.
   - Linux runs the AppImage's command line in WSL Ubuntu 22.04.
   - macOS runs the signed app's command line on a GitHub-hosted Apple Silicon Mac.
   - On both, the rank 1 line, with its radii, back focus, (ΔΦ)² and ΔCTE, must be identical to the Windows
     line.
2. **4GA-8, on Windows:** coarse grouping, with the core from a 2GS search. Every combination must start,
   rank 1 must be on the K-LaSFn9 core, and it must export to ZMX and LHLT.
3. **macOS GUI:** it must start and stay running after opening a `.monosearch` file through Launch Services. A
   screenshot is kept with the workflow run.

The Linux rank 1 line was identical to the Windows one when the AppImage was first built (0.2.3). The Linux
GUI has not been run yet.

---

## Not yet checked

- The OSLO and Optalix files have not been opened in OSLO or Optalix themselves.
- LensHH-LT has had a few files opened, without a recorded comparison (above). A check like the OpticStudio one,
  with named designs, EFL and MTF, has not been made.
- MISC glasses (Corning HPFS 7980) are not in LensHH-LT's catalogs.
- The 4GA-8 full-search winners have not been compared with the paper's family I in OpticStudio.
- The 0.018 mm r1 offset against the 2012 paper is not explained.
- The Linux GUI has not been run. The macOS package has not been tried on a Mac other than the GitHub-hosted
  one.

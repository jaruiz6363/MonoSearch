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
  is not resolved. Under the 2013 aperture convention (see [Aperture convention](#aperture-convention) under 4GA-8), rank 1's r1 becomes 9.055.
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
because the lens focused inside itself. The fitted starting radii fixed this. The [release smoke test](#release-smoke-tests)
requires every combination to start.

---

## Exported files

| Format | Checked | Result |
|---|---|---|
| ZMX | Opened in OpticStudio, 2GS ranks 1, 3, 8 and 9 | EFL 11.997 mm, and the MTF values in [2GS, the 2012 paper's lens](#2gs) |
| ZMX after the move to AberrationCalculator's writer | Old and new files of 2GS ranks 1–3 and 4GA-8 rank 1 opened in OpticStudio (0.2.3) | Same radii, thicknesses, glasses, catalogs, aperture, fields and wavelengths. EFL identical: 11.99663, 11.99692, 11.99706 and 12.00552 mm. Rank 2 (HOYA M-TAFD405, an alias case) gave the same MTF curves at every field, 0–200 lp/mm. |
| Optiland `.json` | Ranks 1 and 3 loaded in Optiland 0.6 | EFL 11.9966 / 11.9971 mm, as in OpticStudio; per-wavelength RMS wavefront within about 0.007 waves of OpticStudio's |
| OSLO `.len`, Optalix `.otx`, LensHH-LT `.lhlt` | Read back with AberrationCalculator's readers (`MonoSearch.exe readback`) | Same prescription and EFL as the ZMX and Optiland files |
| OSLO `.len` in OSLO EDU | 2GS rank 1, opened by the author (0.2.3) | The first file failed to load: OSLO allows at most 80 characters per system note and this note had 140 ("Error at sno_: system note: Argument can not contain more than 80 chars"). Fixed in AberrationCalculator's OSLO writer (49c5b7e: notes wrapped at words, notes 1–9 only). OSLO's paraxial setup then gave EFL 11.99657 mm (OpticStudio 11.99663), entrance beam radius 3.50877 mm, field angle 60°, and 2.91906 mm from the last surface to the image, as written. Its Gaussian image distance, 2.93121 mm, is the paraxial focus; the file places the image at the polychromatic best focus. |
| OSLO lens drawing, in OSLO EDU | 2GS rank 1, by the author (0.2.3), over five rounds | OSLO first drew its defaults: one 3-ray axial fan and chief rays, every ray ending on one plane, and no image surface. Each fix copies what OSLO wrote when the setting was made by hand, saved as a `.len` (AberrationCalculator 492f2cf to e24d6fd): drawing fans of 15 rays at 0, 0.333 and 1.0 of the full field (`DLNR`/`DLFP`/`DLMN`/`DLMX`, index from 0); `DLNF 3` first, since OSLO loads a `.len` with two fans and refused the third; `DRW ON` on the image surface, which OSLO otherwise takes for a dummy (air on both sides); and `DLRS 3`, rays drawn to the image surface, without which the image surface is not drawn and off-axis beams run past it. Final drawing: three beams, each focused on the drawn curved image surface. |
| Optalix layout | 2GS rank 1, by the author (0.2.3) | The first file had no apertures, and Optalix sized them itself, far smaller than the beams: every ray passed outside the lens outlines. The Optalix export now gives each surface a clear semi-diameter from the real-ray trace of the full field (without the in-app drawing's 2° edge). For 2GS rank 1 these agree with OpticStudio's automatic semi-diameters to 0.04 %: 8.5167 / 3.6927 / 1.9496 / 3.6103 / 8.0904 / 10.3791 mm against 8.5197 / 3.6928 / 1.9496 / 3.6107 / 8.0921 / 10.3793. Not yet reopened in Optalix. |
| Code V `.seq` | Read back with AberrationCalculator's Code V reader (`MonoSearch.exe readback`): 2GS ranks 1–3 and 4GA-8 rank 1 (0.2.3) | Same radii, thicknesses and glasses as the other five files, and the same EFL: 11.99663, 11.99692, 11.99706 and 12.00552 mm. Glasses written as `NAME_CATALOG` (`MTAFD405_HOYA`) read back as the right vendor's glass. |
| LensHH-LT `.lhlt` | A few exported files opened in LensHH-LT by the author: the 4GA-8 exports for f = 12 mm, F/1.71, core K-LaSFn9, ranks 1–10 | What was looked at matched OpticStudio. One of them is measured in the next section. |

### LensHH-LT against OpticStudio

One design: 4GA-8 rank 1 of the f = 12 mm, F/1.71, K-LaSFn9-core export in the last row of the table (LASF35 / K-LaSFn9 / TAFD30 / S-LAH79). Its `.lhlt` was
opened in LensHH-LT, through LensHH-LT's own tool server, and its `.zmx` in OpticStudio (MonoSearch 0.2.3). The
FFT MTF used a 128 pupil grid in LensHH-LT and sampling 3 in OpticStudio.

| | OpticStudio | LensHH-LT |
|---|---|---|
| EFL | 12.0055 mm | 12.0055 mm |
| BFL | 2.5302 mm (the image distance in the file) | 2.5357 mm |
| MTF at 210 lp/mm, on axis | 0.6568 | 0.6503 |
| MTF at 210 lp/mm, 30°, T / S | 0.6381 / 0.6528 | 0.6531 / 0.6522 |
| MTF at 210 lp/mm, 60°, T / S | 0.4915 / 0.6361 | 0.5072 / 0.6701 |

- On axis the two agree to about 1 %. Off axis LensHH-LT is up to 5 % higher, most at 60°.
- Why the off-axis values differ has not been looked into; one candidate is how each program measures MTF on a
  curved image surface.
- The cause of the 5.5 µm BFL difference has not been established either.

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
   K-LaSFn9, and its export must write all six formats.
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

- The Code V and Optalix files have not been opened in Code V or Optalix themselves. In OSLO only the 2GS rank 1
  paraxial data has been checked; no image-quality analysis yet.
- The OSLO `.len` file carries only the full field angle, so `readback` reports two fields for it (on axis and
  full) where the other formats report three. Every `.len` file on hand does the same, including ones written
  by Optalix and LensHH-LT. OSLO 6.6 itself did not save a field points set into the `.len` (a file saved by
  OSLO after the fields were set, 2026-10-29), so the evaluation fields are chosen in OSLO, per analysis. That
  file did show OSLO's lens drawing conditions, after `END`; the exports now write the three fields as drawing
  fans (see the OSLO row in [Exported files](#exported-files)).
- LensHH-LT has been compared with OpticStudio on one design only, in
  [LensHH-LT against OpticStudio](#lenshh-lt-against-opticstudio). The 1–5 % off-axis MTF difference and the
  5.5 µm BFL difference found there are not explained.
- MISC glasses (Corning HPFS 7980) are not in LensHH-LT's catalogs.
- The 4GA-8 full-search winners have not been compared with the paper's family I in OpticStudio.
- The 0.018 mm r1 offset against the 2012 paper is not explained.
- The Linux GUI has not been run. The macOS package has not been tried on a Mac other than the GitHub-hosted
  one.

# References

## Papers MonoSearch implements

**[1]** I. Stamenov, I. P. Agurok and J. E. Ford, "Optimization of two-glass monocentric lenses for compact
panoramic imagers: general aberration analysis and specific designs", *Applied Optics* **51**(31), 7648–7661
(2012). doi:10.1364/AO.51.007648

This paper is the basis of the 2GS search. MonoSearch uses:

| Paper | Used for |
|---|---|
| Eq. 9 | Ball radius r2 from shell radius r1 and the focal length, at the design wavelength |
| Eq. 29 | Exact closed-form ray trace through concentric surfaces |
| Eq. 31 | Q, whose global minimum sets r1 in `--mode paper` |
| Eq. 35 | Least-squares fit of the longitudinal aberration to Zernike C20 to C80 (MonoSearch allows more terms) |
| Eq. 36 | Literal refocus, `--focus eq36` |
| Eq. 38 | Polychromatic mean-square wavefront deformation (ΔΦ)², the 2GS ranking. With weights, MonoSearch uses 1.5 × the weighted mean, which equals Eq. 38 for three equal weights. |
| Tables 2–4 | Test cases in [VERIFICATION.md](VERIFICATION.md): AWARE2 f = 70 mm, and the f = 12 mm F/1.7 lens and its glass families |

**[2]** I. Stamenov, I. Agurok and J. E. Ford, "Optimization of high-performance monocentric lenses",
*Applied Optics* **52**(34), 8287–8304 (2013). doi:10.1364/AO.52.008287

This paper is the basis of the 4GA-8 search and of the CTE limit. MonoSearch uses:

| Paper | Used for |
|---|---|
| Fig. 9 | The 4GA-8 architecture: front shell, core, rear shell, air gap, meniscus |
| Eq. 7 | Concentric ray trace, which MonoSearch generalises to any number of shells |
| Eq. 11 | Criterion C, minimised over the radii and used for the 4GA-8 ranking |
| Section 3.D | The search: cores from the best 2GS designs, every glass in the other three places, then replacement glasses |
| M0 | Starting radii (7.0, 2.9, 4.2, 4.5, 7.8) mm at f = 12, scaled to f |
| Tables 3 and 6 | 4GA-8 test case and published C values |
| Fig. 4(b) | The K-GFK68-core 2GS family used as an AWARE2 check |
| Fig. 8(a) | Expected small gain of 4GA-8 over 2GS for the 12 mm visible lens |
| Cemented-surface CTE | The limit "below 1.5 × 10⁻⁶/K for outdoor optics", used as the suggested `--max-dcte` |

The paper also shows that a two-glass lens made asymmetric, or given an air gap, converges back to the
symmetric design. This is why MonoSearch has no two-glass asymmetric search.

## Numerical methods

**[3]** K. Levenberg, "A method for the solution of certain non-linear problems in least squares",
*Quarterly of Applied Mathematics* **2**, 164–168 (1944).

**[4]** D. W. Marquardt, "An algorithm for least-squares estimation of nonlinear parameters", *Journal of the
Society for Industrial and Applied Mathematics* **11**, 431–441 (1963).

MonoSearch optimises the 4GA-8 radii with Levenberg–Marquardt [3, 4], using analytic derivatives. Thicknesses
are written as squared excesses over a minimum (0.03 f of glass, 0.01 f of air), so every step keeps a valid
lens.

## Glass data

The bundled catalogs are the glass makers' own AGF files: SCHOTT, OHARA, HOYA, SUMITA and CDGM. They are the
same files LensHH-LT ships. No file from an OpticStudio installation is bundled.

`MISC.AGF` holds materials those catalogs lack, each from its maker's own data:

- **Corning HPFS 7980 fused silica** (also as F_SILICA), from Corning Specialty Materials, Product Information
  Sheet "HPFS 7979, 7980, 8655 Fused Silica", July 2015. The entry was copied from the Synapse Optics
  CORNING_FS.AGF v1.1.

MonoSearch reads from each AGF:

- the dispersion formula and coefficients, using the formula numbering of the OpticStudio documentation
  (Sellmeier 1–4, Herzberger, Conrady, and so on);
- nd and Vd;
- the status, which marks obsolete glasses among others;
- the wavelength range;
- the CTE from −30 to +70 °C (the first field of the ED line).

## File formats written

| Format | Program | Written by |
|---|---|---|
| `.zmx` | Ansys Zemax OpticStudio | MonoSearch's own writer |
| `.len` | OSLO (Lambda Research) | AberrationCalculator.IO |
| `.otx` | Optalix | AberrationCalculator.IO |
| `.json` | Optiland (open-source Python lens design) | AberrationCalculator.IO |
| `.lhlt` | LensHH-LT | AberrationCalculator.IO |

The Optiland export includes each glass as a refractiveindex.info-style `.yml` file in an Optiland user
catalog.

## Software

| Component | Version | Use |
|---|---|---|
| .NET | 9 | Runtime and SDK |
| Avalonia (with Avalonia.Desktop, Themes.Fluent, Controls.DataGrid) | 11.2.3 | GUI |
| CommunityToolkit.Mvvm | 8.4.0 | View-model bindings |
| MessageBox.Avalonia | 3.3.1.1 | Message boxes |
| AberrationCalculator.IO | source, by path | OSLO, Optalix, Optiland and LHLT writers and readers |
| Inno Setup | 6 | Windows installer (release repository) |

**Windows power throttling.** Both programs call `SetProcessInformation` with `ProcessPowerThrottling`
(`PROCESS_POWER_THROTTLING_STATE`, `PROCESS_POWER_THROTTLING_EXECUTION_SPEED` controlled and off), as
documented in Microsoft's Win32 API reference. This keeps Windows 11 from running a search at reduced speed
(EcoQoS) when its window is not in front.

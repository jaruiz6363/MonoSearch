using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AcGlassCatalog = AberrationCalculator.Core.Glass.GlassCatalog;

namespace MonoSearch;

/// <summary>
/// Writes one design in several lens formats, all with AberrationCalculator.IO's writers on an AberrationCalculator
/// lens model. The ZMX gets its own copy of the model, with the catalog and glass names the user's OpticStudio
/// resolves (see <see cref="ZemaxNamed"/>).
/// </summary>
public static class Exporters
{
    public static readonly string[] AllFormats = { "zmx", "seq", "len", "otx", "json", "lhlt" };

    /// <summary>Writes the requested formats into <paramref name="folder"/>; returns warnings.</summary>
    public static List<string> Write(string folder, string baseName, IEnumerable<string> formats, MonoLens lens,
        ZemaxCatalogs? zc, AcGlassCatalog acGlass, bool optilandInstall)
    {
        Directory.CreateDirectory(folder);
        var warnings = new List<string>();
        var system = Build(lens, acGlass, warnings);

        foreach (var fmt in formats.Select(f => f.Trim().ToLowerInvariant()).Distinct())
        {
            string path = Path.Combine(folder, $"{baseName}.{fmt}");
            try
            {
                switch (fmt)
                {
                    case "zmx":
                        ZmxWriter.Write(ZemaxNamed(lens, acGlass, zc, warnings), path);
                        break;
                    case "len":
                        OsloWriter.Write(system, path, acGlass);
                        break;
                    case "otx":
                        OptalixWriter.Write(WithApertures(lens, acGlass), path, acGlass);
                        break;
                    case "json":
                        var r = OptilandWriter.Write(system, path, acGlass, optilandInstall);
                        if (r.ByNameOnly.Count > 0 || r.InstallError != null) warnings.Add("Optiland: " + r.Describe());
                        break;
                    case "lhlt":
                        LhltWriter.Write(system, path);
                        break;
                    case "seq":
                        // Code V: each glass as NAME_CATALOG (SLAH79_OHARA), so the vendor travels with the name.
                        CodeVWriter.Write(system, path, acGlass);
                        break;
                    default:
                        warnings.Add($"unknown format '{fmt}' (use {string.Join(", ", AllFormats)})");
                        break;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{fmt}: not written - {ex.Message}");
            }
        }
        return warnings;
    }

    /// <summary>The lens as an AberrationCalculator system: object at infinity, the lens's surfaces with the stop at
    /// the centre, the concentric image; fields on axis, half and full field; real-ray aiming.</summary>
    public static OpticalSystem Build(MonoLens lens, AcGlassCatalog acGlass, List<string> warnings)
    {
        var sp = lens.Spec;
        var sys = new OpticalSystem
        {
            Title = lens.Title,
            Notes = lens.Note,
            Aperture = new Aperture(ApertureType.EPD, 2 * sp.H1),
            FieldType = FieldType.ObjectAngle,
            RayAiming = RayAimingMode.Real,
        };
        for (int k = 0; k < sp.Lambda.Length; k++)
            sys.Wavelengths.Add(new Wavelength(sp.Lambda[k], sp.Weight[k], k == sp.Primary));
        foreach (var a in sp.FieldAngles) sys.Fields.Add(new Field(a));

        // Bare glass names are resolved through this list in order, so order it so each glass finds its own
        // vendor's entry (SUMITA's LASFN9 is not SCHOTT's).
        sys.GlassCatalogs.AddRange(CatalogOrder(lens.Glasses, acGlass, warnings));

        void Add(string comment, double radius, double thickness, Glass? g, bool stop = false)
        {
            sys.Surfaces.Add(new Surface
            {
                Index = sys.Surfaces.Count,
                Comment = comment,
                Radius = radius,
                Thickness = thickness,
                Material = g?.Name,
                CatalogName = g?.Catalog,
                IsStop = stop,
                SemiDiameterMode = SemiDiameterMode.Auto,
            });
        }
        Add("Object at infinity", double.PositiveInfinity, double.PositiveInfinity, null);
        for (int k = 0; k < lens.Surfaces.Count; k++)
        {
            var s = lens.Surfaces[k];
            bool last = k == lens.Surfaces.Count - 1;
            Add(s.Comment, s.Radius == 0 ? double.PositiveInfinity : s.Radius, last ? 0.0 : s.Thickness, s.Glass, s.Stop);
        }
        return sys;
    }

    /// <summary>The lens model for the ZMX file: each glass under the catalog and name the user's OpticStudio Glasscat
    /// folder (read only) holds it by, which can differ from the bundled vendor file's (an older file, a renamed
    /// catalog such as HOYA20260126, or an alias). Without that folder the bundled names are kept.</summary>
    private static OpticalSystem ZemaxNamed(MonoLens lens, AcGlassCatalog acGlass, ZemaxCatalogs? zc, List<string> warnings)
    {
        var sys = Build(lens, acGlass, new List<string>());   // its catalog-order warnings are reported once, by Write
        if (zc is null) return sys;
        var names = new Dictionary<Glass, (string Catalog, string Name)>();
        foreach (var g in lens.Glasses.Distinct())
        {
            var r = zc.Resolve(g);
            if (r is null)
            {
                warnings.Add($"{g.Id} is not in the OpticStudio catalogs in {zc.Folder}; copy {Path.Combine("catalogs", g.Catalog + ".AGF")} there to open this file");
                names[g] = (g.Catalog, g.Name);
                continue;
            }
            if (!r.Value.Exact) warnings.Add($"{g.Id} -> {r.Value.Catalog}:{r.Value.Name} has the same nd but different dispersion data");
            names[g] = (r.Value.Catalog, r.Value.Name);
        }
        // Surface k + 1 of the model is surface k of the lens (surface 0 is the object).
        for (int k = 0; k < lens.Surfaces.Count; k++)
            if (lens.Surfaces[k].Glass is { } g)
                (sys.Surfaces[k + 1].CatalogName, sys.Surfaces[k + 1].Material) = names[g];
        sys.GlassCatalogs.Clear();
        sys.GlassCatalogs.AddRange(names.Values.Select(v => v.Catalog).Distinct());
        return sys;
    }

    /// <summary>The lens model for the Optalix file, with every surface's semi-diameter: as wide as the real rays of the
    /// full field need (the trace the drawing below the table uses, without the drawing's edge), the stop as the
    /// on-axis beam fills it. For 2GS rank 1 these agree with OpticStudio's automatic semi-diameters. Without
    /// them Optalix sizes the apertures itself, far smaller than a wide-field monocentric lens's beams: in its layout
    /// the rays passed outside every lens outline (Optalix, 2026-10-29). The semi-diameters stay automatic, so they
    /// clip nothing. Only this file gets them: OSLO would write them as fixed apertures and draw the lens otherwise,
    /// and the other formats write a semi-diameter only when it is fixed.</summary>
    private static OpticalSystem WithApertures(MonoLens lens, AcGlassCatalog acGlass)
    {
        var sys = Build(lens, acGlass, new List<string>());   // its catalog-order warnings are reported once, by Write
        var layout = LensLayout.Build(lens, lens.Spec.FieldAngles.Max());
        int last = lens.Surfaces.Count - 1;
        for (int k = 0; k <= last; k++)
        {
            double r = Math.Abs(lens.Surfaces[k].Radius);
            double semi = lens.Surfaces[k].Stop ? layout.StopSemi
                : k == last ? r * Math.Sin(layout.ClearImageAngle)
                : r * Math.Sin(layout.ClearAngle[k]);
            if (semi > 0) sys.Surfaces[k + 1].SemiDiameter = semi;   // surface 0 of the model is the object
        }
        return sys;
    }

    private static List<string> CatalogOrder(List<Glass> glasses, AcGlassCatalog acGlass, List<string> warnings)
    {
        var cats = glasses.Select(g => g.Catalog).Distinct().ToList();
        bool Resolves(List<string> order) => glasses.All(g => acGlass.Find(g.Name, order)?.Catalog == g.Catalog);
        foreach (var order in Permutations(cats))
            if (Resolves(order)) return order;
        warnings.Add($"{string.Join(", ", glasses.Select(g => g.Id))}: no catalog order resolves every bare name to its own vendor; "
                     + "check the glasses after opening the OSLO, Optalix and LHLT files");
        return cats;
    }

    private static IEnumerable<List<string>> Permutations(List<string> items)
    {
        if (items.Count <= 1) { yield return new List<string>(items); yield break; }
        for (int i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, k) => k != i).ToList();
            foreach (var p in Permutations(rest)) { p.Insert(0, items[i]); yield return p; }
        }
    }
}

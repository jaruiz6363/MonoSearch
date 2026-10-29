using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AcGlassCatalog = AberrationCalculator.Core.Glass.GlassCatalog;

namespace MonoSearch;

/// <summary>
/// Writes one design in several lens formats. ZMX uses MonoSearch's own writer, checked in OpticStudio;
/// OSLO, Optalix, Optiland and LHLT use AberrationCalculator.IO's writers on an AberrationCalculator lens model.
/// </summary>
public static class Exporters
{
    public static readonly string[] AllFormats = { "zmx", "len", "otx", "json", "lhlt" };

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
                        warnings.AddRange(ZmxWriter.Write(path, lens, zc));
                        break;
                    case "len":
                        OsloWriter.Write(system, path, acGlass);
                        break;
                    case "otx":
                        OptalixWriter.Write(system, path, acGlass);
                        break;
                    case "json":
                        var r = OptilandWriter.Write(system, path, acGlass, optilandInstall);
                        if (r.ByNameOnly.Count > 0 || r.InstallError != null) warnings.Add("Optiland: " + r.Describe());
                        break;
                    case "lhlt":
                        LhltWriter.Write(system, path);
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

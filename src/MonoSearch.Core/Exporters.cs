using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AcGlassCatalog = AberrationCalculator.Core.Glass.GlassCatalog;

namespace MonoSearch;

/// <summary>
/// Writes one candidate in several lens formats. ZMX uses MonoSearch's own writer, checked in OpticStudio;
/// OSLO, Optalix, Optiland and LHLT use AberrationCalculator.IO's writers on an AberrationCalculator lens model.
/// </summary>
public static class Exporters
{
    public static readonly string[] AllFormats = { "zmx", "len", "otx", "json", "lhlt" };

    /// <summary>Writes the requested formats into <paramref name="folder"/>; returns warnings.</summary>
    public static List<string> Write(string folder, string baseName, IEnumerable<string> formats, Spec sp,
        Glass outer, Glass inner, Eval e, int rank, ZemaxCatalogs? zc, AcGlassCatalog acGlass, bool optilandInstall)
    {
        Directory.CreateDirectory(folder);
        var warnings = new List<string>();
        var system = Build(sp, outer, inner, e, rank, acGlass, warnings);

        foreach (var fmt in formats.Select(f => f.Trim().ToLowerInvariant()).Distinct())
        {
            string path = Path.Combine(folder, $"{baseName}.{fmt}");
            try
            {
                switch (fmt)
                {
                    case "zmx":
                        warnings.AddRange(ZmxWriter.Write(path, sp, outer, inner, e, rank, zc));
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

    /// <summary>The candidate as an AberrationCalculator lens: object at infinity, shell, ball, stop at the centre,
    /// ball, shell, concentric image at the refocused distance; fields on axis, half and full field; real-ray aiming.</summary>
    public static OpticalSystem Build(Spec sp, Glass outer, Glass inner, Eval e, int rank, AcGlassCatalog acGlass,
        List<string> warnings)
    {
        double r1 = e.R1, r2 = e.R2, sImg = sp.F - e.DS;
        var sys = new OpticalSystem
        {
            Title = $"Monocentric rank {rank}: {outer.Name} / {inner.Name}, f={sp.F}, F/{sp.FNumber}",
            Notes = $"MonoSearch (Stamenov, Agurok, Ford, Appl. Opt. 51, 7648 (2012)). Outer {outer.Id}, inner {inner.Id}. "
                    + $"dPhi2 = {e.DPhi2:F5}.",
            Aperture = new Aperture(ApertureType.EPD, 2 * sp.H1),
            FieldType = FieldType.ObjectAngle,
            RayAiming = RayAimingMode.Real,
        };
        for (int k = 0; k < sp.Lambda.Length; k++)
            sys.Wavelengths.Add(new Wavelength(sp.Lambda[k], sp.Weight[k], k == sp.Primary));
        foreach (var a in sp.FieldAngles) sys.Fields.Add(new Field(a));

        // Bare glass names are resolved through this list in order, so order it so each glass finds its own
        // vendor's entry (SUMITA's LASFN9 is not SCHOTT's).
        sys.GlassCatalogs.AddRange(CatalogOrder(outer, inner, acGlass, warnings));

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
        Add("Outer shell front", r1, r1 - r2, outer);
        Add("Ball front", r2, r2, inner);
        Add("Stop at centre", double.PositiveInfinity, r2, inner, stop: true);
        Add("Ball back", -r2, r1 - r2, outer);
        Add("Outer shell back", -r1, sImg - r1, null);
        Add("Concentric spherical image", -sImg, 0.0, null);
        return sys;
    }

    private static List<string> CatalogOrder(Glass outer, Glass inner, AcGlassCatalog acGlass, List<string> warnings)
    {
        var cats = new[] { outer.Catalog, inner.Catalog }.Distinct().ToList();
        bool Resolves(List<string> order) =>
            acGlass.Find(outer.Name, order)?.Catalog == outer.Catalog && acGlass.Find(inner.Name, order)?.Catalog == inner.Catalog;
        if (Resolves(cats)) return cats;
        cats.Reverse();
        if (Resolves(cats)) return cats;
        warnings.Add($"{outer.Id} and {inner.Id}: no catalog order resolves both bare names to their own vendor; "
                     + "check the glasses after opening the OSLO, Optalix and LHLT files");
        return cats;
    }
}

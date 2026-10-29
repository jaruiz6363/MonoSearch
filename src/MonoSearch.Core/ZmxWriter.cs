using System.Globalization;
using System.Text;

namespace MonoSearch;

/// <summary>Writes a monocentric lens as an OpticStudio .zmx (UTF-16LE, CRLF): object at infinity, the lens's
/// surfaces with the stop at the centre, and the concentric spherical image.</summary>
public static class ZmxWriter
{
    /// <summary>Returns warnings for glasses OpticStudio may not resolve.</summary>
    public static List<string> Write(string path, MonoLens lens, ZemaxCatalogs? zc)
    {
        var ci = CultureInfo.InvariantCulture;
        var sp = lens.Spec;
        var warnings = new List<string>();
        (string Cat, string Name) Pick(Glass g)
        {
            if (zc is null) return (g.Catalog, g.Name);
            var r = zc.Resolve(g);
            if (r is null)
            {
                warnings.Add($"{g.Id} is not in the OpticStudio catalogs in {zc.Folder}; copy {Path.Combine("catalogs", g.Catalog + ".AGF")} there to open this file");
                return (g.Catalog, g.Name);
            }
            if (!r.Value.Exact) warnings.Add($"{g.Id} -> {r.Value.Catalog}:{r.Value.Name} has the same nd but different dispersion data");
            return (r.Value.Catalog, r.Value.Name);
        }
        var names = lens.Glasses.ToDictionary(g => g, Pick);
        var cats = names.Values.Select(v => v.Cat).Distinct().ToList();
        string F(double v) => v.ToString("R", ci);
        var fields = sp.FieldAngles;
        int nf = fields.Length;
        string PerField(string v) => string.Join(" ", Enumerable.Repeat(v, nf));

        var L = new List<string>
        {
            "VERS 221026 119 20120530 20120530", "MODE SEQ",
            $"NAME {lens.Title}",
            "AUTH ", "IWDP 0",
            $"NOTE 0 {lens.Note}",
            "PFIL 0 0 0", "LANG 0", "UNIT MM X W X CM MR CPMM",
            $"ENPD {F(2 * sp.H1)}", "ENVD 20 1 0", "GFAC 0 0",
            "GCAT " + string.Join(" ", cats) + " ",
            "RAIM 0 2 1 1 0 0 0 0 0 1", "PUSH 0 0 0 0 0 0", "SDMA 0 1 0", "OMMA 1 1",
            // FTYP's third and last numbers are the field count (as in every OpticStudio file checked).
            $"FTYP 0 0 {nf} {sp.NLambda} 0 0 0 {nf}", "ROPD 2", "HYPR 0", "PICB 1",
            $"XFLN {PerField("0")}", "YFLN " + string.Join(" ", fields.Select(F)), $"FWGN {PerField("1")}",
            $"VDXN {PerField("0")}", $"VDYN {PerField("0")}", $"VCXN {PerField("0")}", $"VCYN {PerField("0")}", $"VANN {PerField("0")}",
        };
        // 24 slots as OpticStudio writes them; the unused ones repeat the primary. PWAV counts from 1
        // (79 of 79 F,d,C samples shipped with OpticStudio say PWAV 2, the d line).
        for (int k = 0; k < 24; k++)
            L.Add(k < sp.NLambda ? $"WAVM {k + 1} {F(sp.Lambda[k])} {F(sp.Weight[k])}" : $"WAVM {k + 1} {F(sp.Lambda[sp.Primary])} 1");
        L.AddRange(new[]
        {
            $"PWAV {sp.Primary + 1}", "POLS 1 0 1 0 0 1 0", "GLRS 1 0",
            "GSTD 0 100.000 100.000 100.000 100.000 100.000 100.000 0 1 1 0 0 1 1 1 1 1 1",
            "NSCD 100 500 0 0.001 10 9.9999999999999995e-07 0 0 0 0 0 0 1000000 0 2",
            "COFN QF \"COATING.DAT\" \"SCATTER_PROFILE.DAT\" \"ABG_DATA.DAT\" \"PROFILE.GRD\"",
            "COFN COATING.DAT SCATTER_PROFILE.DAT ABG_DATA.DAT PROFILE.GRD",
            "LUID 6", "EERA 0 0 0 10 10",
        });

        void Surf(int i, string comm, double r, string t, Glass? g, bool stop = false)
        {
            L.Add($"SURF {i}"); L.Add($"  SSID {i}"); L.Add($"  COMM {comm}");
            if (stop) L.Add("  STOP");
            L.Add("  TYPE STANDARD"); L.Add("  FIMP "); L.Add("");
            L.Add($"  CURV {(r == 0 ? "0.0" : (1.0 / r).ToString("E18", ci))} 0 0 0 0 \"\"");
            L.Add("  HIDE 0 0 0 0 0 0 0 0 0 0 0 0"); L.Add("  MIRR 2 0");
            L.Add($"  DISZ {t}");
            // OpticStudio matches upper-case names when it reads a .zmx (K-LaSFn9 fails, K-LASFN9 resolves).
            if (g != null) L.Add($"  GLAS {names[g].Name.ToUpperInvariant()} 0 0 1.5 40 0 0 0 0 0 0 ");
            L.Add("  DIAM 0 0 0 0 1 \"\""); L.Add("  POPS 0 0 0 0 0 0 0 0 1 1 1 1 0 0 0 0");
        }
        Surf(0, "Object at infinity", 0, "INFINITY", null);
        for (int k = 0; k < lens.Surfaces.Count; k++)
        {
            var s = lens.Surfaces[k];
            bool last = k == lens.Surfaces.Count - 1;
            Surf(k + 1, s.Comment, s.Radius, last ? "0" : F(s.Thickness), s.Glass, s.Stop);
        }
        L.Add("BLNK ");
        L.Add("TOL TOFF   0   0 0.0000000000000000E+00 0.0000000000000000E+00   0 0 0 0 0");
        L.Add("MNUM 1 1");
        L.Add("MOFF   0   1 \"\" 0 0 0 1 1 0 0.0 \"\" 0");
        L.Add("");

        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(string.Join("\r\n", L))).ToArray();
        File.WriteAllBytes(path, bytes);
        return warnings;
    }
}

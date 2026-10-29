using System.Globalization;
using System.Text;

namespace MonoSearch;

/// <summary>One glass from a vendor AGF catalog. Identity is (Catalog, Name): vendors reuse names
/// (SUMITA's K-LASFN9 is stored as "LASFN9", which is also a different SCHOTT glass).</summary>
public sealed class Glass
{
    public required string Catalog { get; init; }
    public required string Name { get; init; }
    public required int Formula { get; init; }
    public required double Nd { get; init; }
    public required double Vd { get; init; }
    public required int Status { get; init; }      // 0 standard, 1 preferred, 2 obsolete, 3 special, 4 melt
    public required double[] Coef { get; init; }
    public double LambdaMin { get; set; } = 0;    // um, from the LD line
    public double LambdaMax { get; set; } = 100;
    /// <summary>Thermal expansion, -30 to +70 C, 1e-6/K, from the ED line; NaN when the catalog gives none (or 0).</summary>
    public double Cte { get; set; } = double.NaN;

    /// <summary>Other names in the same catalog with identical dispersion data (e.g. SUMITA K-LaSFn9 = LASFN9).</summary>
    public List<string> Aliases { get; } = new();

    public string Id => $"{Catalog}:{Name}";
    public string Label => Aliases.Count == 0 ? Id : $"{Id} (={string.Join("=", Aliases)})";

    public bool SameData(Glass o) =>
        Formula == o.Formula && Coef.Length == o.Coef.Length
        && Coef.Zip(o.Coef).All(p => Math.Abs(p.First - p.Second) <= 1e-12 * Math.Max(1, Math.Abs(p.First)));

    public static string StatusName(int s) => s switch
    {
        0 => "standard", 1 => "preferred", 2 => "obsolete", 3 => "special", 4 => "melt", _ => "?"
    };

    /// <summary>Catalog (air-relative) index at wavelength in micrometres. NaN for an unsupported formula.</summary>
    public double Index(double um)
    {
        double l2 = um * um, c0 = C(0), c1 = C(1), c2 = C(2), c3 = C(3), c4 = C(4), c5 = C(5);
        double n2;
        switch (Formula)
        {
            case 1: // Schott
                n2 = c0 + c1 * l2 + c2 / l2 + c3 / (l2 * l2) + c4 / (l2 * l2 * l2) + c5 / (l2 * l2 * l2 * l2);
                return Math.Sqrt(n2);
            case 2: // Sellmeier 1
                n2 = 1 + c0 * l2 / (l2 - c1) + c2 * l2 / (l2 - c3) + c4 * l2 / (l2 - c5);
                return Math.Sqrt(n2);
            case 3: // Herzberger
            {
                double L = 1.0 / (l2 - 0.028);
                return c0 + c1 * L + c2 * L * L + c3 * l2 + c4 * l2 * l2 + c5 * l2 * l2 * l2;
            }
            case 4: // Sellmeier 2
                n2 = 1 + c0 + c1 * l2 / (l2 - c2 * c2) + c3 / (l2 - c4 * c4);
                return Math.Sqrt(n2);
            case 5: // Conrady
                return c0 + c1 / um + c2 / Math.Pow(um, 3.5);
            case 6: // Sellmeier 3
                n2 = 1 + c0 * l2 / (l2 - c1) + c2 * l2 / (l2 - c3) + c4 * l2 / (l2 - c5) + C(6) * l2 / (l2 - C(7));
                return Math.Sqrt(n2);
            case 7: // Handbook of Optics 1
                n2 = c0 + c1 / (l2 - c2) - c3 * l2;
                return Math.Sqrt(n2);
            case 8: // Handbook of Optics 2
                n2 = c0 + c1 * l2 / (l2 - c2) - c3 * l2;
                return Math.Sqrt(n2);
            case 9: // Sellmeier 4
                n2 = c0 + c1 * l2 / (l2 - c2) + c3 * l2 / (l2 - c4);
                return Math.Sqrt(n2);
            case 10: // Extended
                n2 = c0 + c1 * l2 + c2 / l2 + c3 / Math.Pow(l2, 2) + c4 / Math.Pow(l2, 3) + c5 / Math.Pow(l2, 4)
                     + C(6) / Math.Pow(l2, 5) + C(7) / Math.Pow(l2, 6);
                return Math.Sqrt(n2);
            case 11: // Sellmeier 5
                n2 = 1 + c0 * l2 / (l2 - c1) + c2 * l2 / (l2 - c3) + c4 * l2 / (l2 - c5) + C(6) * l2 / (l2 - C(7))
                     + C(8) * l2 / (l2 - C(9));
                return Math.Sqrt(n2);
            case 12: // Extended 2
                n2 = c0 + c1 * l2 + c2 / l2 + c3 / Math.Pow(l2, 2) + c4 / Math.Pow(l2, 3) + c5 / Math.Pow(l2, 4)
                     + C(6) * Math.Pow(l2, 2) + C(7) * Math.Pow(l2, 3);
                return Math.Sqrt(n2);
            case 13: // Extended 3
                n2 = c0 + c1 * l2 + c2 * Math.Pow(l2, 2) + c3 / l2 + c4 / Math.Pow(l2, 2) + c5 / Math.Pow(l2, 3)
                     + C(6) / Math.Pow(l2, 4) + C(7) / Math.Pow(l2, 5) + C(8) / Math.Pow(l2, 6);
                return Math.Sqrt(n2);
            default:
                return double.NaN;
        }
    }

    private double C(int i) => i < Coef.Length ? Coef[i] : 0.0;
}

public static class AgfReader
{
    public static List<Glass> Load(string path)
    {
        string catalog = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
        var result = new List<Glass>();
        Glass? cur = null;
        foreach (var raw in ReadText(path).Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length < 3) continue;
            var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0) continue;
            switch (p[0])
            {
                case "NM" when p.Length >= 6:
                    cur = new Glass
                    {
                        Catalog = catalog,
                        Name = p[1],
                        Formula = (int)D(p[2]),
                        Nd = D(p[4]),
                        Vd = D(p[5]),
                        Status = p.Length > 7 ? (int)D(p[7]) : 0,
                        Coef = new double[10],
                    };
                    result.Add(cur);
                    break;
                case "CD" when cur != null:
                    for (int i = 1; i < p.Length && i <= cur.Coef.Length; i++) cur.Coef[i - 1] = D(p[i]);
                    break;
                case "ED" when cur != null && p.Length >= 2:
                    cur.Cte = D(p[1]) > 0 ? D(p[1]) : double.NaN;
                    break;
                case "LD" when cur != null && p.Length >= 3:
                    cur.LambdaMin = D(p[1]);
                    cur.LambdaMax = D(p[2]);
                    break;
            }
        }
        return result;
    }

    /// <summary>Merges glasses of one catalog that carry identical data under different names; the first name is kept.</summary>
    public static List<Glass> CollapseAliases(List<Glass> glasses)
    {
        var kept = new List<Glass>();
        foreach (var g in glasses)
        {
            var twin = kept.FirstOrDefault(k => k.Catalog == g.Catalog && k.SameData(g));
            if (twin is null) kept.Add(g);
            else twin.Aliases.Add(g.Name);
        }
        return kept;
    }

    private static double D(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;

    private static string ReadText(string path)
    {
        var b = File.ReadAllBytes(path);
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
        return Encoding.Latin1.GetString(b);
    }
}

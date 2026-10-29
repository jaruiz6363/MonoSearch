namespace MonoSearch;

/// <summary>
/// Read-only view of the user's own OpticStudio Glasscat folder, used only to choose catalog and glass names that
/// OpticStudio will resolve when an exported .zmx is opened. Nothing is copied from it. Users' ZEMAX catalogs are often
/// older than the bundled vendor files, or hold the same vendor file under another name (e.g. HOYA20260126.agf).
/// </summary>
public sealed class ZemaxCatalogs
{
    private readonly Dictionary<string, List<Glass>> _byName = new(StringComparer.OrdinalIgnoreCase);
    public string Folder { get; }

    private ZemaxCatalogs(string folder) => Folder = folder;

    public static ZemaxCatalogs? Open(string? folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
        var z = new ZemaxCatalogs(folder);
        foreach (var f in Directory.EnumerateFiles(folder).Where(p => p.EndsWith(".agf", StringComparison.OrdinalIgnoreCase)))
        {
            List<Glass> gs;
            try { gs = AgfReader.Load(f); } catch { continue; }
            foreach (var g in gs)
            {
                if (!z._byName.TryGetValue(g.Name, out var list)) z._byName[g.Name] = list = new List<Glass>();
                list.Add(g);
            }
        }
        return z;
    }

    /// <summary>Best OpticStudio (catalog, name) for a vendor glass: identical data under any of its names, preferring
    /// the catalog with the vendor's own name; otherwise the same name with nd within 1e-4. Null if none.</summary>
    public (string Catalog, string Name, bool Exact)? Resolve(Glass g)
    {
        var names = new[] { g.Name }.Concat(g.Aliases);
        var cands = names.SelectMany(n => _byName.TryGetValue(n, out var l) ? l : Enumerable.Empty<Glass>()).ToList();
        var exact = cands.Where(c => c.SameData(g)).OrderByDescending(c => c.Catalog == g.Catalog).FirstOrDefault();
        if (exact != null) return (exact.Catalog, exact.Name, true);
        var close = cands.Where(c => Math.Abs(c.Nd - g.Nd) < 1e-4).OrderByDescending(c => c.Catalog == g.Catalog).FirstOrDefault();
        return close is null ? null : (close.Catalog, close.Name, false);
    }
}

namespace MonoSearch;

/// <summary>One surface of a concentric lens, in ray order: signed radius (positive = centre behind it, 0 = plane),
/// thickness to the next surface, the glass after it (null = air), and whether it is the stop.</summary>
public sealed record LensSurface(string Comment, double Radius, double Thickness, Glass? Glass, bool Stop = false);

/// <summary>
/// A monocentric design as a prescription: object at infinity, the surfaces (every sphere centred on the stop),
/// and a concentric spherical image as the last surface. The exporters and the drawing work from this, so they serve
/// every architecture (2GS, 4GA-8).
/// </summary>
public sealed class MonoLens
{
    public required string Title { get; init; }
    public required string Note { get; init; }
    public required Spec Spec { get; init; }
    public required List<LensSurface> Surfaces { get; init; }

    /// <summary>The distinct glasses, in the order they first appear.</summary>
    public List<Glass> Glasses => Surfaces.Where(s => s.Glass != null).Select(s => s.Glass!).Distinct().ToList();

    public double ImageRadius => -Surfaces[^1].Radius;

    /// <summary>The 2GS lens of the 2012 paper: shell, ball, stop at the centre, ball, shell, image at the refocused distance.</summary>
    public static MonoLens From(Candidate c)
    {
        double r1 = c.E.R1, r2 = c.E.R2, sImg = c.ImageRadius;
        return new MonoLens
        {
            Title = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Monocentric rank {c.Rank}: {c.Outer.Name} / {c.Inner.Name}, f={c.Spec.F}, F/{c.Spec.FNumber}"),
            Note = $"MonoSearch (Stamenov, Agurok, Ford, Appl. Opt. 51, 7648 (2012)). Outer {c.Outer.Id}, inner {c.Inner.Id}. "
                   + $"dPhi2 = {c.E.DPhi2.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}.",
            Spec = c.Spec,
            Surfaces = new()
            {
                new("Outer shell front", r1, r1 - r2, c.Outer),
                new("Ball front", r2, r2, c.Inner),
                new("Stop at centre", 0, r2, c.Inner, Stop: true),
                new("Ball back", -r2, r1 - r2, c.Outer),
                new("Outer shell back", -r1, sImg - r1, null),
                new("Concentric spherical image", -sImg, 0, null),
            },
        };
    }

    /// <summary>The 4GA-8 lens of the 2013 paper (Fig. 9): front shell, core, stop, core, rear shell, air gap,
    /// meniscus, image at the refocused distance.</summary>
    public static MonoLens From(Ga8Candidate c)
    {
        var r = c.Radii;
        double sImg = c.ImageRadius;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return new MonoLens
        {
            Title = string.Create(ci, $"Monocentric 4GA-8 rank {c.Rank}: {c.Front.Name} / {c.Core.Name} / {c.Rear.Name} / {c.Meniscus.Name}, f={c.Spec.F}, F/{c.Spec.FNumber}"),
            Note = $"MonoSearch 4GA-8 (Stamenov, Agurok, Ford, Appl. Opt. 52, 8287 (2013)). {c.Front.Id} / {c.Core.Id} / "
                   + $"{c.Rear.Id} / {c.Meniscus.Id}. C = {c.C.ToString("F5", ci)}, dPhi2 = {c.DPhi2.ToString("F5", ci)}.",
            Spec = c.Spec,
            Surfaces = new()
            {
                new("Front shell", r.R1, r.R1 - r.R2, c.Front),
                new("Core front", r.R2, r.R2, c.Core),
                new("Stop at centre", 0, r.R2, c.Core, Stop: true),
                new("Core back", -r.R2, r.R4 - r.R2, c.Rear),
                new("Rear shell back", -r.R4, r.R5 - r.R4, null),
                new("Meniscus front", -r.R5, r.R6 - r.R5, c.Meniscus),
                new("Meniscus back", -r.R6, sImg - r.R6, null),
                new("Concentric spherical image", -sImg, 0, null),
            },
        };
    }
}

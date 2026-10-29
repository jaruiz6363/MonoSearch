using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonoSearch;

/// <summary>One wavelength of the band, in micrometres, with its weight.</summary>
public sealed class WavelengthSetting
{
    public double Micrometres { get; set; }
    public double Weight { get; set; } = 1.0;

    public WavelengthSetting() { }
    public WavelengthSetting(double um, double weight = 1.0) { Micrometres = um; Weight = weight; }
}

/// <summary>
/// Everything a search needs, as saved in a .monosearch file (JSON). The GUI edits one of these; the command
/// line can load one with --settings and override any value with its own options.
/// </summary>
public sealed class SearchSettings
{
    public const string FileExtension = ".monosearch";
    public const double DLine = 0.5875618;

    public int FormatVersion { get; set; } = 1;

    public double FocalLength { get; set; } = 12.0;
    public double FNumber { get; set; } = 1.71;
    public List<WavelengthSetting> Wavelengths { get; set; } =
        new() { new(0.47), new(0.55), new(0.65) };

    /// <summary>1-based reference wavelength.</summary>
    public int PrimaryWavelength { get; set; } = 2;

    /// <summary>Wavelength for the focal length (Eq. 9) and Q; null = the d line inside the band, else the primary.</summary>
    public double? DesignWavelength { get; set; }

    /// <summary>Minimum clearance from the rear shell vertex to the image, mm.</summary>
    public double MinBackFocus { get; set; }

    /// <summary>Half field of view, degrees: the fields of the exported files and of the drawing. A monocentric lens
    /// is the same about every chief ray, so it does not change the ranking.</summary>
    public double HalfFieldAngle { get; set; } = 60.0;

    /// <summary>Largest thermal-expansion difference allowed between the cemented shell and ball, 1e-6/K; null = no limit.
    /// Stamenov et al. 2013 cite below 1.5 for cemented surfaces in outdoor optics. Glasses without a CTE are left out.</summary>
    public double? MaxCteDifference { get; set; }

    public List<string> Catalogs { get; set; } = new() { "SCHOTT", "OHARA", "HOYA", "SUMITA", "CDGM", "MISC" };
    public bool IncludeObsolete { get; set; }

    /// <summary>"paper" (r1 from Q, Eq. 31) or "direct" (r1 from the wavefront).</summary>
    public string Mode { get; set; } = "paper";

    /// <summary>"poly" (weighted least-squares focus) or "eq36" (the paper's literal refocus).</summary>
    public string Focus { get; set; } = "poly";

    /// <summary>"2GS" (two-glass symmetric, 2012 paper) or "4GA-8" (four-glass asymmetric with an air gap, 2013 paper).</summary>
    public string Architecture { get; set; } = "2GS";

    /// <summary>4GA-8: core (ball) glasses as CATALOG:NAME. Empty = the first <see cref="CoreCount"/> distinct ball
    /// glasses of a 2GS search with the same settings, as the paper seeds its search from the best 2GS cores.</summary>
    public List<string> CoreGlasses { get; set; } = new();
    public int CoreCount { get; set; } = 1;

    /// <summary>4GA-8: how closely glasses must match to be searched as one ("fine", "normal", "coarse"); the others
    /// in a group are tried on the best designs afterwards, as the paper's replacement glasses.</summary>
    public string GlassGrouping { get; set; } = "normal";

    public int ScanPoints { get; set; } = 400;
    public int KeepTop { get; set; } = 200;

    public int ExportCount { get; set; } = 10;
    public List<string> ExportFormats { get; set; } = new(Exporters.AllFormats);
    public string OutputFolder { get; set; } = "results";
    public bool OptilandInstall { get; set; } = true;

    /// <summary>The user's OpticStudio Glasscat folder, read to pick names OpticStudio resolves; null = Documents\Zemax\Glasscat.</summary>
    public string? ZemaxGlasscat { get; set; }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json));

    public static SearchSettings Load(string path) =>
        JsonSerializer.Deserialize<SearchSettings>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException($"{path} is not a MonoSearch settings file");

    public SearchSettings Clone() => JsonSerializer.Deserialize<SearchSettings>(JsonSerializer.Serialize(this, Json), Json)!;

    public string ResolvedZemaxGlasscat =>
        ZemaxGlasscat ?? Path.Combine(UserFolders.Documents, "Zemax", "Glasscat");

    /// <summary>The design spec, after checking every value; throws ArgumentException with a message for the user.</summary>
    public Spec ToSpec()
    {
        if (!(FocalLength > 0)) throw new ArgumentException("Focal length must be positive (mm).");
        if (!(FNumber > 0.5)) throw new ArgumentException("F-number must be above 0.5.");
        if (Wavelengths.Count is < 1 or > 24) throw new ArgumentException("Give 1 to 24 wavelengths.");
        if (Wavelengths.Any(w => !(w.Micrometres > 0))) throw new ArgumentException("Wavelengths must be positive, in micrometres.");
        if (Wavelengths.Any(w => w.Weight < 0) || !(Wavelengths.Sum(w => w.Weight) > 0))
            throw new ArgumentException("Wavelength weights must be non-negative and not all zero.");
        if (PrimaryWavelength < 1 || PrimaryWavelength > Wavelengths.Count)
            throw new ArgumentException($"Primary wavelength must be 1 to {Wavelengths.Count}.");
        if (MinBackFocus < 0 || MinBackFocus >= 0.85 * FocalLength)
            throw new ArgumentException($"Minimum back focus must be from 0 to below {0.85 * FocalLength:G4} mm (0.85 f).");
        if (!(HalfFieldAngle >= 0 && HalfFieldAngle < 90)) throw new ArgumentException("Half field of view must be from 0 to below 90 degrees.");
        if (MaxCteDifference is { } dc && !(dc > 0)) throw new ArgumentException("The CTE difference limit must be positive (1e-6/K).");
        if (DesignWavelength is { } dw && !(dw > 0)) throw new ArgumentException("Design wavelength must be positive.");
        if (Mode is not ("paper" or "direct")) throw new ArgumentException("Mode must be paper or direct.");
        if (Focus is not ("poly" or "eq36")) throw new ArgumentException("Focus must be poly or eq36.");
        if (Architecture is not ("2GS" or "4GA-8")) throw new ArgumentException("Architecture must be 2GS or 4GA-8.");
        if (GlassGrouping is not ("fine" or "normal" or "coarse")) throw new ArgumentException("Glass grouping must be fine, normal or coarse.");
        if (Architecture == "4GA-8" && CoreGlasses.Count == 0 && CoreCount < 1) throw new ArgumentException("Give core glasses or a core count of at least 1.");
        if (ScanPoints < 20) throw new ArgumentException("Scan points must be at least 20.");
        if (Catalogs.Count == 0) throw new ArgumentException("Choose at least one glass catalog.");

        var lambda = Wavelengths.Select(w => w.Micrometres).ToArray();
        var weight = Wavelengths.Select(w => w.Weight).ToArray();
        int primary = PrimaryWavelength - 1;
        // The paper sets the focal length and Q at the d line; keep that inside the band, and use the primary
        // wavelength for a band that does not contain it (a SWIR glass may not be defined at 588 nm).
        double design = DesignWavelength ?? (DLine >= lambda.Min() && DLine <= lambda.Max() ? DLine : lambda[primary]);
        return new Spec(FocalLength, FNumber, lambda, weight, primary, design, Focus != "eq36", MinBackFocus, HalfFieldAngle);
    }
}

namespace MonoSearch;

/// <summary>The user's Documents folder on every platform.</summary>
public static class UserFolders
{
    /// <summary>Documents on Windows; ~/Documents on macOS and Linux when it exists, else the home folder.
    /// (.NET's MyDocuments is the home folder itself off Windows.)</summary>
    public static string Documents
    {
        get
        {
            if (OperatingSystem.IsWindows()) return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string docs = Path.Combine(home, "Documents");
            return Directory.Exists(docs) ? docs : home;
        }
    }
}

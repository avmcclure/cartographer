namespace Cartographer;

public static partial class Clones
{
    /// <summary>
    /// Fetches or updates the cached partial clone of a repository's primary location, checked out at its default
    /// branch tip, and returns the directory. <paramref name="repositoryFolder"/> is the atlas folder name.
    /// </summary>
    public static string Ensure(Atlas atlas, string repositoryFolder) =>
        throw new UsageException("cloning is not built yet; pass --path <checkout>");
}

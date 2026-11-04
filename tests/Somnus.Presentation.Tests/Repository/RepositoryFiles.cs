namespace Somnus.Presentation.Tests.Repository;

/// <summary>
/// Finds files of the repository for tests that check source files, such as XAML and resources.
/// </summary>
internal static class RepositoryFiles
{
    /// <summary>Gets the folder that holds <c>Somnus.sln</c>.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>Returns the full path of a file given relative to the repository root.</summary>
    public static string PathOf(string relativePath) => Path.Combine(Root, relativePath);

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Somnus.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Somnus.sln was not found above " + AppContext.BaseDirectory);
    }
}

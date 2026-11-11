namespace RequestsPortal.Core.Configuration;

public static class PortalPaths
{
    public static string ResolveDataDirectory(string? configuredPath = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath)
            && Path.IsPathFullyQualified(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var root = FindRoot(AppContext.BaseDirectory)
            ?? FindRoot(Directory.GetCurrentDirectory());

        if (root is null)
        {
            throw new InvalidOperationException(
                "No se encontró RequestsPortal.sln. Configura una ruta de datos absoluta.");
        }

        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(root, "data")
            : Path.GetFullPath(configuredPath, root);
    }

    private static string? FindRoot(string startingDirectory)
    {
        for (var directory = new DirectoryInfo(startingDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RequestsPortal.sln")))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}

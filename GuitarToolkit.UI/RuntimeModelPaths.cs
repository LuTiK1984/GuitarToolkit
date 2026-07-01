using System.IO;

namespace GuitarToolkit.UI;

internal static class RuntimeModelPaths
{
    private static string UserModelDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GuitarToolkit", "models");

    private static string BundledModelDirectory
    {
        get
        {
            string? assemblyDirectory = Path.GetDirectoryName(typeof(RuntimeModelPaths).Assembly.Location);
            if (!string.IsNullOrWhiteSpace(assemblyDirectory))
                return Path.Combine(assemblyDirectory, "models");

            return Path.Combine(AppContext.BaseDirectory, "models");
        }
    }

    public static string ResolveSingle(string fileName)
    {
        string userPath = Path.Combine(UserModelDirectory, fileName);
        if (File.Exists(userPath))
            return userPath;

        return Path.Combine(BundledModelDirectory, fileName);
    }

    public static (string ModelPath, string VocabularyPath) ResolvePair(string modelFileName, string vocabularyFileName)
    {
        string userModelPath = Path.Combine(UserModelDirectory, modelFileName);
        string userVocabularyPath = Path.Combine(UserModelDirectory, vocabularyFileName);
        if (File.Exists(userModelPath) && File.Exists(userVocabularyPath))
            return (userModelPath, userVocabularyPath);

        return (
            Path.Combine(BundledModelDirectory, modelFileName),
            Path.Combine(BundledModelDirectory, vocabularyFileName));
    }
}

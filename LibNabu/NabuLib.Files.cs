namespace Nabu
{
    public partial class NabuLib
    {
        public static string SafeFileName(string name)
        {
            foreach (var bad in Path.GetInvalidFileNameChars())
                name = name.Replace(bad, '_');
            return name;
        }

        public static string FilePath(AdaptorSettings settings, string filePath) {
            filePath = filePath.Replace("..", string.Empty).Replace(":", string.Empty);
            return Path.Combine(settings.StoragePath, filePath);
        }

        public static int FileSize(string path)
        {
            return (int) new FileInfo(path).Length;
        }

        public static bool IsFileAvailable(string path)
        {
            try
            {
                using FileStream inputStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return inputStream.Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public static Task<bool> FileAvailable(string path)
        {
            return Task.Run(() => {
                return IsFileAvailable(path);
            });
        }

        public static void EnsureFolder(string folder)
        {
            if (Directory.Exists(folder) is false)
                Directory.CreateDirectory(folder);
        }
    }
}

using System.IO;
using System.Text.RegularExpressions;

namespace RingKnifeDetector.Services
{
    internal static class WordFileCommitHelper
    {
        public static string CommitFile(string sourcePath, string destinationPath)
        {
            IOException? lastError = null;
            foreach (var targetPath in EnumerateSaveCandidates(destinationPath))
            {
                try
                {
                    WriteToPath(sourcePath, targetPath);
                    if (!string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(sourcePath); } catch { /* ignore */ }
                    }
                    return targetPath;
                }
                catch (IOException ex)
                {
                    lastError = ex;
                }
            }

            throw new IOException(
                TranslateIoMessage(lastError ?? new IOException("无法写入文件"), destinationPath),
                lastError);
        }

        public static IEnumerable<string> EnumerateSaveCandidates(string destinationPath)
        {
            yield return destinationPath;

            var dir = Path.GetDirectoryName(destinationPath) ?? ".";
            var ext = Path.GetExtension(destinationPath);
            var name = Path.GetFileNameWithoutExtension(destinationPath);
            var baseName = Regex.Replace(name, @"\(\d+\)$", string.Empty);

            for (var i = 2; i <= 99; i++)
                yield return Path.Combine(dir, $"{baseName}({i}){ext}");
        }

        private static void WriteToPath(string sourcePath, string destinationPath)
        {
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(destinationPath))
            {
                File.SetAttributes(destinationPath, FileAttributes.Normal);
                File.Delete(destinationPath);
            }

            File.Copy(sourcePath, destinationPath, true);
        }

        private static string TranslateIoMessage(IOException ex, string destinationPath)
        {
            if (ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("正在由另一进程使用", StringComparison.OrdinalIgnoreCase))
            {
                return $"无法写入文件，请关闭正在打开的 Word 文档后重试：{destinationPath}";
            }
            return ex.Message;
        }
    }
}

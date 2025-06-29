using System.IO;
using System.IO.Compression;


namespace ScriptInstall
{
    public static class ScriptOperations  
    {
        public static string ExtractZip(string zipFile, string extractTo)
        {
            // 1. Get all directories before extraction
            var existingDirs = new HashSet<string>(Directory.GetDirectories(extractTo)
                .Select(d => Path.GetFullPath(d).TrimEnd('\\', '/')));

            // 2. Perform the extraction
            ZipFile.ExtractToDirectory(zipFile, extractTo);

            // 3. Find newly created directories
            var newDirs = Directory.GetDirectories(extractTo)
                .Select(d => Path.GetFullPath(d).TrimEnd('\\', '/'))
                .Where(d => !existingDirs.Contains(d))
                .ToList();

            // 4. Return the appropriate directory name
            if (newDirs.Count == 1)
            {
                return Path.GetFileName(newDirs[0]);
            }
            else if (newDirs.Count > 1)
            {
                // If multiple new directories, try to find the one matching ZIP name
                string expectedName = Path.GetFileNameWithoutExtension(zipFile);
                var matchingDir = newDirs.FirstOrDefault(d =>
                    Path.GetFileName(d).Equals(expectedName, StringComparison.OrdinalIgnoreCase));

                return matchingDir != null
                    ? Path.GetFileName(matchingDir)
                    : newDirs.First(); // fallback to first new directory
            }

            // No new directories found - files were extracted directly
            return null;
        }

        public static void MoveFiles(string extractionBase,string extractedFolderName)
        {
            string dataSource = Path.Combine(extractionBase, extractedFolderName, "Data");
            string systemSource = Path.Combine(extractionBase, extractedFolderName, "system");

            string targetData = Path.Combine(extractionBase, "Data");
            string targetSystem = Path.Combine(extractionBase, "system");

            // Move files from Data folder
            if (Directory.Exists(dataSource))
            {
                foreach (var item in Directory.GetFiles(dataSource))
                {
                    string targetPath = Path.Combine(targetData, Path.GetFileName(item));
                    File.Copy(item, targetPath, true);
                }
            }

            // Move files from system folder
            if (Directory.Exists(systemSource))
            {
                foreach (var item in Directory.GetFiles(systemSource))
                {
                    string targetPath = Path.Combine(targetSystem, Path.GetFileName(item));
                    File.Copy(item, targetPath, true);
                }
            }

            Console.WriteLine("Files moved to Data and system folders.");
        }

        public static void CleanUp(string extractedFolder, string zipFile)
        {
            // Delete extracted folder and zip file
            Directory.Delete(Path.Combine(extractedFolder, "skripty"), true);
            File.Delete(zipFile);
        }
    }
}
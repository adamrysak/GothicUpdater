using System;
using System.Collections.Generic;
using System.IO;

namespace InitialSettingsLoad
{
    class InitialSettingsLoader
    {
        private string appFolder;
        private string iniPath;

        // Section dictionaries
        public Dictionary<string, string> GeneralSettings { get; private set; } = new();
        public Dictionary<string, string> PathSettings { get; private set; } = new();

        public InitialSettingsLoader()
        {
            appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GothicUpdater");
            iniPath = Path.Combine(appFolder, "settings.ini");
            InitialLoad();
            
        }

        public void InitialLoad()
        {
            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            if (!File.Exists(iniPath))
            {
                // Populate with default values
                GeneralSettings = new Dictionary<string, string>
                {
                    ["LastScriptVer"] = "9.9.2024",
                    ["LastCzechVer"] = "0",
                    ["Theme"] = "Light"
                };

                PathSettings = new Dictionary<string, string>
                {
                    ["NewBalanceFolderPath"] = ""
                };

                CreateIniFile(); // Save defaults to file
            }
            else
            {
                LoadSettingsFromFile(); // Load from existing ini
            }
        }

        private void CreateIniFile()
        {
            using var writer = new StreamWriter(iniPath, false);

            writer.WriteLine("[General]");
            foreach (var kvp in GeneralSettings)
                writer.WriteLine($"{kvp.Key}={kvp.Value}");
            writer.WriteLine();

            writer.WriteLine("[Paths]");
            foreach (var kvp in PathSettings)
                writer.WriteLine($"{kvp.Key}={kvp.Value}");
        }

        private void LoadSettingsFromFile()
        {
            string? currentSection = null;

            foreach (var line in File.ReadAllLines(iniPath))
            {
                var trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";"))
                    continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed[1..^1];
                    continue;
                }

                var parts = trimmed.Split('=', 2);
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (currentSection)
                {
                    case "General":
                        GeneralSettings[key] = value;
                        break;
                    case "Paths":
                        PathSettings[key] = value;
                        break;
                }
            }
        }

        public void SaveOrUpdateSetting(string section, string key, string value)
        {
            var lines = new List<string>(File.ReadAllLines(iniPath));
            bool sectionFound = false;
            bool keyUpdated = false;
            int insertIndex = lines.Count;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();

                if (line.Equals($"[{section}]"))
                {
                    sectionFound = true;
                    insertIndex = i + 1;

                    for (int j = insertIndex; j < lines.Count; j++)
                    {
                        var innerLine = lines[j].Trim();

                        if (innerLine.StartsWith("[") && innerLine.EndsWith("]"))
                            break; // Reached next section

                        if (innerLine.StartsWith($"{key}="))
                        {
                            lines[j] = $"{key}={value}";
                            keyUpdated = true;
                            break;
                        }
                    }

                    break;
                }
            }

            if (!sectionFound)
            {
                lines.Add($"[{section}]");
                lines.Add($"{key}={value}");
            }
            else if (!keyUpdated)
            {
                lines.Insert(insertIndex, $"{key}={value}");
            }

            File.WriteAllLines(iniPath, lines);
        }
        public void AddSection(string sectionName)
        {
            var lines = new List<string>(File.ReadAllLines(iniPath));
            bool sectionExists = lines.Any(line =>
                line.Trim().Equals($"[{sectionName}]", StringComparison.OrdinalIgnoreCase));

            if (!sectionExists)
            {
                // Ensure file ends with an empty line before appending
                if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                    lines.Add(string.Empty);

                lines.Add($"[{sectionName}]");

                File.WriteAllLines(iniPath, lines);
            }
        }


        public string GetIniPath() => iniPath;
    }
}

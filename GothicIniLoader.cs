using System.IO;
using System.Reflection;

namespace GothicIni
{
    class SwitchConfig
    {
        public string Key { get; set; }
        public string Description { get; set; }
        public string CurrentValue { get; set; }
        public string OffValue { get; set; }
        public string OnValue { get; set; }
        public int LineIndex { get; set; }
        public bool IsChecked { get; set; }
    }

    class GothicIniLoader
    {
        private string _baseDirectory;
        private Dictionary<string, int> iniLinesIndexes = new();
        private Dictionary<string, string> newValues = new();
        public List<SwitchConfig> switches = new();
        private string[] iniLines;
        private SwitchConfig ExtModeActivateConfig = new SwitchConfig
        {          
            Key = "extModeActivate",
            Description = "",
            OffValue = "0",
            OnValue = "1",
            LineIndex = -1,
        };


        public GothicIniLoader(string basePath)
        {
            _baseDirectory = basePath;
            LoadSwitchConfigsAndMatch();

            
            
        }

        private void LoadSwitchConfigsAndMatch()
        {
            string iniPath = Path.Combine(_baseDirectory, "system", "gothic.ini");

            if (!File.Exists(iniPath))
                throw new FileNotFoundException($"INI file not found at: {iniPath}");

            iniLines = File.ReadAllLines(iniPath);

            // Load embedded resource
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("GothicUpdater.Configs.SwitchConfigs.txt");
            if (stream == null)
                throw new FileNotFoundException("SwitchConfigs.txt not found in embedded resources.");

            using var reader = new StreamReader(stream);
            string? configLine;
            ExtModeActivateConfig.LineIndex = FindSettingLineIndex(iniLines, ExtModeActivateConfig.Key);
            while ((configLine = reader.ReadLine()) != null)
            {
                configLine = configLine.Trim();
                if (string.IsNullOrWhiteSpace(configLine) || configLine.StartsWith("#"))
                    continue;

                var parts = configLine.Split('=', ',');

                if (parts.Length < 3)
                    continue;

                string rawKey = parts[0].Trim();
                string offValue = parts[1].Trim();
                string onValue = parts[2].Trim();

                // Extract description if present
                string key, description;
                int descStart = rawKey.IndexOf('(');
                int descEnd = rawKey.IndexOf(')');

                if (descStart != -1 && descEnd != -1 && descEnd > descStart)
                {
                    key = rawKey.Substring(0, descStart).Trim();
                    description = rawKey.Substring(descStart + 1, descEnd - descStart - 1).Trim();
                }
                else
                {
                    key = rawKey;
                    description = "";
                }

                int lineIndex = FindSettingLineIndex(iniLines, key);
                iniLinesIndexes[key] = lineIndex;
                newValues[key] = onValue;
                string CurrentVal = GetCurrentValue(lineIndex);
                
                switches.Add(new SwitchConfig
                {
                    Key = key,
                    Description = description,
                    CurrentValue = CurrentVal,
                    OffValue = offValue,
                    OnValue = onValue,
                    LineIndex = lineIndex,
                    IsChecked = CurrentVal == "0" ? false : true
                });
            }

        }

        private int FindSettingLineIndex(string[] lines, string settingKey)
        {
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";"))
                    continue;

                if (line.StartsWith(settingKey + "=", StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private string GetCurrentValue(int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= iniLines.Length)
                return "0";

            string line = iniLines[lineIndex];
            int eqIdx = line.IndexOf('=');
            return eqIdx >= 0 && eqIdx + 1 < line.Length
                ? line.Substring(eqIdx + 1).Trim()
                : "0";
        }

        public void ApplyExtModeActivate(SwitchConfig switchState,bool extMode)
        {
            if (extMode)
            {
                iniLines[switchState.LineIndex] = $"{switchState.Key}={switchState.OnValue}";
            }
            else
            {
                iniLines[switchState.LineIndex] = $"{switchState.Key}={switchState.OffValue}";
            }
            
        }

        public void ApplySwitches(bool[] switchStates)
        {
            if (switchStates.Length != switches.Count)
                throw new ArgumentException("Switch states array does not match the number of switches.");

            for (int i = 0; i < switches.Count; i++)
            {
                var sw = switches[i];

                if (!switchStates[i])
                {
                    if (sw.Key == "extMode")
                    {
                        ApplyExtModeActivate(ExtModeActivateConfig, false);
                    }

                    iniLines[sw.LineIndex] = $"{sw.Key}={sw.OffValue}";
                    

                }
                else
                {
                    if (sw.Key == "extMode")
                    {
                        ApplyExtModeActivate(ExtModeActivateConfig, true);
                    }
                     
                    iniLines[sw.LineIndex] = $"{sw.Key}={sw.OnValue}";
                    
                }
                
            }

            File.WriteAllLines(Path.Combine(_baseDirectory, "system", "gothic.ini"), iniLines);
        }
    }
}

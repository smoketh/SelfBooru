using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SelfBooru
{
    internal class AppPreferences
    {
        private static readonly string PreferencesFile =
            Path.Combine(AppContext.BaseDirectory, "preferences.dat");

        public string OutputDir { get; set; } = "";
        public int ScanInterval { get; set; } = 300;
        public bool EnableAutoScan { get; set; } = true;

        public static AppPreferences Load()
        {
            System.Diagnostics.Debug.WriteLine($"Trying to load preferences from {PreferencesFile}");
            try
            {
                if (File.Exists(PreferencesFile))
                {
                    System.Diagnostics.Debug.WriteLine($"Loaded preferences");
                    string json = File.ReadAllText(PreferencesFile);
                    return JsonSerializer.Deserialize<AppPreferences>(json)
                           ?? new AppPreferences();
                }
            }
            catch
            {
                // Log or ignore corrupt preferences file
            }

            return new AppPreferences();
        }

        public void Save()
        {
            System.Diagnostics.Debug.WriteLine($"Trying to write preferences to {PreferencesFile}");
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PreferencesFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save preferences: {ex}");
            }
        }
    }
}

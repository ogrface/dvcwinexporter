using System.Text.Json;

namespace CrmSolutionExporter
{
    internal class UserSettings
    {
        public string ServerUrl { get; set; } = "";
        public string GitRepoPath { get; set; } = "";

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DVCWinExporter");

        private static readonly string SettingsFile = Path.Combine(SettingsDir, "usersettings.json");

        public static UserSettings Load()
        {
            if (!File.Exists(SettingsFile))
                return new UserSettings();

            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
    }
}

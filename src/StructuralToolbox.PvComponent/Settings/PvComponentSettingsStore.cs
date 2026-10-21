using System;
using System.IO;
using System.Xml.Serialization;

namespace StructuralToolbox.PvComponent.Settings
{
    internal static class PvComponentSettingsStore
    {
        private static string SettingsPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "StructuralToolbox", "pv-component", "settings.xml");
            }
        }

        public static PvComponentSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new PvComponentSettings();
                XmlSerializer serializer = new XmlSerializer(
                    typeof(PvComponentSettings));
                using (FileStream input = File.OpenRead(SettingsPath))
                {
                    PvComponentSettings settings = serializer.Deserialize(input)
                        as PvComponentSettings;
                    if (settings == null)
                        return new PvComponentSettings();
                    settings.Normalize();
                    return settings;
                }
            }
            catch
            {
                return new PvComponentSettings();
            }
        }

        public static void Save(PvComponentSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            PvComponentSettings copy = settings.Clone();
            copy.Normalize();
            string directory = Path.GetDirectoryName(SettingsPath);
            Directory.CreateDirectory(directory);
            string temporary = SettingsPath + ".tmp-" +
                Guid.NewGuid().ToString("N");
            try
            {
                XmlSerializer serializer = new XmlSerializer(
                    typeof(PvComponentSettings));
                using (FileStream output = File.Create(temporary))
                    serializer.Serialize(output, copy);
                File.Copy(temporary, SettingsPath, true);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}

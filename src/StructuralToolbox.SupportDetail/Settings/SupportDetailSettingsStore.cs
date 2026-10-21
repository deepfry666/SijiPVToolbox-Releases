using System;
using System.IO;
using System.Xml.Serialization;

namespace StructuralToolbox.SupportDetail.Settings
{
    public static class SupportDetailSettingsStore
    {
        private static readonly object SyncRoot = new object();

        public static string SettingsPath
        {
            get
            {
                string root = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, "StructuralToolbox",
                    "support-detail-settings.xml");
            }
        }

        public static SupportDetailSettings Load()
        {
            lock (SyncRoot)
            {
                try
                {
                    if (!File.Exists(SettingsPath))
                        return new SupportDetailSettings();
                    XmlSerializer serializer = new XmlSerializer(
                        typeof(SupportDetailSettings));
                    using (FileStream stream = File.OpenRead(SettingsPath))
                    {
                        SupportDetailSettings settings =
                            serializer.Deserialize(stream) as SupportDetailSettings;
                        if (settings == null)
                            return new SupportDetailSettings();
                        settings.Normalize();
                        return settings;
                    }
                }
                catch
                {
                    return new SupportDetailSettings();
                }
            }
        }

        public static void Save(SupportDetailSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            lock (SyncRoot)
            {
                settings.Normalize();
                string directory = Path.GetDirectoryName(SettingsPath);
                Directory.CreateDirectory(directory);
                string temporaryPath = SettingsPath + ".tmp";
                XmlSerializer serializer = new XmlSerializer(
                    typeof(SupportDetailSettings));
                using (FileStream stream = File.Create(temporaryPath))
                    serializer.Serialize(stream, settings);
                if (File.Exists(SettingsPath))
                    File.Delete(SettingsPath);
                File.Move(temporaryPath, SettingsPath);
            }
        }
    }
}

using System;
using System.IO;
using System.Xml.Serialization;

namespace StructuralToolbox.Layout.Settings
{
    public static class LayoutSettingsStore
    {
        private static readonly object SyncRoot = new object();
        public static string SettingsPath
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, "StructuralToolbox", "layout-settings.xml");
            }
        }
        public static LayoutSettings Load()
        {
            lock (SyncRoot)
            {
                try
                {
                    if (!File.Exists(SettingsPath)) return new LayoutSettings();
                    XmlSerializer serializer = new XmlSerializer(typeof(LayoutSettings));
                    using (FileStream stream = File.OpenRead(SettingsPath))
                    {
                        LayoutSettings settings = serializer.Deserialize(stream) as LayoutSettings;
                        if (settings == null) return new LayoutSettings(); settings.Normalize(); return settings;
                    }
                }
                catch { return new LayoutSettings(); }
            }
        }
        public static void Save(LayoutSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            lock (SyncRoot)
            {
                settings.Normalize(); string directory = Path.GetDirectoryName(SettingsPath); Directory.CreateDirectory(directory);
                string temporaryPath = SettingsPath + ".tmp"; XmlSerializer serializer = new XmlSerializer(typeof(LayoutSettings));
                using (FileStream stream = File.Create(temporaryPath)) serializer.Serialize(stream, settings);
                if (File.Exists(SettingsPath)) File.Delete(SettingsPath); File.Move(temporaryPath, SettingsPath);
            }
        }
    }
}

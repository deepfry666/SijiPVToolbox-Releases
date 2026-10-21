using System;
using System.IO;
using System.Xml.Serialization;

namespace StructuralToolbox.Settings
{
    public static class RailSettingsStore
    {
        private static readonly object SyncRoot = new object();

        public static string SettingsPath
        {
            get
            {
#if INTERNAL_VERIFY
                const string overrideVariable = "FOURJTOOLS_SETTINGS_PATH";
                string overridePath = Environment.GetEnvironmentVariable(
                    overrideVariable);
                if (!String.IsNullOrWhiteSpace(overridePath))
                {
                    if (!Path.IsPathRooted(overridePath))
                    {
                        throw new InvalidOperationException(
                            overrideVariable + " must be an absolute path.");
                    }

                    string fullOverridePath = Path.GetFullPath(overridePath);
                    string overrideDirectory = Path.GetDirectoryName(
                        fullOverridePath);
                    if (String.IsNullOrWhiteSpace(overrideDirectory) ||
                        !Directory.Exists(overrideDirectory) ||
                        !File.Exists(fullOverridePath))
                    {
                        throw new InvalidOperationException(
                            overrideVariable +
                            " must identify an existing file in an existing directory.");
                    }
                    return fullOverridePath;
                }
#endif
                string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, "StructuralToolbox", "rail-settings.xml");
            }
        }

        public static RailSettings Load()
        {
            lock (SyncRoot)
            {
                try
                {
                    if (!File.Exists(SettingsPath))
                        return new RailSettings();

                    XmlSerializer serializer = new XmlSerializer(typeof(RailSettings));
                    using (FileStream stream = File.OpenRead(SettingsPath))
                    {
                        RailSettings settings = serializer.Deserialize(stream) as RailSettings;
                        if (settings == null)
                            return new RailSettings();
                        settings.Normalize();
                        return settings;
                    }
                }
                catch
                {
                    return new RailSettings();
                }
            }
        }

        public static void Save(RailSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            lock (SyncRoot)
            {
                settings.Normalize();
                string directory = Path.GetDirectoryName(SettingsPath);
                Directory.CreateDirectory(directory);
                string temporaryPath = SettingsPath + ".tmp";

                XmlSerializer serializer = new XmlSerializer(typeof(RailSettings));
                using (FileStream stream = File.Create(temporaryPath))
                    serializer.Serialize(stream, settings);

                if (File.Exists(SettingsPath))
                    File.Delete(SettingsPath);
                File.Move(temporaryPath, SettingsPath);
            }
        }
    }
}

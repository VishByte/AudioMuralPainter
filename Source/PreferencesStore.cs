using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace Amp
{
    internal static class PreferencesStore
    {
        const int MaximumXmlSize = 1048576;
        static readonly XmlSerializer SettingsSerializer = new XmlSerializer(typeof(Preferences));
        static readonly XmlSerializer PresetSerializer = CreatePresetSerializer();
        static XmlSerializer CreatePresetSerializer()
        {
            var overrides = new XmlAttributeOverrides();
            foreach (var field in typeof(Preferences).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (field.IsDefined(typeof(LocalPreferenceAttribute), false))
                    overrides.Add(typeof(Preferences), field.Name, new XmlAttributes { XmlIgnore = true });
            return new XmlSerializer(typeof(Preferences), overrides);
        }

        public static Preferences Read(string path)
        {
            // Check and parse one open file so a replacement cannot bypass the size check.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > MaximumXmlSize)
                    throw new InvalidDataException("This preset is too large.");
                var options = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaximumXmlSize
                };
                using (var reader = XmlReader.Create(stream, options))
                    while (reader.Read())
                        if (reader.Depth > 32)
                            throw new InvalidDataException("This preset is nested too deeply.");
                stream.Position = 0;
                using (var reader = XmlReader.Create(stream, options))
                {
                    var p = (Preferences)SettingsSerializer.Deserialize(reader);
                    if (p == null)
                        throw new InvalidDataException("The preset is empty.");
                    p.Validate();
                    return p;
                }
            }
        }

        public static void Write(Preferences preferences, string path, bool appearanceOnly)
        {
            var serializer = appearanceOnly ? PresetSerializer : SettingsSerializer;
            preferences.Validate();
            path = Path.GetFullPath(path);
            string temp = Path.Combine(Path.GetDirectoryName(path), ".amp-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    serializer.Serialize(stream, preferences);
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }
    }
}

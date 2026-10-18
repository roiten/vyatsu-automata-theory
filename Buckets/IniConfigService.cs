using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Buckets
{
    public sealed class BucketConfiguration
    {
        public string[] Colors { get; set; }
        public int[] FillLevels { get; set; }
        public BucketConfiguration() { Colors = new string[10]; FillLevels = new int[10]; }
    }

    public static class IniConfigService
    {
        public static BucketConfiguration Load(string path, IList<string> availableColors)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Файл конфигурации не найден.", path);
            var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string section = "";
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    if (!sections.ContainsKey(section)) sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                int equals = line.IndexOf('=');
                if (equals > 0 && section.Length > 0) sections[section][line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
            }

            var result = new BucketConfiguration();
            for (int i = 0; i < 10; i++)
            {
                string colorFile;
                if (!TryGet(sections, "colors", "bucket" + i, out colorFile)) colorFile = availableColors[i];
                colorFile = Path.GetFileName(colorFile);
                if (!availableColors.Contains(colorFile, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("Неизвестный цвет: " + colorFile);
                result.Colors[i] = colorFile;
                string amount;
                int parsed;
                if (TryGet(sections, "fullness", "bucket" + i, out amount) && int.TryParse(amount, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed >= 0 && parsed <= 10)
                    result.FillLevels[i] = parsed;
                else result.FillLevels[i] = 0;
            }
            if (result.Colors.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 10) throw new InvalidDataException("Цвета вёдер должны быть уникальными.");
            return result;
        }

        public static void Save(string path, IList<Bucket> buckets)
        {
            using (var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(true)))
            {
                writer.WriteLine("[colors]");
                foreach (Bucket bucket in buckets.OrderBy(b => b.Id)) writer.WriteLine("bucket{0} = {1}", bucket.Id, bucket.Color);
                writer.WriteLine(); writer.WriteLine("[fullness]");
                foreach (Bucket bucket in buckets.OrderBy(b => b.Id)) writer.WriteLine("bucket{0} = {1}", bucket.Id, bucket.WaterAmount);
            }
        }

        private static bool TryGet(Dictionary<string, Dictionary<string, string>> sections, string section, string key, out string value)
        {
            value = null;
            return sections.ContainsKey(section) && sections[section].TryGetValue(key, out value);
        }
    }
}

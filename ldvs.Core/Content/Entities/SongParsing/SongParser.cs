using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
// ReSharper disable HeuristicUnreachableCode

namespace ldvs.Core.Content.Entities
{
    public interface ISongParser
    {
        public static Beatmap Parse()
            => throw new NotImplementedException(); // why did you call it like that
    }

    public class BeatmapSet
    {
        public string FolderPath { get; set; } = "";
        public List<Beatmap> maps { get; set; } = new();
        public BMSData Metadata { get; set; } = new();
    }

    public class Beatmap
    {
        public List<HitObject> HitObjects { get; set; } = new();
        public BMPData BMPData { get; set; } = new();
        public General General { get; set; } = new();
        public List<Mod> Mods = [];
        public TempoMap BPMList = new(1);
    }

    public class BMSData
    {
        public string Title { get; set; } = "";
        public string TitleUnicode { get; set; } = "";
        public string Artist { get; set; } = "";
        public string ArtistUnicode { get; set; } = "";
        public string Source { get; set; } = "";
        public List<string> Tags { get; set; } = [];
    }

    public class BMPData
    {
        public string Difficulty { get; set; } = "";
        public string DifficultyName { get; set; } = "";
        public string Creator { get; set; } = "";
        public string CreatorUnicode { get; set; } = "";
        public Color DifficultyColor { get; set; }
        public int OMKeyCount { get; set; } = 0;
    }

    public class General
    {
        public string AudioFilename { get; set; } = ""; // TS SHIT IS RELATIVE AND MAKE SURE IT IS
        public int AudioLeadIn { get; set; }
        public int PreviewPoint { get; set; }
    }

    public class HitObject
    {
        public int Lane { get; set; }        // 0..3 for normal notes, 4..6 for L/M/R bumper
        public double Time { get; set; }     // ms
        public double? EndTime { get; set; } // see above
        public int Type { get; set; }        // 0..2, normal, timed, mine (2 doesnt apply for bumpers i hope)
    }

    public static class SPHelper
    {
        public static string[] SplitCSVLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool insideQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    insideQuotes = !insideQuotes;

                    continue;
                }

                if (c == ',' && !insideQuotes)
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            values.Add(current.ToString().Trim());

            return values.ToArray();
        }

        public static bool TryParseDouble(string value, out double result)
        {
            string trimmed = value.Trim();
            bool success = double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture,out result);
            return success;
        }

        public static bool TryParseInt(string value, out int result)
        {
            string trimmed = value.Trim();
            bool success = int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
            return success;
        }

        public static bool TryParseKeyValue(string line, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;

            var separator = line.IndexOf(':');

            if (separator < 0)
                return false;

            key = line[..separator].Trim();
            value = line[(separator + 1)..].Trim();

            return key.Length > 0;
        }
    }

    public class SongParser
    {
        public List<BeatmapSet> ParseSongsFolder()
        {
            var beatmapSets = new List<BeatmapSet>();

            string projectDirectory =
                Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.FullName; // holy shit dude

            foreach (var folder in Directory.GetDirectories(Path.Combine(projectDirectory, "Songs")))
            {
                var bms = new BeatmapSet();
                bms.FolderPath = folder;

                foreach (var filePath in Directory.EnumerateFiles(folder, "*.vs"))
                {
                    string content = File.ReadAllText(filePath);
                    Beatmap bmp = _vs.Parse(content, bms);
                    bms.maps.Add(bmp);
                }

                foreach (var filePath in Directory.EnumerateFiles(folder, "*.osu"))
                {
                    string content = File.ReadAllText(filePath);
                    Beatmap bmp = _osu.Parse(content, bms);
                    bms.maps.Add(bmp);
                }

                foreach (var filePath in Directory.EnumerateFiles(folder, "*.vsb"))
                {
                    Beatmap bmp = _vsb.Parse(filePath);
                    // i think you should auto generate this in like a .vsbm file and then let the player customize it :distracteline:
                    bms.Metadata.Title = Path.GetFileNameWithoutExtension(folder);
                    bms.Metadata.TitleUnicode = Path.GetFileNameWithoutExtension(folder);
                    bms.Metadata.Artist = Path.GetFileNameWithoutExtension(folder);
                    bms.Metadata.ArtistUnicode = Path.GetFileNameWithoutExtension(folder);
                    bmp.BMPData.CreatorUnicode = "VIVID/STASIS";
                    bmp.BMPData.Creator = "VIVID/STASIS";
                    bmp.BMPData.CreatorUnicode = "VIVID/STASIS";
                    bmp.BMPData.Difficulty = "00";
                    bmp.BMPData.DifficultyName = Path.GetFileNameWithoutExtension(filePath);
                    bmp.General.AudioLeadIn = 2000; // replace later imo
                    string? file = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories).FirstOrDefault(f => new[] { ".ogg", ".mp3", ".wav", ".flac", ".m4a", ".aac", ".wma" }.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
                    if (file != null)
                    {
                        bmp.General.AudioFilename = Path.GetRelativePath(folder, file);
                    }
                    else {
                        Console.WriteLine(@$"{Path.Combine(folder, Path.GetFileNameWithoutExtension(filePath))} either doesnt exist or doesnt have a valid type");
                        continue;
                    }
                    bms.maps.Add(bmp);
                }


                var sorted = bms.maps.OrderBy(n => n.BMPData.Difficulty).ToList();

                if (sorted.Count < 1) { continue; } // map's b lank idiot
                bms.maps = new List<Beatmap>(sorted);
                beatmapSets.Add(bms);
                Logger.Log($"Parsed {bms.FolderPath}");
            }
            Logger.Log("Done parsing maps!");
            return beatmapSets;
        }
    }
}
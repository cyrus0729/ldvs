using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace ldvs.Core.Content.Entities
{
    public class BeatmapSet
    {
        public string FolderPath { get; set; } = "";
        public LinkedList<Beatmap> maps { get; set; } = new();
        public BMSData Metadata { get; set; } = new();
    }

    public class Beatmap
    {
        public Dictionary<string, object> Sections { get; set; } = new();

        public List<TimingPoint> TimingPoints { get; set; } = new();
        public List<HitObject> HitObjects { get; set; } = new();

        // optional convenience
        public BMPData BMPMeta { get; set; } = new();
        public General General { get; set; } = new();
    }

    public class General
    {
        public string AudioFilename { get; set; } = "";
        public int AudioLeadIn { get; set; }
        public int PreviewPoint { get; set; }
    }

    public class BMSData
    {
        public string Title { get; set; }
        public string TitleUnicode { get; set; }
        public string Artist { get; set; }
        public string ArtistUnicode { get; set; }
        public string Source { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    public class BMPData
    {
        public string Difficulty { get; set; }
        public string DifficultyName { get; set; }
        public string Creator { get; set; }
        public string CreatorUnicode { get; set; }
        public int TickRate { get; set; }
        public int OMKeyCount { get; set; }
    }

    public class TimingPoint
    {
            public double offset { get; init; }
            public double beatLen { get; init; }
            public int meter { get; init; }
            public bool Uninherited { get; init; }

            public double Bpm
                => Uninherited && beatLen > 0
                       ? 60000.0 / beatLen
                       : 0;
            public double svMult { get; init; } = 1.0;
    }

    public class HitObject
    {
        public int Column { get; set; } // 0..3 for normal notes, 4..5 for L/M/R bumper
        public int Time { get; set; }
        public int? EndTime { get; set; } // long notes end time?
        public int Type { get; set; }     // 0..2, normal, timed, mine (2 doesnt apply for bumpers i hope)
    }

    public static class VSBUtils
    {
        private static byte[] ToUnsignedBigEndian(BigInteger value)
        {
            return value.ToByteArray(
                isUnsigned: true,
                isBigEndian: true);
        }

        public static RSAParameters RsaParameters = new RSAParameters
        {
            Exponent = ToUnsignedBigEndian(
                BigInteger.Parse(
                    "4087966337962722934016728373929954736911054268340290529718840403152381802449888346105429852424107746132591382090681618505600090563880520343659326344144081754767820449572095395735609339190867582498239351112700454340174266240306915532865730737700937023273316512201852193092848735659991098335550058987389902744138993370187109674688602220616550429190371617718469944750231838931021340649708625720099359930363371306184708512452382562266870574235957050890058978540898926879156480030954345055309039385471595968562995762819631280551603822874246284493346689033984936977482360320788049616621145392393928330228382874172082440289259049272547327070384017027740526865779790246622942927212687995907573512992024572752906193469726690570592323041441474712576330726229741449831409379198722634050167222736564950170077459202587882952456318580615189706952925518026801158664284712656280067439912216671693199268965961613231362525782754244074362083059")),
            Modulus = [0x11],
            D = [0x0],
        };

        public static (int size, int padding) BinaryPad(MemoryStream ms)
        {
            int size = (int)ms.Length;
            int modu = size % 4;
            int padding = 0;

            if (modu > 0)
            {
                padding = 4 - modu;

                ms.Position = ms.Length;
                for (int i = 0; i < padding; i++)
                    ms.WriteByte(0);
            }

            return (size, padding);
        }

        public static void binary_append_signature(MemoryStream arg0, (int size, int padding) arg1, String arg2)
        {
            if (arg1.padding > 0)
            {
                arg0.Seek(arg1.padding * -1, SeekOrigin.Current);
            }

            for (var i = 0; i < 384; i++)
            {
                var code = arg2[i * 3 + 1] + arg2[i * 3 + 2];
                float.TryParse("0x" + code, CultureInfo.CurrentCulture, out float num);
                var a = new BinaryWriter(arg0);
                a.Write(num);
            }

        }

        public static void rsa_sign(MemoryStream arg0) // the fucking game doesnt even use this lmao
        {
            var position = arg0.Position;
            var size = BinaryPad(arg0);

            using (RSA rsa = RSA.Create())
            {
                RSAPKCS1SignatureFormatter rsaFormatter = new(rsa);
                binary_append_signature(arg0,size, Convert.ToString(Encoding.UTF8.GetString(rsaFormatter.CreateSignature(arg0.ToArray()))));
            }

        }

        public static bool rsa_verify(MemoryStream arg0)
        {
            var size = BinaryPad(arg0);

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(RsaParameters);

                RSAPKCS1SignatureDeformatter rsaDeformatter = new(rsa);
                RSAPKCS1SignatureFormatter rsaFormatter = new(rsa);
                if (rsaDeformatter.VerifySignature(arg0.ToArray(), rsaFormatter.CreateSignature(arg0.ToArray())))
                {
                    Console.WriteLine("The signature is valid.");
                    return true;
                }
                else
                {
                    Console.WriteLine("The signature is not valid.");
                    return false;
                }
            }
        }

    }

    public class SongParser
    {
        private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

        public LinkedList<BeatmapSet> ParseSongsFolder()
        {
            var beatmapSets = new LinkedList<BeatmapSet>();

            string projectDirectory =
                Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.FullName;

            foreach (var folder in Directory.GetDirectories(Path.Combine(projectDirectory, "Songs")))
            {
                var bms = new BeatmapSet();
                bms.FolderPath = folder;

                foreach (var filePath in Directory.EnumerateFiles(folder, "*.vs"))
                {
                    string content = File.ReadAllText(filePath);
                    Beatmap bmp = ParseVS(content, bms);
                    bms.maps.AddLast(bmp);
                }

                foreach (var filePath in Directory.EnumerateFiles(folder, "*.osu"))
                {
                    string content = File.ReadAllText(filePath);
                    Beatmap bmp = ParseOsu(content, bms);
                    bms.maps.AddLast(bmp);
                }

                foreach (var filePath in Directory.EnumerateFiles(folder, "*.vsb"))
                {
                    // TODO: parse VS binary
                }

                var sorted = bms.maps
                                .OrderBy(n => n.BMPMeta.Difficulty)
                                .ToList();

                if (sorted.Count < 1) { continue; } // map's b lank idiot

                bms.maps = new LinkedList<Beatmap>(sorted);
                beatmapSets.AddLast(bms);
            }

            return beatmapSets;
        }

        public Beatmap ParseOsu(string text, BeatmapSet bms)
        {
            var map = new Beatmap();
            string? currentSection = null;

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (line.Length == 0)
                    continue;
                if (line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    if (!map.Sections.ContainsKey(currentSection))
                        map.Sections[currentSection] = new List<string>();

                    continue;
                }

                if (currentSection == null)
                    continue;

                switch (currentSection)
                {
                    case "General":
                        ParseGeneralKeyValueLine(line, map.General); break;
                    case "Metadata":
                        ParseOsuManiaMetadata(line, map.BMPMeta, bms.Metadata); break;
                    case "TimingPoints":
                        ParseTimingPoint(line, map); break;
                    case "HitObjects":
                        ParseOsuManiaNote(line, map); break;
                    case "Editor":
                        break;
                    case "Events":
                        // ignore for now
                        break;
                    case "Difficulty":
                        ParseOsuManiaDifficulty(line,map.BMPMeta); break;
                }
            }

            return map;
        }

        public Beatmap ParseVS(string text, BeatmapSet bms)
        {
            var map = new Beatmap();

            string? currentSection = null;

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (line.Length == 0)
                    continue;
                if (line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    currentSection = line.Substring(1, line.Length - 2);

                    if (!map.Sections.ContainsKey(currentSection))
                        map.Sections[currentSection] = new List<string>();

                    continue;
                }

                if (currentSection == null)
                    continue;

                switch (currentSection)
                {
                    case "General":
                        ParseGeneralKeyValueLine(line, map.General); break;
                    case "Metadata":
                        ParseVSMetadata(line, map.BMPMeta, bms.Metadata); break;
                    case "Difficulty":
                        ParseVSDifficulty(line,map.BMPMeta); break;
                    case "TimingPoints":
                        ParseTimingPoint(line, map); break;
                    case "HitObjects":
                        ParseVSNote(line, map); break;
                    case "Events":
                        // parse later
                        break;
                }
            }

            return map;
        }

        private static Beatmap ParseVSBBinary(string file, bool rsa = true, bool arg2 = false) // no clue what arg2 does yet
        {
            if (File.Exists(file))
            {
                using (FileStream fileStream = File.OpenRead(file))
                {
                    MemoryStream memoryStream = new MemoryStream();
                    fileStream.CopyTo(memoryStream);

                    var b1 = memoryStream.ReadByte();
                    var b2 = memoryStream.ReadByte();
                    var b3 = memoryStream.ReadByte();
                    var b4 = memoryStream.ReadByte();
                    var b5 = memoryStream.ReadByte();

                    try
                    {
                        if (b1 == 86 && b2 == 83 && b3 == 67 && b5 == 0)
                        {
                            switch (b4)
                            {
                                case 1:
                                    // chart reader time!! yayyy
                                    break;
                            }
                        }
                    }
                    finally
                    {
                        memoryStream.Dispose();
                    }

                }
            }
            return new Beatmap();
        }

        private static void ParseGeneralKeyValueLine(string line, General general)
        {
            int idx = line.IndexOf(':');

            if (idx < 0)
                return;

            var key = line.Substring(0, idx).Trim();
            var value = line.Substring(idx + 1).Trim();

            switch (key)
            {
                case "AudioFilename":
                    general.AudioFilename = value; break;
                case "AudioLeadIn":
                    if (int.TryParse(value, NumberStyles.Integer, _invariant, out var leadIn))
                        general.AudioLeadIn = leadIn; break;
                case "PreviewTime":
                    if (int.TryParse(value, NumberStyles.Integer, _invariant, out var preview))
                        general.PreviewPoint = preview; break;
            }
        }

        private static void ParseOsuManiaDifficulty(string line, BMPData md)
        {
            int idx = line.IndexOf(':');

            if (idx < 0)
                return;

            var key = line.Substring(0, idx).Trim();
            var value = line.Substring(idx + 1).Trim();

            switch (key)
            {
            case "CircleSize":
                md.OMKeyCount = Int32.Parse(value);
                break;
            case "SliderTickRate":
                md.TickRate = Int32.Parse(value);
                break;
            }

        }

        private static void ParseVSDifficulty(string line, BMPData md)
        {
            int idx = line.IndexOf(':');

            if (idx < 0)
                return;

            var key = line.Substring(0, idx).Trim();
            var value = line.Substring(idx + 1).Trim();

            switch (key)
            {
            case "Difficulty":
                md.Difficulty = value;

                break;
            case "DifficultyName":
                md.DifficultyName = value;

                break;
            case "SliderTickRate":
                md.TickRate = Int32.Parse(value);

                break;
            }
        }

        private static void ParseOsuManiaMetadata(string line, BMPData md, BMSData bms)
        {
            int idx = line.IndexOf(':');

            if (idx < 0)
                return;

            var key = line.Substring(0, idx).Trim();
            var value = line.Substring(idx + 1).Trim();

            switch (key)
            {
                case "Title":
                    bms.Title = value;
                    break;
                case "TitleUnicode":
                    bms.TitleUnicode = value;
                    break;
                case "Artist":
                    bms.Artist = value;
                    break;
                case "ArtistUnicode":
                    bms.ArtistUnicode = value;
                    break;
                case "Creator":
                    md.Creator = value;
                    break;
                case "CreatorUnicode":
                    md.CreatorUnicode = value;
                    break;
                case "Version":
                    var v = value.Split(" ");
                    if (int.TryParse(v[0], out int v2))
                    {
                        md.Difficulty = v2.ToString();
                        md.DifficultyName = String.Join(" ", v[1..]);
                    }
                    else
                    {
                        md.Difficulty = "0";
                        md.DifficultyName = value;
                    }
                    break;
                case "Source":
                    bms.Source = value;
                    break;
                case "Tags":
                    bms.Tags = value.Split([' '], StringSplitOptions.RemoveEmptyEntries).ToList();
                    break;
            }
        }

        private static void ParseVSMetadata(string line, BMPData md, BMSData bms)
        {
            int idx = line.IndexOf(':');

            if (idx < 0)
                return;

            var key = line.Substring(0, idx).Trim();
            var value = line.Substring(idx + 1).Trim();

            switch (key)
            {
                case "Title":
                   bms.Title = value; break;
                case "TitleUnicode":
                    bms.TitleUnicode = value; break;
                case "Artist":
                    bms.Artist = value; break;
                case "ArtistUnicode":
                    bms.ArtistUnicode = value; break;
                case "Creator":
                    md.Creator = value; break;
                case "CreatorUnicode":
                    md.CreatorUnicode = value; break;
                case "Version":
                    md.DifficultyName = value; break;
                case "Source":
                    bms.Source = value; break;
                case "Tags":
                    bms.Tags = value.Split([' '], StringSplitOptions.RemoveEmptyEntries).ToList(); break;
            }
        }

        private static void ParseTimingPoint(string line, Beatmap map)
        {
            string[] parts = line.Split(',');
            bool uninherited = parts[6] == "1";
            double beatLength = double.Parse(
                parts[1],
                CultureInfo.InvariantCulture);

            double svMultiplier = uninherited
                                      ? 1.0
                                      : 100.0 / Math.Abs(beatLength);

            map.TimingPoints.Add(new TimingPoint
            {
                offset = double.Parse(
                    parts[0],
                    CultureInfo.InvariantCulture),
                beatLen = double.Parse(
                    parts[1],
                    CultureInfo.InvariantCulture),
                meter = int.Parse(parts[2]),
                Uninherited = parts[6] == "1",
                svMult = svMultiplier,
            });

        }

        private static void ParseVSNote(string line, Beatmap map)
        {
            // .vs sample: 1,2005,-1,1
            var parts = line.Split(',');

            if (parts.Length < 4)
                return;

            int column = ParseInt(parts[0]); // .vs handles columns
            int time = ParseInt(parts[1]);

            int? endTime = parts[2] != "" ? ParseInt(parts[2]) : null;

            int type = ParseInt(parts[3]);
            map.HitObjects.Add(
                new HitObject
                {
                    Column = column,
                    Time = time,
                    EndTime = endTime,
                    Type = type
                });
        }

        private static void ParseOsuManiaNote(string line, Beatmap map)
        { // sample: 109,192,2241,128,0,2359:0:0:0:0:
            var parts = line.Split(',');

            if (parts.Length < 6)
                return;

            double x = ParseDouble(parts[0]);
            int time = ParseInt(parts[2]);

            string fiveone = parts[5].Split(":")[0];
            int? LNEnd = null;
            int type = 0;

            switch (fiveone)
            {
                case "0":
                    type = 0;
                    break;
                case "1":
                    type = 1;
                    break;
                case "2":
                    type = 2;
                    break;
                case "3":
                    type = 2;
                    break;
                default:
                    LNEnd = ParseInt(fiveone);
                    break;
            }

            var obj = new HitObject
            {
                Column = ParseOM_XToLane(x,map.BMPMeta.OMKeyCount),
                Time = time,
                EndTime = LNEnd,
                Type = type
            };

            map.HitObjects.Add(obj);
        }

        //todo: figure out how this actually fucking WORKS
        private static int ParseOM_XToLane(double x, int lanes)
        {
            var laneTable = lanes switch
            {
                7 => new Dictionary<double, int>
                {
                    [36] = 0,
                    [109] = 1,
                    [182] = 2,
                    [256] = 3,
                    [329] = 4,
                    [402] = 5,
                    [475] = 6
                },
                4 => new Dictionary<double, int>
                {
                    [64] = 0,
                    [192] = 1,
                    [320] = 2,
                    [448] = 3,
                },
                _ => throw new ArgumentOutOfRangeException(nameof(lanes), lanes, null)
            };

            return laneTable[x];
        }

        private static int ParseInt(string s)
        {
            if (int.TryParse(s.Trim(), NumberStyles.Integer, _invariant, out var v))
                return v;

            return 0;
        }

        private static double ParseDouble(string s)
        {
            if (double.TryParse(s.Trim(), NumberStyles.Float, _invariant, out var v))
                return v;

            return 0.0;
        }

    }
}
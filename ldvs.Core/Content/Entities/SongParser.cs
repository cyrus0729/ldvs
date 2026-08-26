using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

        public List<TimingPoint> UninheritedTimingPoints { get; set; } = new();
        public List<TimingPoint> InheritedTimingPoints { get; set; } = new();

        public List<HitObject> HitObjects { get; set; } = new();

        public List<object> Mods { get; set; } = new();

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
        public Color DifficultyColor { get; set; }
        public int OMKeyCount { get; set; }
    }


    public class BackgroundEvent
    {
        public int StartTime { get; set; }
        public string FileName { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Extra { get; set; }
    }

    public class VideoEvent
    {
        public int StartTime { get; set; }
        public string FileName { get; set; } = "";
    }

    public class SampleEvent
    {
        public int Time { get; set; }
        public int Layer { get; set; }
        public string FileName { get; set; } = "";
        public int Volume { get; set; } = 1;
    }

    public class TimingPoint
    {
        public double offset;
        public double MsPerBeat;
        public bool Uninherited;
        public int meter;

        public double SvMultiplier { get; set; } = 1.0;

        public double Bpm // tbh this is just a helper
            => Uninherited && MsPerBeat > 0
                   ? 60000.0 / MsPerBeat
                       : 0;
    }


    public class HitObject
    {
        public int Lane { get; set; } // 0..3 for normal notes, 4..5 for L/M/R bumper
        public int Time { get; set; }
        public int? EndTime { get; set; }
        public int Type { get; set; }     // 0..2, normal, timed, mine (2 doesnt apply for bumpers i hope)
    }

    public class VSBHitObject: HitObject
    {
        public int Lane { get; set; }
        public int Time { get; set; }
        public int? EndTime { get; set; }
        public int Type { get; set; }
        public List<object> extra = [0];
    }

    public class VSBeatmap : Beatmap
    {
        public List<object> mods;
    }
    public static class VSBUtils
    {
        public static RSAParameters RsaParameters = new RSAParameters
        {
            Exponent = ToUnsignedBigEndian(
                BigInteger.Parse(
                    "4087966337962722934016728373929954736911054268340290529718840403152381802449888346105429852424107746132591382090681618505600090563880520343659326344144081754767820449572095395735609339190867582498239351112700454340174266240306915532865730737700937023273316512201852193092848735659991098335550058987389902744138993370187109674688602220616550429190371617718469944750231838931021340649708625720099359930363371306184708512452382562266870574235957050890058978540898926879156480030954345055309039385471595968562995762819631280551603822874246284493346689033984936977482360320788049616621145392393928330228382874172082440289259049272547327070384017027740526865779790246622942927212687995907573512992024572752906193469726690570592323041441474712576330726229741449831409379198722634050167222736564950170077459202587882952456318580615189706952925518026801158664284712656280067439912216671693199268965961613231362525782754244074362083059")),
            Modulus = [0x11],
            D = [0x0],
        };

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

        private static byte[] ToUnsignedBigEndian(BigInteger value)
        {
            return value.ToByteArray(
                isUnsigned: true,
                isBigEndian: true);
        }
    }

    public static class StoryboardParser
            {
                public static List<object> ReadEvents(string filePath)
                {
                    var events = new List<object>();
                    bool insideEvents = false;

                    foreach (string rawLine in File.ReadLines(filePath))
                    {
                        string line = rawLine.Trim();

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (line.Equals("[Events]", StringComparison.OrdinalIgnoreCase))
                        {
                            insideEvents = true;
                            continue;
                        }

                        if (!insideEvents)
                            continue;

                        // Ignore comments and section headers
                        if (line.StartsWith("//") || line.StartsWith("["))
                            continue;

                        string[] parts = SplitCsvLine(line);

                        if (parts.Length == 0)
                            continue;

                        switch (parts[0].ToLowerInvariant())
                        {
                            case "0":
                            case "background":
                                if (parts.Length >= 6)
                                {
                                    events.Add(new BackgroundEvent
                                    {
                                        StartTime = ParseInt(parts[1]),
                                        FileName = parts[2],
                                        X = ParseInt(parts[3]),
                                        Y = ParseInt(parts[4]),
                                        Extra = ParseInt(parts[5])
                                    });
                                }
                                break;

                            case "1":
                            case "video":
                                if (parts.Length >= 3)
                                {
                                    events.Add(new VideoEvent
                                    {
                                        StartTime = ParseInt(parts[1]),
                                        FileName = parts[2]
                                    });
                                }
                                break;

                            case "3":
                            case "sample":
                                if (parts.Length >= 4)
                                {
                                    events.Add(new SampleEvent
                                    {
                                        Time = ParseInt(parts[1]),
                                        Layer = ParseInt(parts[2]),
                                        FileName = parts[3]
                                    });
                                }
                                break;
                        }
                    }

                    return events;
                }

                private static int ParseInt(string value)
                {
                    return int.Parse(value, CultureInfo.InvariantCulture);
                }

                private static string[] SplitCsvLine(string line)
                {
                    var result = new List<string>();
                    bool insideQuotes = false;
                    string current = "";

                    foreach (char character in line)
                    {
                        if (character == '"')
                        {
                            insideQuotes = !insideQuotes;
                            continue;
                        }

                        if (character == ',' && !insideQuotes)
                        {
                            result.Add(current.Trim());
                            current = "";
                        }
                        else
                        {
                            current += character;
                        }
                    }

                    result.Add(current.Trim());
                    return result.ToArray();
                }
            }

    public static class SPHelper
    {
        public static bool ParseDouble(string s, out double k)
        {
            if (double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                k = v;
                return true;
            }
            k= double.NegativeInfinity;
            return false;
        }

        public static bool ParseInt(string s, out int k)
        {
            if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            {
                k = v;
                return true;
            }
            k = int.MinValue;
            return false;
        }

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
    }

    public class SongParser
    {
        public class VSB_Chart_reader(BinaryReader buffer, bool arg2 = false)
        {
            public List<VSBHitObject> notes;

            // fuck gameamker fw
            const int BufferU8 = 1;
            const int BufferS8 = 2;
            const int BufferU16 = 3;
            const int BufferS16 = 4;
            const int BufferU32 = 5;
            const int BufferS32 = 6;
            const int BufferF16 = 7;
            const int BufferF32 = 8;
            const int BufferF64 = 9;
            const int BufferString = 10;
            const int BufferText = 11;
            const int BufferBool = 12;

            public VSBeatmap run()
            {
                VSBeatmap v = new();

                return v;

                while (true)
                {
                    int flag = buffer.ReadByte();

                    if (verifyFlag(flag, 160) == false)
                    {
                        Console.WriteLine(@"hey the map fucking failed to be read");
                        throw new ArgumentNullException();
                    }

                    if (flag == 192) // what does this mean????
                    {
                        while (true)
                        {
                            int flag2 = buffer.ReadByte();

                            if (flag2 == 160)
                                v.HitObjects.Add(readNote());
                            else if (flag2 == 193)
                                break;
                        }
                    }

                    // i dont give a flying fuck about mods rn :3c
                    /*if (flag == 224 && !bypass_mods)
                    {
                        var data =
                        {
                            proxies: 1,
                            obj: "obj_base_gimmick"
                        };
                        var modlist = [];
                        var perframelist = [];
                        var obj = undefined;

                        while (true)
                        {
                            var flag2 = (int)ReadGameMakerValue(buffer, BufferU8);

                            switch (flag2)
                            {
                                case 228:
                                    data.proxies = (int)ReadGameMakerValue(buffer, BufferU8);
                                    break;

                                case 229:
                                    data.obj = (int)ReadGameMakerValue(buffer, BufferBool);
                                    break;
                            }

                            if (flag2 == 226)
                            {
                                obj = getModGimmickObj(data.obj);

                                while (true)
                                {
                                    var flag3 = read(1);

                                    if (flag3 == 233)
                                    {
                                        var m = {};
                                        m.b = read(8);
                                        m.d = read(8);
                                        m.e = read(1);
                                        m.v1 = read(8);
                                        m.v2 = read(8);
                                        m.mi = read(1);
                                        m.p = read(2);
                                        m.e = getEaseFromByte(m.e);
                                        m.m = getModNameFromByte(m.mi, data.obj);
                                        m.w = variable_struct_get(global.mod_weight, m.m);
                                        array_push(modlist, m);
                                    }
                                    else if (flag3 == 236)
                                    {
                                        var m = {};
                                        m.b = read(8);
                                        m.e = read(8);
                                        m.f = read(11);
                                        m.f = variable_struct_get(obj.obj.funcs, m.f);
                                        array_push(perframelist, m);
                                    }

                                    if (flag3 == 227)
                                        break;
                                }

                                mods =
                                {
                                    mods: modlist,
                                    perFrame: perframelist,
                                    data: data
                                };

                                if (obj.isNew)
                                    instance_destroy(obj.obj);

                            }

                            if (flag2 == 225)
                                break;
                        }
                    }

                    if (flag == 255)
                        break;
                } */

                    return v;
                }
            }

            private static int GetBufferType(int arg0)
            {
                return arg0 switch
                {
                    1676 => BufferU8, 177 => BufferS8, 178 => BufferU32, 179 => BufferS32, 181 => BufferF16, 182 => BufferF32, 183 => BufferString, 184 => BufferText, 1 or 2 or 4 or 5 => BufferF32, 3 or 6 => BufferU8, 7 => BufferS8, _ => throw new ArgumentOutOfRangeException(
                        nameof(arg0),
                        arg0,
                        @"unknown value")
                };
            }

            private static object ReadGameMakerValue(BinaryReader reader, int bufferType)
            {
                return bufferType switch
                {
                    BufferU8 => reader.ReadByte(), BufferS8 => reader.ReadSByte(), BufferU16 => reader.ReadUInt16(), BufferS16 => reader.ReadInt16(), BufferU32 => reader.ReadUInt32(), BufferS32 => reader.ReadInt32(), BufferF16 => reader.ReadHalf(), BufferF32 => reader.ReadSingle(), BufferF64 => reader.ReadDouble(), BufferString => reader.ReadString(), BufferText => ReadNullTerminatedString(reader), BufferBool => reader.ReadBoolean(), _ => throw new NotSupportedException($"Unsupported buffer type: {bufferType}")
                };
            }

            private VSBHitObject readNote()
            {
                var n = new VSBHitObject();
                while (true)
                {
                    var flag = (int)ReadGameMakerValue(buffer,BufferU8);
                    verifyFlag(flag, 160);

                    switch (flag)
                    {
                        case 162: n.Type = (int)ReadGameMakerValue(buffer, BufferU8); break;
                        case 163: n.Lane = (int)ReadGameMakerValue(buffer, BufferU8); break;
                        case 164: n.Time = (int)ReadGameMakerValue(buffer, BufferU16); break;
                        case 166:
                            var extra = new List<object>();
                            while (true)
                            {
                                var type = buffer.ReadByte();

                                if (type == 167)
                                    break;

                                verifyFlag(type, 176);
                                //var _id = buffer.ReadByte();
                                var value = ReadGameMakerValue(buffer,GetBufferType(type));
                                extra[0] = value;
                            }
                            n.extra = extra;
                            break;
                    }

                    if (flag == 161)
                        break;
                }

                return n;
            }

            static string ReadNullTerminatedString(BinaryReader reader)
            {
                using var bytes = new MemoryStream();

                while (true)
                {
                    byte value = reader.ReadByte();

                    if (value == 0)
                        break;

                    bytes.WriteByte(value);
                }

                return Encoding.UTF8.GetString(bytes.ToArray());
            }

            private bool verifyFlag(int arg0, int arg1)
            {
                return ((arg0 & arg1) == arg1);
            }
        }

        public Beatmap ParseOsu(string text, BeatmapSet bms) {
            var map = new Beatmap();
            string? currentSection = null;

            var lines = text.Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.None
            );

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
                        ParseGeneralKeyValueLine(line, map.General);
                        break;

                    case "Metadata":
                        ParseOsuManiaMetadata(line, map.BMPMeta, bms.Metadata);
                        break;

                    case "TimingPoints":
                        ParseTimingPoint(line, map);
                        break;

                    case "HitObjects":
                        ParseOsuManiaNote(line, map);
                        break;

                    case "Editor":
                        break;

                    case "Events":
                        ParseEvent(line, map.Mods);
                        break;

                    case "Difficulty":
                        ParseOsuManiaDifficulty(line, map.BMPMeta);
                        break;
                }
            }

            map.UninheritedTimingPoints = map.UninheritedTimingPoints
                .OrderBy(point => point.offset)
                .ToList();

            if (map.UninheritedTimingPoints.Count > 0)
            {
                map.UninheritedTimingPoints.Add(
                    new TimingPoint
                    {
                        offset = 0,
                        MsPerBeat = map.UninheritedTimingPoints[0].MsPerBeat,
                        Uninherited = false,
                        meter = map.UninheritedTimingPoints[0].meter,
                        SvMultiplier = map.UninheritedTimingPoints[0].SvMultiplier
                    }
                );
            }

            map.InheritedTimingPoints = map.InheritedTimingPoints
                .OrderBy(point => point.offset)
                .ToList();

            return map;
        }

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
                    ParseVSBBinary(filePath);
                    continue;
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

        private void ParseEvent(string line, List<object> events) // yayyy i love string parsing :annoyedline:
        {
            string[] parts = SPHelper.SplitCSVLine(line);

            if (parts.Length == 0)
                return;

            // Background
            // 0,0,"bg.jpg",0,0
            if (parts[0] == "0" && parts.Length >= 5)
            {
                if (!SPHelper.ParseInt(parts[1], out int startTime))
                    return;

                if (!SPHelper.ParseInt(parts[3], out int x))
                    x = 0;

                if (!SPHelper.ParseInt(parts[4], out int y))
                    y = 0;

                events.Add(
                    new BackgroundEvent
                    {
                        StartTime = startTime,
                        FileName = parts[2],
                        X = x,
                        Y = y
                    });

                return;
            }

            // Video
            // 1,1000,"video.mp4"
            if (parts[0] == "1" && parts.Length >= 3)
            {
                if (!SPHelper.ParseInt(parts[1], out int startTime))
                    return;

                events.Add(
                    new VideoEvent
                    {
                        StartTime = startTime,
                        FileName = parts[2]
                    });

                return;
            }

            // Sound sample
            // 3,1500,0,"hit.wav",80
            if (parts[0] == "3" && parts.Length >= 4)
            {
                if (!SPHelper.ParseInt(parts[1], out int time))
                    return;

                if (!SPHelper.ParseInt(parts[2], out int layer))
                    layer = 0;

                int volume = 100;

                if (parts.Length >= 5)
                    SPHelper.ParseInt(parts[4], out volume);

                events.Add(
                    new SampleEvent
                    {
                        Time = time,
                        Layer = layer,
                        FileName = parts[3],
                        Volume = volume
                    });
            }
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
                    if (SPHelper.ParseInt(value, out int leadIn))
                        general.AudioLeadIn = leadIn; break;
                case "PreviewTime":
                    if (SPHelper.ParseInt(value, out int preview))
                        general.PreviewPoint = preview; break;
            }
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
                    if (SPHelper.ParseInt(v[0], out int v2))
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

        private static void ParseOsuManiaNote(string line, Beatmap map)
        {
            var parts = SPHelper.SplitCSVLine(line);

            if (parts.Length < 6)
                return;

            SPHelper.ParseDouble(parts[0], out var x);
            SPHelper.ParseInt(parts[2], out var time);

            string fiveone = parts[5].Split(":")[0];
            int? LNEnd = null;
            int type = 0;

            SPHelper.ParseInt(fiveone, out var k);

            switch (fiveone)
            {
                case "0": // auto
                case "1": // normal
                case "2": // soft
                case "3": // drum
                    type = k;
                    break;
                default:
                    LNEnd = k;
                    break;
            }

            var obj = new HitObject
            {
                Lane = ParseOM_XToLane(x,map.BMPMeta.OMKeyCount),
                Time = time,
                EndTime = LNEnd,
                Type = type
            };

            map.HitObjects.Add(obj);
        }

        private static void ParseTimingPoint(string line, Beatmap map)
        {
            string[] parts = SPHelper.SplitCSVLine(line);

            SPHelper.ParseDouble(parts[0].Trim(), out var offset);
            SPHelper.ParseDouble(parts[1].Trim(), out var rawBeatLength);
            SPHelper.ParseInt(parts[2].Trim(), out var meter);
            bool uninherited = parts[6].Trim() == "1";

            var point = new TimingPoint
            {
                offset = offset,
                MsPerBeat = uninherited
                    ? rawBeatLength
                    : 0.0,
                Uninherited = uninherited,
                SvMultiplier = uninherited
                    ? 1.0
                    : 100.0 / Math.Abs(rawBeatLength),
                meter = meter
            };

            if (uninherited)
                map.UninheritedTimingPoints.Add(point);
            else
                map.InheritedTimingPoints.Add(point);
        }

        private static Beatmap ParseVSBBinary(string file, bool rsa = true, bool arg2 = false)
        {
            if (!File.Exists(file))
                return new Beatmap();

            using FileStream fileStream = File.OpenRead(file);
            using BinaryReader buf = new BinaryReader(fileStream);

            Beatmap mp = new Beatmap();

            int b1 = buf.ReadByte();
            int b2 = buf.ReadByte();
            int b3 = buf.ReadByte();
            int b4 = buf.ReadByte();
            int b5 = buf.ReadByte();

            if (b1 == 86 && // V
                b2 == 83 && // S
                b3 == 67 && // C
                b5 == 0)
            {
                switch (b4)
                {
                    case 1: {
                        VSB_Chart_reader reader = new VSB_Chart_reader(buf, arg2);

                        return reader.run();
                    }

                    default: return new Beatmap();
                }
            }

            return new Beatmap();
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
                case "DifficultyColor":
                    var prop = typeof(Color).GetProperty(value.Substring(0, 6) == "Color." ? value : "Color." + value);
                    if (prop != null)
                        md.DifficultyColor = (Color)(prop.GetValue(null, null) ?? Color.White);
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

        private static void ParseVSNote(string line, Beatmap map)
        {
            var parts = SPHelper.SplitCSVLine(line);

            if (parts.Length < 4)
                return;

            SPHelper.ParseInt(parts[0], out var column);
            SPHelper.ParseInt(parts[1], out var time);
            SPHelper.ParseInt(parts[2], out var endTime);
            SPHelper.ParseInt(parts[3], out var type);
            map.HitObjects.Add(
                new HitObject
                {
                    Lane = column,
                    Time = time,
                    EndTime = endTime,
                    Type = type
                });
        }
    }
}
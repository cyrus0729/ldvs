using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace ldvs.Core.Content.Entities;

public class _vsb : ISongParser
{
    public static class VSBUtils
    {
        public static RSAParameters RsaParameters = new()
        {
            Exponent = ToUnsignedBigEndian(
                BigInteger.Parse(
                    "40879663379627229340167283739299547369110542683402905297188404031523818024498883461054298524241077461325913820906816185056000905638805203436593263441440817547678204495720953957356093391908675824982393511127004543401742662403069155328657307377009370232733165122018521930928487356599910983355500589873899027441389933701871096746886022206165504291903716177184699447502318389310213406497086257200993599303633713061847085124523825622668705742359570508900589785408989268791564800309543450530" +
                    "9385471595968562995762819631280551603822874246284493346689033984936971597562818580615189706952925518026801158664284712656280067439912216671693199268965961613231362525782754244074362083059"
                )
            ),
            Modulus = [0x11],
            D = [0x0]
        };

        public static void BinaryAppendSignature(
            MemoryStream stream,
            (int size, int padding) paddingInfo,
            string value)
        {
            if (paddingInfo.padding > 0)
            {
                stream.Seek(-paddingInfo.padding, SeekOrigin.Current);
            }

            for (var i = 0; i < 384; i++)
            {
                var code = value[i * 3 + 1] + value[i * 3 + 2];

                float.TryParse(
                    "0x" + code,
                    CultureInfo.CurrentCulture,
                    out var number);

                using var writer = new BinaryWriter(stream);
                writer.Write(number);
            }
        }

        public static (int size, int padding) BinaryPad(MemoryStream stream)
        {
            var size = (int)stream.Length;
            var remainder = size % 4;
            var padding = 0;

            if (remainder > 0)
            {
                padding = 4 - remainder;
                stream.Position = stream.Length;

                for (var i = 0; i < padding; i++)
                {
                    stream.WriteByte(0);
                }
            }

            return (size, padding);
        }

        public static bool RsaVerify(MemoryStream stream)
        {
            BinaryPad(stream);

            using var rsa = RSA.Create();
            rsa.ImportParameters(RsaParameters);

            var deformatter = new RSAPKCS1SignatureDeformatter(rsa);
            var formatter = new RSAPKCS1SignatureFormatter(rsa);

            var data = stream.ToArray();
            var signature = formatter.CreateSignature(data);

            if (deformatter.VerifySignature(data, signature))
            {
                return true;
            }

            Logger.Warn("signature is not valid.");
            return false;
        }

        private static byte[] ToUnsignedBigEndian(BigInteger value)
        {
            return value.ToByteArray(
                isUnsigned: true,
                isBigEndian: true);
        }
    }

    public class VSBBeatmap : Beatmap
    {
        public ModData data { get; set; } = new();
    }

    public class VSBHitObject : HitObject
    {
        public Dictionary<int, object> Extra { get; set; } = new();
    }

    public class VSBMod
    {
        public double Begin { get; set; }
        public double Duration { get; set; }
        public Func<float, float>? Ease { get; set; }
        public double End { get; set; }
        public double V1 { get; set; }
        public double V2 { get; set; }
        public byte ModIndex { get; set; }
        public sbyte Proxies { get; set; }
        public object? Mod { get; set; }
        public object? Weight { get; set; }
        public string Callback { get; set; }
    }

    public class ModData
    {
        public List<VSBMod> Mods { get; set; } = [];
        public List<VSBMod> PerFrame { get; set; } = [];
        public int Proxies { get; set; }
        public string Obj { get; set; } = "obj_base_gimmick";
    }

    /// <summary>
    /// Parsing of VSB charts (binary)
    /// </summary>
    /// <param name="buffer">contents of the .vsb file</param>
    /// <param name="bypass_mods">will disable all mods from being read</param>
    public class VSB_Chart_reader(BinaryReader buffer, bool bypass_mods = false)
    {
        public List<VSBHitObject> Notes { get; } = [];
        VSBBeatmap beatmap = new();

        // i think this just gets the "mod" from each index
        public string getModNameFromByte(int index, object ModObj)
        {
            return ModObj.ToString();
        }

        public VSBBeatmap Run()
        {
            while (true)
            {
                var flag = buffer.ReadByte();
                if (flag == 192) // all notes
                {
                    while (true)
                    {
                        var flag2 = buffer.ReadByte();
                        if (flag2 == 160)
                        {
                            VSBHitObject n = ReadNote();
                            if (n.Extra.TryGetValue(1, out object? bpm) && beatmap.BPMList._changes.Count == 1)
                            {
                                beatmap.BPMList.Add(n.Time, Convert.ToDouble(bpm));
                                continue;
                            }

                            if (n.Type == 3) // deprecated iirc
                                continue;
                            beatmap.HitObjects.Add(n);
                        }
                        else if (flag2 == 193) // end of notes
                            break;
                    }
                }

                if (flag == 224 && !bypass_mods) // all mods
                {
                    var proxies = 1;
                    var obj = "obj_base_gimmick";

                    var modList = new List<VSBMod>();
                    var perFrameList = new List<VSBMod>();

                    while (true)
                    {
                        var flag2 = buffer.ReadByte();

                        switch (flag2)
                        {
                            case 228:
                                proxies = buffer.ReadByte();
                                break;
                            case 229:
                                obj = buffer_readstring(buffer);
                                break;
                        }

                        if (flag2 == 226)
                        {
                            //obj = getModGimmickObj(data.obj);
                            while (true)
                            {
                                var flag3 = buffer.ReadByte();
                                switch (flag3)
                                {
                                    case 233: { // this doesnt actually make modregistry-valid mods, you gotta implement each and every chart mod individually...
                                        double time = buffer.ReadSingle();
                                        double d = buffer.ReadSingle();
                                        var ease = Eases.GetEase(buffer.ReadByte());
                                        double v1 = buffer.ReadSingle();
                                        double v2 = buffer.ReadSingle();
                                        byte modIndex = buffer.ReadByte();
                                        sbyte p = buffer.ReadSByte();

                                        var modifier = new VSBMod
                                        {
                                            Begin = time,
                                            Duration = d,
                                            Ease = ease,
                                            V1 = v1,
                                            V2 = v2,
                                            ModIndex = modIndex,
                                            Proxies = p,
                                            Mod = getModNameFromByte(modIndex, obj),
                                            Weight = 1, // mod weights later ngl
                                        };
                                        modList.Add(modifier);
                                        break;
                                    }
                                    case 236: {
                                        var modifier = new VSBMod
                                        {
                                            Begin = buffer.ReadSingle(),
                                            End = buffer.ReadSingle(),
                                            Callback = buffer_readstring(buffer),
                                        };
                                        perFrameList.Add(modifier);
                                        break;
                                    }
                                }
                                if (flag3 is 227) { break; }
                            }

                            beatmap.data = new ModData
                            {
                                Mods = modList,
                                PerFrame = perFrameList,
                                Proxies = proxies,
                                Obj = obj
                            };
                        }

                        if (flag2 == 225) // end of mods
                            break;
                    }
                }

                if (flag == 255) // end
                {
                    break;
                }
            }

            return beatmap;
        }

        private static string buffer_readstring(BinaryReader reader)
        {
            using var bytes = new MemoryStream();

            while (true)
            {
                var value = reader.ReadByte();
                bytes.WriteByte(value);
                if (value == 0) { break; }
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        // TODO parse into actual types because vs uses different syntax lol
        private VSBHitObject ReadNote()
        {
            var note = new VSBHitObject();
            var isLN = false;

            while (true)
            {
                var flag = buffer.ReadByte();
                VerifyFlag(flag, 160);
                switch (flag)
                {
                    case 162:
                        note.Type = buffer.ReadByte();
                        break;
                    case 163:
                        var n = buffer.ReadByte();
                        switch (note.Type)
                        {
                            case 0: note.Lane = n; note.Type = 0; break;      // normal
                            case 1: note.Lane = n + 4; note.Type = 0; break;          // bumper
                            case 2: note.Lane = n; note.Type = 0; isLN = true; break; // LN start/end, rest handled in Run()
                            case 6: note.Lane = n; note.Type = 2; break;
                            case 7: note.Lane = n + 4; note.Type = 2; break;
                            case 8: note.Lane = n + 4; note.Type = 1; break;
                            case 3: break;
                        }
                        break;
                    case 164: note.Time = Convert.ToDouble(buffer.ReadSingle()); break; // gamemaker bullshit
                    case 166:
                        while (true)
                        {
                            var type = buffer.ReadByte();
                            if (type == 167) { break; }
                            VerifyFlag(type, 176);
                            var _id = buffer.ReadByte();
                            var value = weirdenumthing(type);
                            note.Extra[_id] = value;
                        }
                        break;
                }

                if (flag == 161) { break; }
            }

            if (isLN && note.Extra.TryGetValue(1, out var k))
            {
                note.EndTime = Convert.ToDouble(k);
            }

            Logger.Log($"{note.Time}, {note.Lane},{note.Type}");
            note.Extra.ToList().ForEach(kvp => Console.WriteLine($"{kvp.Key}: {kvp.Value}"));
            return note;
        }

        private static void VerifyFlag(int value, int flag)
        {
            if ((value & flag) != flag)
            {
                throw new ArgumentException(@$"Failed to read map - {value} & {flag} != {flag} 3:");
            }
        }

        private object weirdenumthing(byte type)
        {
            return type switch
            {
                176 => buffer.ReadByte(),
                177 => buffer.ReadSByte(),
                178 => buffer.ReadUInt32(),
                179 => buffer.ReadInt32(),
                181 => buffer.ReadHalf(),
                182 => buffer.ReadSingle(),
                183 => buffer.ReadByte() != 0,
                184 => buffer_readstring(buffer),
                1 or 2 or 4 or 5 => buffer.ReadSingle(),
                3 or 6 => buffer.ReadByte(),
                7 => buffer.ReadSByte(),
                _ => throw new InvalidDataException($" how thef uck: {type}")
            };
        }
    }

    public static Beatmap Parse(string file, bool rsa = true, bool arg2 = false)
    {
        if (!File.Exists(file))
        {
            return new Beatmap();
        }

        using var fileStream = File.OpenRead(file);
        using var buffer = new BinaryReader(fileStream);

        if (rsa)
        {
            buffer.BaseStream.Seek(0, 0); // idk if this is needed but
        }

        var b1 = buffer.ReadByte();
        var b2 = buffer.ReadByte();
        var b3 = buffer.ReadByte();
        var b4 = buffer.ReadByte();
        var b5 = buffer.ReadByte();

        if (b1 == 86 && // V
            b2 == 83 && // S
            b3 == 67 && // C
            b4 == 1 && // not sure what was here before but useless case statement was there
            b5 == 0) // null
        {
            var reader = new VSB_Chart_reader(buffer, arg2);
            VSBBeatmap map = reader.Run();
            return map;
        }
        Logger.Error(@"invalid VSB header");
        return new Beatmap();
    }
}
using System;
using System.Linq;
using Microsoft.Xna.Framework;

namespace ldvs.Core.Content.Entities;

public class _vs : ISongParser
{
	public static Beatmap Parse(string text, BeatmapSet bms)
    {
	    var map = new Beatmap();

	    string? currentSection = null;

	    var lines = text.Split(new[] { $"\r\n", $"\n" }, StringSplitOptions.None);

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

			    continue;
		    }

		    if (currentSection == null)
			    continue;

		    switch (currentSection)
		    {
			    case "General": ParseGeneralLine(line, map.General); break;
			    case "Metadata": ParseMetadata(line, map.BMPData, bms.Metadata); break;
			    case "Difficulty": ParseDifficulty(line, map.BMPData); break;
			    case "TimingPoints": ParseTimingPointLine(line, map); break;
			    case "HitObjects": ParseNote(line, map); break;
			    case "Events":
				    // parse later
				    break;
		    }
	    }

	    return map;
    }

	private static void ParseDifficulty(string line, BMPData md)
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
	            Color color;
	            if (DumbXnaColorLookupThingy.TryHex(value, out color))
	            {
		            md.DifficultyColor = color;
	            }
	            if (DumbXnaColorLookupThingy.TryXNA(value, out color))
	            {
		            md.DifficultyColor = color;
	            }
                break;
        }
    }

	private static void ParseGeneralLine(string line, General general)
	{
		if (!SPHelper.TryParseKeyValue(line, out var key, out var value))
			return;

		switch (key)
		{
			case "AudioFilename": general.AudioFilename = value; break;
			case "AudioLeadIn":
				if (SPHelper.TryParseInt(value, out var leadIn))
					general.AudioLeadIn = leadIn;

				break;
			case "PreviewTime":
				if (SPHelper.TryParseInt(value, out var previewTime))
					general.PreviewPoint = previewTime;
				break;
		}
	}

	private static void ParseMetadata(string line, BMPData md, BMSData bms)
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

	private static void ParseNote(string line, Beatmap map)
    {
	    var parts = SPHelper.SplitCSVLine(line);

	    if (parts.Length < 4)
		    return;

	    if (!SPHelper.TryParseInt(parts[0], out var column) ||
		    !SPHelper.TryParseInt(parts[1], out var time) ||
		    !SPHelper.TryParseInt(parts[2], out var endTime) ||
		    !SPHelper.TryParseInt(parts[3], out var type))
		    return;

	    map.HitObjects.Add(
	            new HitObject
	            {
	                Lane = column,
	                Time = time,
	                EndTime = endTime,
	                Type = type
	            });
    }

	private static void ParseTimingPointLine(string line, Beatmap map)
	{
		var parts = SPHelper.SplitCSVLine(line);

		if (parts.Length < 7)
			return;

		if (!SPHelper.TryParseDouble(parts[0], out var offset) || !SPHelper.TryParseDouble(parts[1], out var beatLength) || !SPHelper.TryParseInt(parts[2], out var meter) || !SPHelper.TryParseInt(parts[6], out var inheritedValue))
		{
			return;
		}

		if (inheritedValue == 1)
		{
			map.BPMList.Add(offset, beatLength);
		}
		else
		{
			map.Mods.Add(ModRegistry.Create("globalscrollspeed", offset, double.PositiveInfinity, (float)(100.0 / Math.Abs(beatLength))));
		}

	}
};
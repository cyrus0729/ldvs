using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;

namespace ldvs.Core.Content.Entities;

public static class DumbXnaColorLookupThingy
{
	private static readonly Dictionary<string, Color> dictionary =
		typeof(Color)
			.GetProperties(BindingFlags.Public | BindingFlags.Static)
			.Where(prop => prop.PropertyType == typeof(Color))
			.ToDictionary(
				prop => prop.Name,
				prop => (Color)prop.GetValue(null, null)!, StringComparer.OrdinalIgnoreCase);

	public static Color Hex(string hex)
	{
		hex = hex.TrimStart('#');

		if (hex.Length == 6)
		{
			int rgb = int.Parse(hex, NumberStyles.HexNumber);

			return new Color(
				(rgb >> 16) & 0xFF, // R
				(rgb >> 8) & 0xFF,  // G
				rgb & 0xFF          // B
			);
		}

		if (hex.Length == 8)
		{
			uint rgba = uint.Parse(hex, NumberStyles.HexNumber);

			return new Color(
				(byte)((rgba >> 24) & 0xFF), // R
				(byte)((rgba >> 16) & 0xFF), // G
				(byte)((rgba >> 8) & 0xFF),  // B
				(byte)(rgba & 0xFF)          // A
			);
		}

		throw new ArgumentException();
	}

	public static bool TryHex(string hex, out Color color)
	{
		try {
			color = Hex(hex);
			return true;
		}
		catch (ArgumentException) {
			color = Color.Black;
			return false; }
	}

	public static bool TryXNA(string name, out Color color)
	{
		try
		{
			color = XNAName(name);
			return true;
		}
		catch (ArgumentException)
		{
			color = Color.Black;
			return false;
		}
	}

	public static Color XNAName(string name)
	{
		if (dictionary.TryGetValue(name, out Color color))
			return color;

		throw new ArgumentException();
	}
}
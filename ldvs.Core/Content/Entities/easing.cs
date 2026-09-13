using System;
using System.Collections.Generic;

namespace ldvs.Core.Content.Entities;

public static class Eases
{
	public static readonly Dictionary<int, Func<float, float>?> eases = new()
	{
		{ 1, Linear },
		{ 2, OutElastic },
		{ 3, InExpo },
		{ 4, OutExpo },
		{ 5, InOutExpo },
		{ 6, InQuad },
		{ 7, OutQuad },
		{ 8, InOutQuad },
		{ 9, InCubic },
		{ 10, OutCubic },
		{ 11, InOutCubic },
		{ 12, OutBack },
		{ 13, InSine },
		{ 14, OutSine },
		{ 15, InOutSine },
		{ 16, OutQuart },
		{ 17, InOutCirc },
		{ 18, InCirc },
		{ 19, OutCirc }
	};

	public static Func<float, float>? GetEase(int val)
	{
		return eases.GetValueOrDefault(val);
	}

	public static float InBack(float t)
	{
		float s = 1.70158f;
		return t * t * ((s + 1) * t - s);
	}

	public static float InBounce(float t) => 1 - OutBounce(1 - t);
	public static float InCirc(float t) => -((float)Math.Sqrt(1 - t * t) - 1);
	public static float InCubic(float t) => t * t * t;
	public static float InElastic(float t) => 1 - OutElastic(1 - t);
	public static float InExpo(float t) => (float)Math.Pow(2, 10 * (t - 1));

	public static float InOutBack(float t)
	{
		if (t < 0.5) return InBack(t * 2) / 2;
		return 1 - InBack((1 - t) * 2) / 2;
	}

	public static float InOutBounce(float t)
	{
		if (t < 0.5) return InBounce(t * 2) / 2;
		return 1 - InBounce((1 - t) * 2) / 2;
	}

	public static float InOutCirc(float t)
	{
		if (t < 0.5) return InCirc(t * 2) / 2;
		return 1 - InCirc((1 - t) * 2) / 2;
	}

	public static float InOutCubic(float t)
	{
		if (t < 0.5) return InCubic(t * 2) / 2;
		return 1 - InCubic((1 - t) * 2) / 2;
	}

	public static float InOutElastic(float t)
	{
		if (t < 0.5) return InElastic(t * 2) / 2;
		return 1 - InElastic((1 - t) * 2) / 2;
	}

	public static float InOutExpo(float t)
	{
		if (t < 0.5) return InExpo(t * 2) / 2;
		return 1 - InExpo((1 - t) * 2) / 2;
	}

	public static float InOutQuad(float t)
	{
		if (t < 0.5) return InQuad(t * 2) / 2;
		return 1 - InQuad((1 - t) * 2) / 2;
	}

	public static float InOutQuart(float t)
	{
		if (t < 0.5) return InQuart(t * 2) / 2;
		return 1 - InQuart((1 - t) * 2) / 2;
	}

	public static float InOutQuint(float t)
	{
		if (t < 0.5) return InQuint(t * 2) / 2;
		return 1 - InQuint((1 - t) * 2) / 2;
	}

	public static float InOutSine(float t) => (float)(Math.Cos(t * Math.PI) - 1) / -2;
	public static float InQuad(float t) => t * t;
	public static float InQuart(float t) => t * t * t * t;
	public static float InQuint(float t) => t * t * t * t * t;
	public static float InSine(float t) => 1 - (float)Math.Cos(t * Math.PI / 2);

	public static float Linear(float t) => t;
	public static float OutBack(float t) => 1 - InBack(1 - t);

	public static float OutBounce(float t)
	{
		float div = 2.75f;
		float mult = 7.5625f;
		if (t < 1 / div) { return mult * t * t; }
		if (t < 2 / div) { t -= 1.5f / div; return mult * t * t + 0.75f; }
		if (t < 2.5 / div) { t -= 2.25f / div; return mult * t * t + 0.9375f; }
		t -= 2.625f / div; return mult * t * t + 0.984375f;
	}

	public static float OutCirc(float t) => 1 - InCirc(1 - t);
	public static float OutCubic(float t) => 1 - InCubic(1 - t);

	public static float OutElastic(float t)
	{
		float p = 0.3f;
		return (float)Math.Pow(2, -10 * t) * (float)Math.Sin((t - p / 4) * (2 * Math.PI) / p) + 1;
	}

	public static float OutExpo(float t) => 1 - InExpo(1 - t);
	public static float OutQuad(float t) => 1 - InQuad(1 - t);
	public static float OutQuart(float t) => 1 - InQuart(1 - t);
	public static float OutQuint(float t) => 1 - InQuint(1 - t);
	public static float OutSine(float t) => (float)Math.Sin(t * Math.PI / 2);
}
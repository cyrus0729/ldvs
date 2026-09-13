using System;
using System.Collections.Generic;
using System.Reflection;

namespace ldvs.Core.Content.Entities;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ModAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}

public enum ModMode
{
    OneShot, // anikopancakes
    Repeating
}

/// <summary>
/// base mod object, extend every variant from here
/// </summary>
public abstract class Mod
{
    public double startTime { get; set; }
    public double endTime { get; set; }

    public virtual ModMode Mode => ModMode.OneShot;
    internal bool HasRun { get; set; }

    protected Mod(double startTime, double endTime = double.PositiveInfinity)
    {
        this.startTime = startTime;
        this.endTime = endTime;
    }

    protected Mod()
    {
        startTime = endTime = 0;
    }

    public abstract void Apply(ModCtx ctx);
}

public abstract class RepeatingMod(double startTime, double intervalMs) : Mod
{
    private double _nextRunTime = startTime;
    public override ModMode Mode => ModMode.Repeating;

    internal void ApplyIfDue(double songTime, ModCtx ctx)
    {
        if (songTime < _nextRunTime)
            return;

        Apply(ctx);

        do
        {
            _nextRunTime += intervalMs;
        } while (_nextRunTime <= songTime);
    }
}

public abstract class EasingMod(Func<double, float> easing) : Mod
{
    protected double GetEasedProgress(double songTime)
    {
        return easing(GetProgress(songTime));
    }

    protected double GetProgress(double songTime)
    {
        if (endTime <= startTime)
            return 1.0;

        return Math.Clamp(
            (songTime - startTime) / (endTime - startTime),
            0.0,
            1.0);
    }
}

/// <summary>
///     mods can only interact with exposed values here, might remove later not gonna lie
/// </summary>
/// <param name="values"></param>
/// <param name="songTime"> current ms </param>
/// <param name="progress"> mod progress </param>
public sealed class ModCtx(ModdableValues values, double songTime, double progress)
{
    public ModdableValues Values { get; } = values;
    public double SongTime { get; } = songTime;
    public double Progress { get; } = progress;

    public void AddCameraX(float amount)
        => Values.CameraX += amount;

    public void AddCameraY(float amount)
        => Values.CameraY += amount;

    public void AddCameraZoom(float amount)
        => Values.CameraZoom += amount;

    public void AddNoteY(int lane, float value)
        => Values.NoteY[lane] += value;

    public void AddReceptorAlpha(float amount)
        => Values.ReceptorAlpha += amount;

    public void AddScrollSpeed(int lane, float amount)
        => Values.ScrollSpeedMultipliers[lane] += amount;

    public void SetCameraX(float value)
        => Values.CameraX = value;

    public void SetCameraY(float value)
        => Values.CameraY = value;

    public void SetCameraZoom(float value)
        => Values.CameraZoom = value;

    public void SetNoteX(int lane, float value)
        => Values.NoteX[lane] = value;

    public void SetNoteY(int lane, float value)
        => Values.NoteY[lane] = value;

    public void SetReceptorAlpha(float value)
        => Values.ReceptorAlpha = value;

    public void SetScrollSpeed(int lane, float amount)
        => Values.ScrollSpeedMultipliers[lane] = amount;
}

/// <summary>
/// entry point of all mod management, make one for each playfield if more will be made
/// </summary>
public sealed class ModManager
{
    private readonly List<Mod> _mods = [];
    private readonly List<Mod> _activeMods = [];

    private int _nextMod;
    private double _lastSongTime = double.NegativeInfinity;

    public ModdableValues Values { get; } = new();

    public void Add(Mod mod)
    {
        _mods.Add(mod);
        _mods.Sort(static (a, b) => a.startTime.CompareTo(b.startTime));
    }

    public void Clear()
    {
        _mods.Clear();
        _activeMods.Clear();
        _nextMod = 0;
    }

    public void Reset()
    {
        _activeMods.Clear();

        foreach (var mod in _mods)
            mod.HasRun = false;

        _nextMod = 0;
        _lastSongTime = double.NegativeInfinity;
    }

    public void Update(double songTime)
    {
        if (songTime < _lastSongTime) // back-seeking
        {
            Reset();
        }
        _lastSongTime = songTime;

        while (_nextMod < _mods.Count - 1 && _mods[_nextMod].startTime <= songTime) // apply active mods
        {
            var mod = _mods[_nextMod++];

            if (mod.Mode == ModMode.OneShot)
            {
                if (!mod.HasRun)
                {
                    var context = new ModCtx(Values, songTime, 1.0);
                    mod.Apply(context);
                    mod.HasRun = true;
                }
                continue;
            }

            _activeMods.Add(mod);
        }

        for (int i = _activeMods.Count - 1; i >= 0; i--) // any active mods
        {
            var mod = _activeMods[i];

            if (songTime > mod.endTime)
            {
                _activeMods.RemoveAt(i);
                continue;
            }

            double progress = mod.endTime <= mod.startTime ? 1.0 : Math.Clamp((songTime - mod.startTime) / (mod.endTime - mod.startTime), 0.0, 1.0);

            var context = new ModCtx(Values, songTime, progress);

            if (mod is RepeatingMod repeating)
            {
                repeating.ApplyIfDue(songTime, context);
            }
            else
            {
                mod.Apply(context);
            }
        }
    }
}

public sealed class ModdableValues // please don't add anything here that you won't want people to change.
{
    public float[] NoteX { get; set; } = [0, 0, 0, 0, 0, 0, 0];
    public float[] NoteY { get; set; } = [0, 0, 0, 0, 0, 0, 0];
    public float[] NoteZ { get; set; } = [0, 0, 0, 0, 0, 0, 0];
    public float[] HoldX { get; set; } = [0, 0, 0, 0, 0, 0, 0];
    public float[] HoldY { get; set; } = [0, 0, 0, 0, 0, 0, 0];
    public float[] HoldZ { get; set; } = [0, 0, 0, 0, 0, 0, 0];
    public float[] NoteAngles { get; set; } = [0, 0, 0, 0, 0, 0, 0];
    public float[] NoteScale { get; set; } = [0, 0, 0, 0, 0, 0, 0];

    public float ReceptorAlpha { get; set; } = 1f;

    public float CameraX { get; set; }
    public float CameraY { get; set; }
    public float CameraZoom { get; set; } = 1f;

    public float HudAlpha { get; set; } = 1f;
    public float[] ScrollSpeedMultipliers { get; set; } = [1, 1, 1, 1, 1, 1, 1];
    public float ReceptorY { get; set; } = 0f;
}

public sealed class GlobalSettings // unsure if this is optimal, maybe change later
{
    public bool Autoplay { get; set; } = true;
    public float ReceptorY { get; set; } = 1000;
    public float SpawnDistance { get; set; } = 1000f;
    public float CenterX { get; set; } = 1000;
    public float LaneWidth { get; set; } = 170;
    public double TravelTimeMs { get; set; } = 500;
}

/// <summary>
/// registry of every single available mod using reflection (because manually registering is for chumps!!)
/// </summary>
public static class ModRegistry // reflection looks fucking insane from first sight man
{
    public static readonly Dictionary<string, Type> modTypes = [];

    static ModRegistry()
	{
		RegisterMods(typeof(ModRegistry).Assembly);
	}

    public static Mod Create(string id, params object[] arguments)
	{
		if (!modTypes.TryGetValue(id, out var type))
			throw new KeyNotFoundException($"Unknown mod: {id}");
		return (Mod)Activator.CreateInstance(type, arguments)!;
	}

    public static bool TryGetModType(string id, out Type? modType)
	{
		return modTypes.TryGetValue(id, out modType);
	}

    private static void RegisterMods(Assembly assembly)
	{
		foreach (var type in assembly.GetTypes())
		{
			if (!typeof(Mod).IsAssignableFrom(type) || !type.IsClass || type.IsAbstract)
			{ continue; }

			var attribute = type.GetCustomAttribute<ModAttribute>();
			if (attribute != null) modTypes[attribute.Id] = type;
		}
	}
}

/// <summary>
/// you should probably move all these to different .cs files
/// </summary>
public class ModImplementations
{
    // custom mods
    [Mod("notescrollspeed")]
    public sealed class NoteScrollSpeed : Mod
    {
        private readonly float _multiplier;
        private readonly int _lane;

        public NoteScrollSpeed(double offset,int lane, float multiplier)
        {
            _multiplier = multiplier;
            _lane = lane;
            startTime = offset;
            endTime = offset;
        }

        public override void Apply(ModCtx ctx)
        {
            ctx.SetScrollSpeed(_lane, _multiplier);
        }
    }

    [Mod("globalscrollspeed")]
    public sealed class GlobalScrollSpeed : Mod
    {
        private readonly float _multiplier;

        public GlobalScrollSpeed(double offset, double duration, float multiplier)
        {
            _multiplier = multiplier;
            startTime = offset;
            endTime = offset + duration;
        }

        public override void Apply(ModCtx ctx)
        {
            for (int i = 0; i < 7; i++)
            {
                ctx.SetScrollSpeed(i, _multiplier);
            }
        }
    }

    [Mod("Sinewave")]
	public sealed class Sinewave(float amplitude, float frequency, int lane) : Mod
	{
        public override void Apply(ModCtx ctx)
		{
			ctx.SetNoteY(lane,amplitude * MathF.Sin((float)(ctx.SongTime * Math.PI * 2.0 * frequency)));
		}
    }

    [Mod("BGImage")]
	public sealed class BGImage(double offset, string filename, int x, int y, int extra) : Mod
	{
        string FileName = filename;
        int X = x;
        int Y = y;
        int Extra = extra;

        public override void Apply(ModCtx ctx)
        {
            throw new NotImplementedException();
        }
    }

    [Mod("Video")]
	public sealed class Video(double offset, string filename) : Mod
	{
        string FileName = filename;

        public override void Apply(ModCtx ctx)
        {
            throw new NotImplementedException();
        }
    }

    [Mod("Sample")]
	public sealed class Sample(double offset, string filename, int layer, int volume) : Mod
	{
        string FileName = filename;
        int Layer = layer;
        int Volume = volume;

        public override void Apply(ModCtx ctx)
        {
            throw new NotImplementedException();
        }
    }

    public bool isChartSpecificMod(int arg0)
	{
		return (arg0 & 128) == 128;
	}

#region ModImplementations VS-RELATED-BULLSHIT

    [Mod("unknown")] public sealed class Unknown : Mod
    {
        public Unknown(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prx")] public sealed class Prx : Mod
    {
        public Prx(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prxb")] public sealed class Prxb : Mod
    {
        public Prxb(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prxc")] public sealed class Prxc : Mod
    {
        public Prxc(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("pry")] public sealed class Pry : Mod
    {
        public Pry(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("pryb")] public sealed class Pryb : Mod
    {
        public Pryb(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("pryc")] public sealed class Pryc : Mod
    {
        public Pryc(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prsx")] public sealed class Prsx : Mod
    {
        public Prsx(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("pra")] public sealed class Pra : Mod
    {
        public Pra(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("przm")] public sealed class Przm : Mod
    {
        public Przm(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("przmb")] public sealed class Przmb : Mod
    {
        public Przmb(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("przx")] public sealed class Przx : Mod
    {
        public Przx(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("przy")] public sealed class Przy : Mod
    {
        public Przy(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prrx")] public sealed class Prrx : Mod
    {
        public Prrx(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prry")] public sealed class Prry : Mod
    {
        public Prry(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prrz")] public sealed class Prrz : Mod
    {
        public Prrz(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prrzb")] public sealed class Prrzb : Mod
    {
        public Prrzb(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shxs")] public sealed class Shxs : Mod
    {
        public Shxs(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shxp")] public sealed class Shxp : Mod
    {
        public Shxp(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shxa")] public sealed class Shxa : Mod
    {
        public Shxa(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shys")] public sealed class Shys : Mod
    {
        public Shys(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shyp")] public sealed class Shyp : Mod
    {
        public Shyp(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shya")] public sealed class Shya : Mod
    {
        public Shya(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("scrollspeed")] public sealed class ScrollSpeed : Mod
    {
        private readonly float _multiplier;

        public ScrollSpeed(double Begin, double Duration, Func<float, float> Ease, double V1, double V2)
        {
            startTime = Begin;
            endTime = Begin + Duration;
        }

        public override void Apply(ModCtx ctx)
        {
            for (int i = 0; i < 7; i++)
            {
                ctx.SetScrollSpeed(i, _multiplier);
            }
        }
    }

    [Mod("noterot")] public sealed class NoteRot : Mod
    {
        public NoteRot(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("velocity")] public sealed class Velocity : Mod
    {
        public Velocity(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("spinradius")] public sealed class SpinRadius : Mod
    {
        public SpinRadius(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("spiny")] public sealed class SpinY : Mod
    {
        public SpinY(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("spinx")] public sealed class SpinX : Mod
    {
        public SpinX(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("driven")] public sealed class Driven : Mod
    {
        public Driven(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("beat")] public sealed class Beat : Mod
    {
        public Beat(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("wave")] public sealed class Wave : Mod
    {
        public Wave(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("hom")] public sealed class Hom : Mod
    {
        public Hom(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("boost_distance")] public sealed class BoostDistance : Mod
    {
        public BoostDistance(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("boost_time")] public sealed class BoostTime : Mod
    {
        public BoostTime(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("yoffset")] public sealed class YOffset : Mod
    {
        public YOffset(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("notealp")] public sealed class NoteAlpha : Mod
    {
        public NoteAlpha(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("przmc")] public sealed class Przmc : Mod
    {
        public Przmc(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prxd")] public sealed class Prxd : Mod
    {
        public Prxd(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("pryd")] public sealed class Pryd : Mod
    {
        public Pryd(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prct")] public sealed class Prct : Mod
    {
        public Prct(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prcb")] public sealed class Prcb : Mod
    {
        public Prcb(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prcl")] public sealed class Prcl : Mod
    {
        public Prcl(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prcr")] public sealed class Prcr : Mod
    {
        public Prcr(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("prvib")] public sealed class Prvib : Mod
    {
        public Prvib(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shct")] public sealed class Shct : Mod
    {
        public Shct(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shft")] public sealed class Shft : Mod
    {
        public Shft(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shcb")] public sealed class Shcb : Mod
    {
        public Shcb(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shfb")] public sealed class Shfb : Mod
    {
        public Shfb(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shcl")] public sealed class Shcl : Mod
    {
        public Shcl(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shfl")] public sealed class Shfl : Mod
    {
        public Shfl(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shcr")] public sealed class Shcr : Mod
    {
        public Shcr(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("shfr")] public sealed class Shfr : Mod
    {
        public Shfr(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("scrollind0")] public sealed class ScrollInd0 : Mod
    {
        public ScrollInd0(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("scrollind1")] public sealed class ScrollInd1 : Mod
    {
        public ScrollInd1(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("scrollind2")] public sealed class ScrollInd2 : Mod
    {
        public ScrollInd2(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("scrollind3")] public sealed class ScrollInd3 : Mod
    {
        public ScrollInd3(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("scrollind4")] public sealed class ScrollInd4 : Mod
    {
        public ScrollInd4(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("scrollind5")] public sealed class ScrollInd5 : Mod
    {
        public ScrollInd5(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("scrollind6")] public sealed class ScrollInd6 : Mod
    {
        public ScrollInd6(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("drawdist")] public sealed class DrawDist : Mod
    {
        public DrawDist(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("pburstleft")] public sealed class PBurstLeft : Mod
    {
        public PBurstLeft(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("pburstright")] public sealed class PBurstRight : Mod
    {
        public PBurstRight(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("particlexpower")] public sealed class ParticleXPower : Mod
    {
        public ParticleXPower(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("particleypower")] public sealed class ParticleYPower : Mod
    {
        public ParticleYPower(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("uialpha")] public sealed class UIAlpha : Mod
    {
        public UIAlpha(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("fx_contrast")] public sealed class FxContrast : Mod
    {
        public FxContrast(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("fx_chroma_distort")] public sealed class FxChromaDistort : Mod
    {
        public FxChromaDistort(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("fx_film")] public sealed class FxFilm : Mod
    {
        public FxFilm(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("fx_glow")] public sealed class FxGlow : Mod
    {
        public FxGlow(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("fx_particleglow")] public sealed class FxParticleGlow : Mod
    {
        public FxParticleGlow(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("pburstspeed")] public sealed class PBurstSpeed : Mod
    {
        public PBurstSpeed(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("freeze")] public sealed class Freeze : Mod
    {
        public Freeze(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    [Mod("drawuntil")] public sealed class DrawUntil : Mod
    {
        public DrawUntil(double Begin, double Duration, Func<float,float> Ease, double V1, double V2) { startTime = Begin; endTime = Begin + Duration; }
        public override void Apply(ModCtx ctx) { }
    }

    public static readonly (string Mod, Type Type)[] ModLookup =
    [
        ("unknown", typeof(Unknown)),
        ("prx", typeof(Prx)),
        ("prxb", typeof(Prxb)),
        ("prxc", typeof(Prxc)),
        ("pry", typeof(Pry)),
        ("pryb", typeof(Pryb)),
        ("pryc", typeof(Pryc)),
        ("prsx", typeof(Prsx)),
        ("pra", typeof(Pra)),
        ("przm", typeof(Przm)),
        ("przmb", typeof(Przmb)),
        ("przx", typeof(Przx)),
        ("przy", typeof(Przy)),
        ("prrx", typeof(Prrx)),
        ("prry", typeof(Prry)),
        ("prrz", typeof(Prrz)),
        ("prrzb", typeof(Prrzb)),
        ("shxs", typeof(Shxs)),
        ("shxp", typeof(Shxp)),
        ("shxa", typeof(Shxa)),
        ("shys", typeof(Shys)),
        ("shyp", typeof(Shyp)),
        ("shya", typeof(Shya)),
        ("scrollspeed", typeof(ScrollSpeed)),
        ("noterot", typeof(NoteRot)),
        ("velocity", typeof(Velocity)),
        ("spinradius", typeof(SpinRadius)),
        ("spiny", typeof(SpinY)),
        ("spinx", typeof(SpinX)),
        ("driven", typeof(Driven)),
        ("beat", typeof(Beat)),
        ("wave", typeof(Wave)),
        ("hom", typeof(Hom)),
        ("boost_distance", typeof(BoostDistance)),
        ("boost_time", typeof(BoostTime)),
        ("yoffset", typeof(YOffset)),
        ("notealp", typeof(NoteAlpha)),
        ("przmc", typeof(Przmc)),
        ("prxd", typeof(Prxd)),
        ("pryd", typeof(Pryd)),
        ("prct", typeof(Prct)),
        ("prcb", typeof(Prcb)),
        ("prcl", typeof(Prcl)),
        ("prcr", typeof(Prcr)),
        ("prvib", typeof(Prvib)),
        ("shct", typeof(Shct)),
        ("shft", typeof(Shft)),
        ("shcb", typeof(Shcb)),
        ("shfb", typeof(Shfb)),
        ("shcl", typeof(Shcl)),
        ("shfl", typeof(Shfl)),
        ("shcr", typeof(Shcr)),
        ("shfr", typeof(Shfr)),
        ("scrollind0", typeof(ScrollInd0)),
        ("scrollind1", typeof(ScrollInd1)),
        ("scrollind2", typeof(ScrollInd2)),
        ("scrollind3", typeof(ScrollInd3)),
        ("scrollind4", typeof(ScrollInd4)),
        ("scrollind5", typeof(ScrollInd5)),
        ("scrollind6", typeof(ScrollInd6)),
        ("drawdist", typeof(DrawDist)),
        ("pburstleft", typeof(PBurstLeft)),
        ("pburstright", typeof(PBurstRight)),
        ("particlexpower", typeof(ParticleXPower)),
        ("particleypower", typeof(ParticleYPower)),
        ("uialpha", typeof(UIAlpha)),
        ("fx_contrast", typeof(FxContrast)),
        ("fx_chroma_distort", typeof(FxChromaDistort)),
        ("fx_film", typeof(FxFilm)),
        ("fx_glow", typeof(FxGlow)),
        ("fx_particleglow", typeof(FxParticleGlow)),
        ("pburstspeed", typeof(PBurstSpeed)),
        ("freeze", typeof(Freeze)),
        ("drawuntil", typeof(DrawUntil))
    ];

#endregion
}
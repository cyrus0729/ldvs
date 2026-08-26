using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Scenes;
using Color = Microsoft.Xna.Framework.Color;

namespace ldvs.Core.Content.Entities;

public static class JudgementRangeExtensions
{
    public static JudgementMS.Judgement maxRange(this JudgementMS.Judgement[] judgements)
    {
        if (judgements.Length == 0)
            throw new InvalidOperationException("The judgement list is empty.");

        JudgementMS.Judgement widest = judgements[0];
        int widestSize = widest.Max - widest.Min;

        foreach (JudgementMS.Judgement judgement in judgements)
        {
            int size = judgement.Max - judgement.Min;

            if (size > widestSize)
            {
                widest = judgement;
                widestSize = size;
            }
        }

        return widest;
    }

    public static JudgementMS.Judgement window(this JudgementMS.Judgement[] judgements, JudgementMS.Judge judge)
    {
        if (judgements.Length == 0)
            throw new InvalidOperationException("The judgement list is empty.");

        return judgements.First(n => n.Name == judge);
    }
}

public static class JudgementMS
{
    public record Judgement(Judge Name, int Min, int Max);

    public enum Judge
    {
        Marvelous=0,
        Perfect=1,
        Great=2,
        Good=3,
        Miss=4
    }

    public static readonly Judgement[] Normal =
    {
        new(Judge.Marvelous, -35, 35),
        new(Judge.Perfect, -70, 70),
        new(Judge.Great, -110, 110),
        new(Judge.Good, -150, 200),
        new(Judge.Miss, -200, 200),
    };

    public static readonly Judgement[] Bumper =
    {
        new(Judge.Marvelous, -200, 200),
    };

    public static readonly Judgement[] LN =
    {
        new(Judge.Marvelous, -150, 150),
    };

    public static (Judge? Name, double delta) HandleJudgement(Judgement[] judges, double t, double hit)
    {
        double delta = (t - hit);
        foreach (var window in judges)
        {
            if (window.Min <= delta && delta <= window.Max)
            {
                return (window.Name,delta);
            }
        }
        return (null,0);
    }
}

public interface IPlayfield
{
    // make all these customizable later btw
    const bool autoplay = false;
    const int receptorY = 1000;
    const int spawnY = 0;
    const int centerX = 1000;
    const int laneWidth = 170;
    const int travelTimeMs = 500;
    public Beatmap _map { get; set; }
}

public interface ITimingPointProvider
{
    IReadOnlyList<TimingPoint> Uninherited { get; }
    IReadOnlyList<TimingPoint> Inherited { get; }
}

public sealed class TimingPointProvider(
    IReadOnlyList<TimingPoint> uninherited,
    IReadOnlyList<TimingPoint> inherited) : ITimingPointProvider
{
    public IReadOnlyList<TimingPoint> Uninherited { get; } = uninherited;
    public IReadOnlyList<TimingPoint> Inherited { get; } = inherited;

    public static double getBaseBeatLength(List<TimingPoint> timingPoints)
    {
        TimingPoint? firstUninherited = timingPoints
            .Where(point => point.Uninherited)
            .OrderBy(point => point.offset)
            .FirstOrDefault();

        return firstUninherited?.MsPerBeat ?? throw new InvalidOperationException("map has no uninherited timing point");
    }

    public static TimingPoint GetTimingPoint(double time,
        List<TimingPoint> timingPoints)
    {
        return timingPoints
                .Where(point => point.offset <= time)
                .OrderBy(point => point.offset)
                .LastOrDefault() ??
            timingPoints
                .Where(point => point.MsPerBeat > 0)
                .OrderBy(point => point.offset)
                .First();
    }
}

public interface INoteHandler
{
    List<int> JudgeCount { get; set; }
    JudgementMS.Judge? mostRecentJudge { get; set; }
    double? mostRecentDelta { get; set; }

    // yayyyy i hope theres a better way to do this shit later

    public Stack<NoteHandler.DisplayJudgeInstance> JudgeInstances { get; set; }

    public List<double> LastHeld { get; set; }
    public List<bool> LanesBlocked { get; set; }
    public List<bool> LanesHeld { get; set; }
    public List<List<NoteInstance>> VSLanes { get; set; }
    public List<int> VSLanesNext { get; set; }
    public List<List<NoteHandler.HoldInstance>> HoldLanes { get; set; }
    IReadOnlyList<NoteInstance> notes { get; }
    void AddHold(int lane, int note, bool held);
    bool InputLaneHeld(int lane);

    bool InputLanePress(int lane);
    bool InputLaneRelease(int lane);
    void ResolveNote(NoteInstance note, JudgementMS.Judge judge, double delta = double.PositiveInfinity);
}

public sealed class NoteHandler : INoteHandler
{
    public sealed class HoldInstance(int arg0, bool held, int piece)
    {
        public int j { get; } = arg0;
        public bool Held { get; set; } = held;
        public int Piece { get; set; } = piece;
    }

    public sealed class DisplayJudgeInstance(JudgementMS.Judge j, int l)
    {
        public JudgementMS.Judge judge = j;
        public int lane = l;
    }

    private readonly List<Keys> _laneInputKeys;

    public IReadOnlyList<NoteInstance> notes { get; set; } = Array.Empty<NoteInstance>();

    public List<int> JudgeCount { get; set; } = [0, 0, 0, 0, 0];
    public JudgementMS.Judge? mostRecentJudge { get; set; }
    public double? mostRecentDelta { get; set; }

    public List<List<bool>> _laneInputStates { get; set; }  = [[false, false, false], [false, false, false], [false, false, false], [false, false, false]];

    public List<double> LastHeld { get; set; } = [double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity ];

    public List<bool> LanesBlocked { get; set; } = [false, false, false, false, false, false, false];
    public List<bool> LanesHeld { get; set; } = [false, false, false, false, false, false, false];
    public List<List<NoteInstance>> VSLanes { get; set; } = [[], [], [], [], [], [], []];
    public List<int> VSLanesNext { get; set; } = [0, 0, 0, 0, 0, 0, 0];
    public List<List<HoldInstance>> HoldLanes { get; set; } = [[], [], [], [], [], [], []];

    public Stack<DisplayJudgeInstance> JudgeInstances { get; set; } = new();

    public NoteHandler(
        List<Keys> binds)
    {
        if (binds.Count != 4)
            throw new ArgumentException(
                @"four input bindings are required,,,,,,,,,,,",
                nameof(binds));

        _laneInputKeys = binds;
        VSLanes = InitLanes(notes);
    }

    public void AddHold(int lane, int j, bool held)
    {
        var note = VSLanes[lane][j];

        if (!held)
        {
            ResolveNote(note, JudgementMS.Judge.Miss);
            return;
        }
        note.isHeldVisual = true;
        HoldLanes[lane].Add(new HoldInstance(j, held: held, piece: 1));
    }

    public void ClearBlockedLanes()
    {
        for (int i = 0; i < LanesBlocked.Count; i++)
        {
            LanesBlocked[i] = false;
        }
    }

    public bool InputLaneHeld(int lane) =>
        InputLaneState(lane, 1);

    public bool InputLanePress(int lane) =>
        InputLaneState(lane, 0);

    public bool InputLaneRelease(int lane) =>
        InputLaneState(lane, 2);

    public void ResolveNote(
        NoteInstance note,
        JudgementMS.Judge judge,
        double delta = double.PositiveInfinity)
    {
        note.ResolvedAs = judge;
        AddJudge(note.Column,judge, delta);
    }

    public void SetNotes(IReadOnlyList<NoteInstance> n)
    {
        notes = n;
        VSLanes = InitLanes(n);
    }

    public void UpdateLaneStates()
    {
        for (int i = 0; i < 4; i++)
        {
            _laneInputStates[i][0] =
                ldvsGame.Input.Keyboard.WasKeyJustPressed(
                    _laneInputKeys[i]);

            _laneInputStates[i][1] =
                ldvsGame.Input.Keyboard.IsKeyDown(
                    _laneInputKeys[i]);

            _laneInputStates[i][2] =
                ldvsGame.Input.Keyboard.WasKeyJustReleased(
                    _laneInputKeys[i]);
        }
    }

    void AddJudge(int lane,JudgementMS.Judge judge, double delta)
    {
        JudgeCount[(int)judge]++;
        mostRecentJudge = judge;

        JudgeInstances.Push(new DisplayJudgeInstance(judge,lane));

        if (!double.IsPositiveInfinity(delta))
            mostRecentDelta = delta;
    }

    private List<List<NoteInstance>> InitLanes(
        IReadOnlyList<NoteInstance> NewNotes)
    {
        List<List<NoteInstance>> lanes =
            [[], [], [], [], [], [], []];

        foreach (NoteInstance note in NewNotes)
        {
            if (note.Column < 0 || note.Column >= lanes.Count)
                throw new ArgumentOutOfRangeException(
                    nameof(note.Column));

            lanes[note.Column].Add(note);
        }

        return lanes;
    }

    private bool InputLaneState(int lane, int stateIndex)
    {
        if (lane < 0 || lane > 6)
            throw new ArgumentOutOfRangeException(nameof(lane));

        if (stateIndex < 0 || stateIndex > 2)
            throw new ArgumentOutOfRangeException(nameof(stateIndex));

        if (lane < 4)
            return _laneInputStates[lane][stateIndex];

        return _laneInputStates[lane - 4][stateIndex] ||
               _laneInputStates[lane - 3][stateIndex];
    }
}

public class NoteInstance
{
    public Sprite sprite;
    public Sprite LNsprite;
    public int Column;
    public double HitMs;
    public double? EndMs;
    public JudgementMS.Judge? ResolvedAs { get; set; }
    public bool isHeldVisual;
    public bool holdConsumed;
}

public class BumperInstance : NoteInstance
{
    public int Type;
    public Sprite spriteL;
    public Sprite spriteM;
    public Sprite spriteR;
    public Sprite LNspriteL;
    public Sprite LNspriteM;
    public Sprite LNspriteR;
    public Sprite TspriteL;
    public Sprite TspriteM;
    public Sprite TspriteR;
}

public class MineInstance : NoteInstance {}

public class Playfield : Scene, IPlayfield
{
    private Conductor _c;
    private TimingPointProvider _tp;

    private NoteDrawer _nd;
    private NoteUpdater _nu;
    private NoteHandler _nh;

    //public SoundEffect hitsound = ldvsGame.Content.Load<SoundEffect>("SFX/normal-hitnormal"); // test hitsound
    Texture2D _noteLaneTexL; // stupid replace later plssss
    Texture2D _noteLaneTexR;
    Texture2D _noteLaneTexJ;
    Sprite NoteLane1;
    Sprite NoteLane2;
    Sprite NoteLane3;
    Sprite NoteLane4;
    Sprite NoteLaneJ;
    SpriteFont font;

    readonly BeatmapSet _set;

    SimpleFpsCounter.SimpleFps fpsTester = new SimpleFpsCounter.SimpleFps();

    public Playfield(BeatmapSet set, Beatmap map)
    {
        _set = set;
        _map = map;

        _tp = new TimingPointProvider(_map.UninheritedTimingPoints, _map.InheritedTimingPoints);
        _c = new Conductor();
        _nh = new NoteHandler([Keys.X,Keys.C,Keys.M,Keys.OemComma]);

        _nu = new NoteUpdater(_c,_tp,_nh);
        _nd = new NoteDrawer(_tp, _c,_nh);
    }

    public Beatmap _map { get; set; }

    public override void Draw(GameTime gameTime)
    {

        ldvsGame.GraphicsDevice.Clear(Color.Black);
        ldvsGame.SpriteBatch.Begin();
        fpsTester.DrawFps(ldvsGame.SpriteBatch, font, new Vector2(10f, 10f), Color.MonoGameOrange);

        NoteLane1.Draw(new Vector2(_nd.LaneX(0), 0f));
        NoteLane2.Draw(new Vector2(_nd.LaneX(1), 0f));
        NoteLane3.Draw(new Vector2(_nd.LaneX(2), 0f));
        NoteLane4.Draw(new Vector2(_nd.LaneX(3), 0f));
        NoteLaneJ.Draw(new Vector2(_nd.LaneX(0) - 100f, IPlayfield.receptorY)); // pain
        _nd.Draw(font);
        ldvsGame.SpriteBatch.End();
        base.Draw(gameTime);
    }

    public override void Initialize()
    {
        base.Initialize();
        _c.Start(_set, _map);
        BuildNotes(_map);
    }

    public override void LoadContent()
    {
        base.LoadContent();

        _noteLaneTexL = Content.Load<Texture2D>("Sprites/Playfield/NoteLanes/sp_noteLane_3");
        _noteLaneTexR = Content.Load<Texture2D>("Sprites/Playfield/NoteLanes/sp_noteLane_2");
        _noteLaneTexJ = Content.Load<Texture2D>("Sprites/Playfield/sp_noteJudgementLine");

        NoteLane1 = new Sprite(_noteLaneTexL, Color.White, scale: new Vector2(7, 50));
        NoteLane2 = new Sprite(_noteLaneTexL, Color.White, scale: new Vector2(7, 50));
        NoteLane3 = new Sprite(_noteLaneTexR, Color.White, scale: new Vector2(7, 50));
        NoteLane4 = new Sprite(_noteLaneTexR, Color.White, scale: new Vector2(7, 50));
        NoteLaneJ = new Sprite(_noteLaneTexJ, Color.White, scale: new Vector2(7, 7));
        font = Content.Load<SpriteFont>("Fonts/Hud");
    }

    public override void Update(GameTime gameTime)
    {
        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Escape))
        {
            _c.Stop();
            ldvsGame.ChangeScene(new SongSelectScreen());

            return;
        }
        fpsTester.Update(gameTime);
        _c.Update();
        _nh.UpdateLaneStates();

        _nu.Update(IPlayfield.autoplay);
        base.Update(gameTime);
    }

    private void BuildNotes(Beatmap beatmap)
{
    var notes = new List<NoteInstance>();

    var noteS = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_NoteNew_6"), scale: 10f);
    var noteSLN = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_NoteNew_7"), scale: 10f);
    var mine = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_chip_mine_normal_0"), scale: 8f);
    var bumperL = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_0"), scale: 8f,layerDepth:1);
    var bumperM = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_1"), scale: 8,layerDepth:1);
    var bumperR = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_2"), scale: 8f, layerDepth: 1);
    var bumperLLN = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_ln0"), scale: 8f, layerDepth: 1);
    var bumperMLN = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_ln1"), scale: 8f, layerDepth: 1);
    var bumperRLN = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_ln2"), scale: 8f, layerDepth: 1);
    var bumperLT = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_time0"), scale: 8f, layerDepth: 1);
    var bumperMT = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_time1"), scale: 8f, layerDepth: 1);
    var bumperRT = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_time2"), scale: 8f, layerDepth: 1);

    foreach (var hitObject in beatmap.HitObjects)
    {
        NoteInstance startNote;

        if (hitObject.Type == 2)
        {
            startNote = new MineInstance
            {
                sprite = mine,
                LNsprite = mine,
                Column = hitObject.Lane,
                HitMs = hitObject.Time,
                ResolvedAs = null
            };
        }
        else if (hitObject.Lane > 3)
        {
            startNote = new BumperInstance
            {
                spriteL = bumperL,
                spriteM = bumperM,
                spriteR = bumperR,

                LNspriteL = bumperLLN,
                LNspriteM = bumperMLN,
                LNspriteR = bumperRLN,

                TspriteL = bumperLT,
                TspriteM = bumperMT,
                TspriteR = bumperRT,

                Type = hitObject.Type,
                Column = hitObject.Lane,
                HitMs = hitObject.Time,
                ResolvedAs = null
            };
        }
        else
        {
            startNote = new NoteInstance
            {
                sprite = noteS,
                LNsprite = noteSLN,
                Column = hitObject.Lane,
                HitMs = hitObject.Time,
                ResolvedAs = null
            };
        }

        if (hitObject.EndTime != null)
        {
            startNote.EndMs = hitObject.EndTime;
        }

        notes.Add(startNote);
    }

    notes.Sort((a, b) => a.HitMs.CompareTo(b.HitMs));

    _nh.SetNotes(notes);
}
}
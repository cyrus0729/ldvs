using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.Swift;
using ldvs.Core;
using ldvs.Core.Content.Entities;
using Microsoft.Xna.Framework.Audio;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Scenes;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

public static class JudgementRangeExtensions
{
    public static Playfield.JudgementMS.Judgement window(this Playfield.JudgementMS.Judgement[] judgements, Playfield.JudgementMS.Judge judge)
    {
        if (judgements.Length == 0)
            throw new InvalidOperationException("The judgement list is empty.");

        return judgements.First(n => n.Name == judge);
    }

    public static Playfield.JudgementMS.Judgement maxRange(this Playfield.JudgementMS.Judgement[] judgements)
    {
        if (judgements.Length == 0)
            throw new InvalidOperationException("The judgement list is empty.");

        Playfield.JudgementMS.Judgement widest = judgements[0];
        int widestSize = widest.Max - widest.Min;

        foreach (Playfield.JudgementMS.Judgement judgement in judgements)
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
}

public class Playfield(BeatmapSet set, Beatmap map) : Scene
{
    public Conductor Conductor;
    
    public static class JudgementMS
    {
        public record Judgement(Judge Name, int Min, int Max);
        
        public enum Judge
        {
            Marvelous,
            Perfect,
            Great,
            Good,
            Miss
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

        public static Judge? HandleJudgement(Judgement[] judges, double t, double hit)
        {
            double delta = (t - hit);
            foreach (var window in judges)
            {
                if (window.Min <= delta && delta <= window.Max)
                {
                    return window.Name;
;                }
            }
            return null;
        }
    }

    public double travelTimeMs = 450;

    //public SoundEffect hitsound = ldvsGame.Content.Load<SoundEffect>("SFX/normal-hitnormal"); // test hitsound

    public class ActiveTiming
    {
        public double beatLength = 500.0;
        public double svMultiplier = 1.0;
        public double bpm
        {
            get => 60000.0 / beatLength;
            set => beatLength = 60000.0 / value;
        }
    };

    private float receptorY = 1080f;
    private float spawnY = 0f;
    private float centerX = 1000f;
    private float columnWidth = 170f;

    bool autoplay = false; // PLEASE DISABLE THIS LATER

    private readonly List<NoteInstance> _notes = new();
    private readonly List<TimingPoint> _timingPoints = new();

    static List<Keys> laneInputKeys;
    static List<List<bool>> laneInputStates = [[false,false,false], [false, false, false], [false, false, false], [false, false, false]];
    public List<double> last_held = [-Double.NegativeInfinity, -Double.NegativeInfinity, -Double.NegativeInfinity, -Double.NegativeInfinity, -Double.NegativeInfinity, -Double.NegativeInfinity, -Double.NegativeInfinity];
    public List<bool> lanes_blocked = [false, false, false, false, false, false, false];
    public List<bool> lanes_held = [false, false, false, false, false, false, false];
    public List<List<NoteInstance>> VSLanes = [[], [], [], [], [], [], []];
    public List<int> VSLanes_next = [0,0,0,0,0,0,0];
    public static List<NoteInstance?> next_notes = [];
    public NoteHandler PlayfieldNoteHandler = new();
    public List<List<HeldNoteInstance>> hold_lanes = [[], [], [], [], [], [], []];

    Texture2D _noteLaneTexL; // stupid replace later plssss
    Texture2D _noteLaneTexR;
    Texture2D _noteLaneTexJ;
    Sprite NoteLane1;
    Sprite NoteLane2;
    Sprite NoteLane3;
    Sprite NoteLane4;
    Sprite NoteLaneJ;

    public class NoteInstance
    {
        public Sprite sprite;
        public Sprite LNsprite;
        public int Column;
        public double HitMs;
        public double? EndMs;
        public JudgementMS.Judge? ResolvedAs { get; set; }
    }

    public class BumperInstance : NoteInstance
    {
        public int Type;
        public Sprite spriteL = new(ldvsGame.Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_0"));
        public Sprite spriteM = new(ldvsGame.Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_1"));
        public Sprite spriteR = new(ldvsGame.Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_2"));
        public Sprite LNspriteL = new(ldvsGame.Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_ln0"));
        public Sprite LNspriteM = new(ldvsGame.Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_ln1"));
        public Sprite LNspriteR = new(ldvsGame.Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_ln2"));
    }

    public class MineInstance: NoteInstance {}

    public override void Initialize()
    {
        base.Initialize();

        Conductor = new Conductor();
        Conductor.Start(set, map);

        BuildNotes(map);
        VSLanes = PlayfieldNoteHandler.initLanes(_notes);

        laneInputKeys = [Keys.X, Keys.C, Keys.M, Keys.OemComma];

        foreach (TimingPoint t in map.TimingPoints)
        {
            _timingPoints.Add(t);
        }

    }

    private void BuildNotes(Beatmap map88888uoikhikj)
    {
        _notes.Clear();

        var noteS = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_NoteNew_6"), scale: 10f);
        var noteSLN = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_NoteNew_7"), scale: 10f);
        var mine = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_chip_mine_normal_0"), scale: 8f);
        var bumperL = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_0"), scale: 8f);
        var bumperM = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_1"), scale: 8f);
        var bumperR = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_2"), scale: 8f);
        var bumperLLN = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_ln0"), scale: 8f);
        var bumperMLN = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_ln1"), scale: 8f);
        var bumperRLN = new Sprite(Content.Load<Texture2D>("Sprites/Playfield/Notes/sp_note_bumper_timing_stargazers_ln2"), scale: 8f);

        foreach (var o in map88888uoikhikj.HitObjects)
        {
            bool isLong = o.EndTime is not null;

            NoteInstance startNote;

            if (o.Type == 2)
            {
                startNote = new MineInstance
                {
                    sprite = mine,
                    LNsprite = mine,
                    Column = o.Column,
                    HitMs = o.Time,
                    ResolvedAs = null
                };
            }
            else if (o.Column > 3)
            {
                startNote = new BumperInstance
                {
                    spriteL = bumperL,
                    spriteM = bumperM,
                    spriteR = bumperR,
                    LNspriteL = bumperLLN,
                    LNspriteM = bumperMLN,
                    LNspriteR = bumperRLN,
                    Type = o.Type,
                    Column = o.Column,
                    HitMs = o.Time,
                    ResolvedAs = null
                };
            }
            else
            {
                startNote = new NoteInstance
                {
                    sprite = noteS,
                    LNsprite = noteSLN,
                    Column = o.Column,
                    HitMs = o.Time,
                    ResolvedAs = null
                };
            }

            if (isLong)
            {
                startNote.EndMs = (double)o.EndTime;
            }

            _notes.Add(startNote);
        }

        _notes.Sort((a, b) => a.HitMs.CompareTo(b.HitMs));
    }

    public override void LoadContent()
    {
        base.LoadContent();

        _noteLaneTexL = Content.Load<Texture2D>("Sprites/Playfield/NoteLanes/sp_noteLane_3");
        _noteLaneTexR = Content.Load<Texture2D>("Sprites/Playfield/NoteLanes/sp_noteLane_2");
        _noteLaneTexJ = Content.Load<Texture2D>("Sprites/Playfield/sp_noteJudgementLine");

        NoteLane1 = new Sprite(_noteLaneTexL, Color.White);
        NoteLane2 = new Sprite(_noteLaneTexL, Color.White);
        NoteLane3 = new Sprite(_noteLaneTexR, Color.White);
        NoteLane4 = new Sprite(_noteLaneTexR, Color.White);
        NoteLaneJ = new Sprite(_noteLaneTexJ, Color.White);

        NoteLane1.Scale = NoteLane2.Scale = NoteLane3.Scale = NoteLane4.Scale = 7f;
        NoteLaneJ.Scale = 7f;
    }

    public class NoteHandler
    {
        public int JudgeMarvelousCount;
        public int JudgePerfectCount;
        public int JudgeGreatCount;
        public int JudgeGoodCount;
        public int JudgeMissCount;
        public JudgementMS.Judge? mostRecentJudge = null;

        public void updateLaneStates()
        {
            for (var i = 0; i < 4; i++)
            {
                laneInputStates[i][0] = ldvsGame.Input.Keyboard.WasKeyJustPressed(laneInputKeys[i]);
                laneInputStates[i][1] = ldvsGame.Input.Keyboard.IsKeyDown(laneInputKeys[i]);
                laneInputStates[i][2] = ldvsGame.Input.Keyboard.WasKeyJustReleased(laneInputKeys[i]);
            }
        }

        public List<List<NoteInstance>> initLanes(List<NoteInstance> notes)
        {
            List<List<NoteInstance>> lanes = [[], [], [], [], [], [], []];

            foreach (var n in notes)
                lanes[n.Column].Add(n);

            return lanes;
        }

        public bool input_lane_state(int lane, int stateIndex)
        {
            if (lane < 0 || lane > 6)
                throw new ArgumentOutOfRangeException();

            if (lane < 4)
                return laneInputStates[lane][stateIndex];

            int firstLane = lane - 4;

            return laneInputStates[firstLane][stateIndex] ||
                   laneInputStates[firstLane + 1][stateIndex];
        }

        public bool input_lane_press(int lane) => input_lane_state(lane, 0);
        public bool input_lane_held(int lane) => input_lane_state(lane, 1);
        public bool input_lane_release(int lane) => input_lane_state(lane, 2);

        public void resolveNote(NoteInstance note, JudgementMS.Judge judge)
        {
            note.ResolvedAs = judge;
            addJudge(judge);
        }

        public void addJudge(JudgementMS.Judge judge)
        {
            switch (judge)
            {
                case JudgementMS.Judge.Marvelous: JudgeMarvelousCount++; break;
                case JudgementMS.Judge.Perfect: JudgePerfectCount++; break;
                case JudgementMS.Judge.Great: JudgeGreatCount++; break;
                case JudgementMS.Judge.Good: JudgeGoodCount++; break;
                case JudgementMS.Judge.Miss: JudgeMissCount++; break;
            }
            mostRecentJudge = judge;
        }

    }

    public class HeldNoteInstance : NoteInstance
    {
        public int lane;
        public bool held;
        public int piece;
    }
    public void addHold(int lane, NoteInstance note, bool held)
    {
        if (!held)
        {
            PlayfieldNoteHandler.resolveNote(note, JudgementMS.Judge.Marvelous);
        }
        hold_lanes[lane].Add(new HeldNoteInstance() { lane = lane, held = held, piece = 1 });
    }

    bool IsResolved(NoteInstance n)
        => n.ResolvedAs != null;

    public static ActiveTiming GetTimingAt(double time,
                                           List<TimingPoint> timingPoints)
    {
        double beatLength = 500.0;
        double svMultiplier = 1.0;

        foreach (TimingPoint point in timingPoints)
        {
            if (point.offset > time)
                break;

            if (point.Uninherited)
            {
                // +beatLength =  PM.
                beatLength = point.beatLen;

                // red = inherited SV to 1
                svMultiplier = 1.0;
            }
            else if (point.beatLen < 0.0)
            {
                // -beatLength = inherited SV
                svMultiplier = -100.0 / point.beatLen;
            }
        }

        return new ActiveTiming()
        {
            beatLength = beatLength,
            bpm = 60000.0 / beatLength,
            svMultiplier = svMultiplier
        };
    }

    public double getBeatCount(double BPM, double ms)
    {
        return ms * (double)BPM / 60000;
    }

    public override void Update(GameTime gameTime)
    {
        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Escape))
        {
            Conductor.Stop();
            ldvsGame.ChangeScene(new SongSelectScreen());
            return;
        }

        Conductor.Update();
        double currentTime = Conductor.SongPositionMs;

        // ---- update inputs ----
        PlayfieldNoteHandler.updateLaneStates();

        // =========================
        // reset blocked lanes
        // =========================
        for (int lane = 0; lane < lanes_blocked.Count; lane++)
            lanes_blocked[lane] = false;

        // =========================
        // update hold times
        // =========================
        for (int lane = 0; lane < 7; lane++)
        {
            if (lane < 4)
            {
                if (PlayfieldNoteHandler.input_lane_held(lane))
                    last_held[lane] = currentTime;
            }
            else
            {
                last_held[lane] = Math.Max(last_held[lane - 4], last_held[lane - 3]);
            }
        }

        // =========================
        // gm line 23
        // expired note's judgement?
        // =========================
        for (int lane = 0; lane < 7; lane++)
        {
            while (VSLanes_next[lane] < VSLanes[lane].Count)
            {
                NoteInstance note = VSLanes[lane][VSLanes_next[lane]];

                if (IsResolved(note)) { continue; }

                int missTiming;
                JudgementMS.Judge judgement;

                if (note is MineInstance) // i dont fucking know about this naymore
                {
                    // case 3
                    missTiming = !autoplay
                                     ? JudgementMS.LN.window(JudgementMS.Judge.Miss).Min
                                     : 0;

                    judgement = !autoplay
                                    ? JudgementMS.Judge.Marvelous
                                    : JudgementMS.Judge.Miss;
                }
                else if (note.EndMs != null)
                {
                    if (last_held[lane] < (note.HitMs - JudgementMS.Normal.window(JudgementMS.Judge.Marvelous).Max))
                    {
                        missTiming = -JudgementMS.Normal.window(JudgementMS.Judge.Marvelous).Max;
                        judgement = JudgementMS.Judge.Marvelous;
                    }
                    else
                    {
                        missTiming = int.MaxValue;
                        judgement = JudgementMS.Judge.Miss;
                    }
                }
                else
                {
                    // normal notes
                    missTiming = !autoplay
                                     ? JudgementMS.Normal.window(JudgementMS.Judge.Miss).Min
                                     : 0;

                    judgement = !autoplay
                                    ? JudgementMS.Judge.Miss
                                    : JudgementMS.Judge.Marvelous;
                }

                if (note.HitMs - currentTime > missTiming) // too early, don count dude
                    break;

                // line 61
                if (!autoplay)
                {
                    PlayfieldNoteHandler.resolveNote(note, judgement);
                    VSLanes_next[lane]++;
                }
                else
                {
                    if (note is MineInstance)
                    {
                        PlayfieldNoteHandler.resolveNote(note, judgement);
                        VSLanes_next[lane]++; // om nom nom nom
                    }
                }
                if (note is not BumperInstance && note is not MineInstance && note.EndMs != null)
                {
                    addHold(note.Column, note, autoplay);
                }
            }

            // =========================
            // lane blocking!! yayyy
            // =========================

            if (lane > 4)
            {
                int leftLane = lane - 4;
                int rightLane = lane - 3;

                lanes_blocked[lane - 4] = lanes_blocked[lane - 4] ||
                                          (VSLanes_next[lane] < VSLanes[lane].Count &&
                                           (VSLanes_next[leftLane] >= VSLanes[leftLane].Count || VSLanes[lane][VSLanes_next[lane]].HitMs <= VSLanes[leftLane][VSLanes_next[leftLane]].HitMs));
                lanes_blocked[lane - 3] = lanes_blocked[lane - 3] ||
                                          (VSLanes_next[lane] < VSLanes[lane].Count &&
                                           (VSLanes_next[rightLane] >= VSLanes[rightLane].Count ||
                                            VSLanes[lane][VSLanes_next[lane]].HitMs <= VSLanes[rightLane][VSLanes_next[rightLane]].HitMs));
                lanes_blocked[lane] = VSLanes_next[lane] >= VSLanes[lane].Count ||
                                      (VSLanes_next[lane] >= VSLanes[lane].Count && VSLanes[lane][VSLanes_next[lane]].HitMs > VSLanes[leftLane][VSLanes_next[leftLane]].HitMs) ||
                                      (VSLanes_next[rightLane] < VSLanes[rightLane].Count && VSLanes[lane][VSLanes_next[lane]].HitMs >
                                       VSLanes[rightLane][VSLanes_next[rightLane]].HitMs);
            }

        }

        // =========================
        // gm line 135
        // process input!! yayyy....
        // =========================
        for (int lane = 0; lane < 7; lane++)
        {
            if (lanes_blocked[lane] || VSLanes_next[lane] >= VSLanes[lane].Count) { continue; }
            NoteInstance note = VSLanes[lane][VSLanes_next[lane]];

            if (note is MineInstance)
                continue;

            if (!PlayfieldNoteHandler.input_lane_press(lane))
                continue;

            JudgementMS.Judgement[] noteType;

            // what even is case 0 for this...
            if (note is BumperInstance b && b.Type != 1)
            {
                noteType = JudgementMS.Bumper;
            }
            else if (note.EndMs is not null)
            {
                noteType = JudgementMS.LN;
            }
            else
            {
                noteType = JudgementMS.Normal;
            }

            var result = JudgementMS.HandleJudgement(
                noteType,
                currentTime,
                note.HitMs);

            if (result is null)
                break;

            if (note.EndMs is not null)
            {
                addHold(lane, note, true);
            }

            Console.WriteLine(
                $"hit {note.Column} type {note.GetType()} hitms {note.HitMs} with judge {result.Value}");

            var judgement = result.Value;
            PlayfieldNoteHandler.resolveNote(note, judgement);
            note.ResolvedAs = judgement;

            VSLanes_next[lane]++;
        }

        for (int i = 0; i < 4; i++)
        {
            var last = 0;
            var j = 0;
            var pressed = autoplay || PlayfieldNoteHandler.input_lane_held(i);

            if (hold_lanes[i].Count <= 0 ) { continue; }
            for (var _=0;_<(hold_lanes[i].Count);_++)
            {
                var hold = hold_lanes[i][j];
                var note = VSLanes[i][hold.lane]; // idont know about this dude

                if (note.EndMs == null) {continue;}

                ActiveTiming state = GetTimingAt(currentTime, _timingPoints);
                var msdelay = 60000 / state.bpm;
                var parts = Math.Floor(Math.Max(0.0,(double)(note.EndMs - note.HitMs - 150)) / msdelay);

                if (pressed)
                {
                    while (hold.piece <= parts && (note.HitMs + (hold.piece * state.beatLength)) <= 0)
                    {
                        PlayfieldNoteHandler.resolveNote(note, JudgementMS.Judge.Marvelous);
                        hold.piece++;
                    }
                }
                else
                {
                    while (hold.piece <= parts && (note.HitMs + (hold.piece * state.beatLength)) <= JudgementMS.Normal.window(JudgementMS.Judge.Miss).Min)
                    {
                        PlayfieldNoteHandler.resolveNote(note, JudgementMS.Judge.Miss);
                        hold.piece++;
                    }
                }

                if (hold.held && (!pressed || note.EndMs <= 0))
                {
                    hold.held = false;

                    if (note.EndMs <= JudgementMS.Normal.window(JudgementMS.Judge.Good).Max)
                    {
                        PlayfieldNoteHandler.resolveNote(note, JudgementMS.Judge.Marvelous);
                    }
                    else
                    {
                        PlayfieldNoteHandler.resolveNote(note, JudgementMS.Judge.Good);
                    }
                }

                if (note.EndMs > 0 || hold.piece <= parts)
                {
                    hold_lanes[i][last] = hold_lanes[i][j];
                    last++;
                }
                j++;

            }
        }


        base.Update(gameTime);
    }

    private float LaneX(int col)
        => centerX + (col - 2f) * columnWidth;

    private float NoteY(double noteTime, double currentTime)
    {
        double spawnTimeMs = noteTime - travelTimeMs;

        double totalDistance = GetScrollDistance(
            spawnTimeMs,
            noteTime,
            _timingPoints,
            receptorY);

        double remainingDistance = GetScrollDistance(
            currentTime,
            noteTime,
            _timingPoints,
            receptorY);

        double progress;
        progress = 1.0 - remainingDistance / totalDistance;
        return MathHelper.Lerp(
            spawnY-50f,
            receptorY,
            (float)progress);

    }

    public static float GetScrollDistance(double fromTime, double toTime, List<TimingPoint> timingPoints, double pixelsPerBeat)
    {
        if (toTime <= fromTime)
            return 0;

        double distance = 0.0;
        double currentTime = fromTime;

        while (currentTime < toTime)
        {
            ActiveTiming state =
                GetTimingAt(currentTime, timingPoints);

            double nextTime = toTime;

            foreach (TimingPoint point in timingPoints)
            {
                if (point.offset > currentTime)
                {
                    nextTime = Math.Min(nextTime, point.offset);

                    break;
                }
            }

            double duration = nextTime - currentTime;

            double pixelsPerMillisecond =
                pixelsPerBeat / state.beatLength * state.svMultiplier;

            distance += duration * pixelsPerMillisecond;
            currentTime = nextTime;
        }

        return (float)distance;
    }

    private void SetNoteColor(NoteInstance note, Color color)
    {
        if (note is not BumperInstance bumper)
        {
            note.sprite.Color = color;
            note.LNsprite.Color = color;

            return;
        }

        bumper.spriteL.Color = color;
        bumper.spriteM.Color = color;
        bumper.spriteR.Color = color;

        bumper.LNspriteL.Color = color;
        bumper.LNspriteM.Color = color;
        bumper.LNspriteR.Color = color;
    }

    private float GetDrawLaneX(NoteInstance note)
    {
        if (note is BumperInstance bumper)
            return LaneX(bumper.Column - 4);

        return LaneX(note.Column);
    }

    private Sprite GetRegularSprite(NoteInstance note)
    {
        if (note is not BumperInstance bumper)
            return note.sprite;

        return bumper.Column switch
        {
            4 => bumper.spriteL, 5 => bumper.spriteM, 6 => bumper.spriteR, _ => null
        };
    }

    private Sprite GetLNSprite(NoteInstance note)
    {
        if (note is not BumperInstance bumper)
            return note.LNsprite;

        return bumper.Column switch
        {
            4 => bumper.LNspriteL, 5 => bumper.LNspriteM, 6 => bumper.LNspriteR, _ => null
        };
    }

    private void DrawRegular(NoteInstance note,
                             double currentTime,
                             double firstVisibleTime,
                             double lastVisibleTime,
                             Color color)
    {
        if (note.HitMs < firstVisibleTime ||
            note.HitMs > lastVisibleTime)
        {
            return;
        }

        SetNoteColor(note, color);

        var sprite = GetRegularSprite(note);

        if (sprite == null)
            return;

        float width = GetSpriteWidth(note);
        float height = GetSpriteHeight(note);

        float x = GetDrawLaneX(note);
        float y = NoteY(note.HitMs, currentTime) - height / 2f;

        var destination = new Rectangle(
            (int)x,
            (int)y,
            (int)width,
            (int)height);

        sprite.Draw(destination);
    }

    private void DrawLNHead(NoteInstance note,
                            double currentTime,
                            double firstVisibleTime,
                            double lastVisibleTime,
                            Color color)
    {
        if (note.HitMs < firstVisibleTime ||
            note.HitMs > lastVisibleTime)
        {
            return;
        }

        SetNoteColor(note, color);

        var sprite = GetRegularSprite(note);

        if (sprite == null)
            return;

        float width = GetSpriteWidth(note);
        float height = GetSpriteHeight(note);

        float x = GetDrawLaneX(note);
        float y = NoteY(note.HitMs, currentTime) - height / 2f;

        var destination = new Rectangle(
            (int)x,
            (int)y,
            (int)width,
            (int)height);

        sprite.Draw(destination);
    }


    private void DrawLNBody(NoteInstance note,
                            double currentTime,
                            double firstVisibleTime,
                            double lastVisibleTime,
                            Color color)
    {
        if (note.EndMs == null)
            return;

        double startTime = Math.Max(note.HitMs, firstVisibleTime);
        double endTime = Math.Min((double)note.EndMs, lastVisibleTime);

        if (startTime > endTime)
            return;

        SetNoteColor(note, color);

        var sprite = GetLNSprite(note);

        if (sprite == null)
            return;

        float startY = NoteY(startTime, currentTime);
        float endY = NoteY(endTime, currentTime);

        float top = Math.Min(startY, endY);
        float height = Math.Max(1f, Math.Abs(endY - startY));

        float x = GetDrawLaneX(note);
        float width = GetSpriteWidth(note);

        var destination = new Rectangle(
            (int)x,
            (int)top,
            (int)width,
            (int)height);

        sprite.Draw(destination);
    }


    private void DrawLNTail(NoteInstance note,
                            double currentTime,
                            double firstVisibleTime,
                            double lastVisibleTime,
                            Color color)
    {
        double tailTime = note.HitMs;

        if (tailTime < firstVisibleTime ||
            tailTime > lastVisibleTime)
        {
            return;
        }

        SetNoteColor(note, color);

        var sprite = GetRegularSprite(note);

        if (sprite == null)
            return;

        float width = GetSpriteWidth(note);
        float height = GetSpriteHeight(note);

        float x = GetDrawLaneX(note);
        float y = NoteY(tailTime, currentTime) - height / 2f;

        var destination = new Rectangle(
            (int)x,
            (int)y,
            (int)width,
            (int)height);

        sprite.Draw(destination);
    }


    private float GetSpriteHeight(NoteInstance note)
    {
        if (note is BumperInstance b)
        {
            return b.spriteM.Height * b.spriteM.Scale;
        }

        return note.sprite.Height * note.sprite.Scale;
    }

    private float GetSpriteWidth(NoteInstance note)
    {
        if (note is BumperInstance b)
        {
            return b.spriteM.Width * b.spriteM.Scale;
        }

        return note.sprite.Width * note.sprite.Scale;
    }

    private void DrawNotes()
    {
        double currentTime = Conductor.SongPositionMs;

        const double visibleDurationMs = 1000.0;
        const double missLeadOffMs = 300.0;

        double firstVisibleTime = currentTime - missLeadOffMs;
        double lastVisibleTime = currentTime + visibleDurationMs;

        foreach (NoteInstance note in _notes)
        {
            if (note.HitMs > lastVisibleTime)
                break;

            bool missed = note.ResolvedAs == JudgementMS.Judge.Miss;
            bool LNFail = note.ResolvedAs == JudgementMS.Judge.Good && note.EndMs != null;
            bool resolved = IsResolved(note);

            Color color = (missed || LNFail) ? Color.DarkBlue : Color.White;

            if (resolved && !missed && !LNFail)
                continue;

            if (note.EndMs != null)
            {
                DrawLNHead(
                    note,
                    currentTime,
                    firstVisibleTime,
                    lastVisibleTime,
                    color);

                DrawLNBody(
                    note,
                    currentTime,
                    firstVisibleTime,
                    lastVisibleTime,
                    color);
                DrawLNTail(
                    note,
                    currentTime,
                    firstVisibleTime,
                    lastVisibleTime,
                    color);
            }
            else
            {
                DrawRegular(
                    note,
                    currentTime,
                    firstVisibleTime,
                    lastVisibleTime,
                    color);
            }
        }
    }

    public override void Draw(GameTime _)
    {
        ldvsGame.GraphicsDevice.Clear(Color.Black);
        ldvsGame.SpriteBatch.Begin();

        double t = Conductor.SongPositionMs;

        var f = Content.Load<SpriteFont>("Fonts/Hud");
        ldvsGame.SpriteBatch.DrawString(f,t.ToString(CultureInfo.InvariantCulture),Vector2.One,Color.White); // THIS IS FOR DEBUGGING FFS

        for (var i = 0; i < 7; i++)
        {
            // make keys actually appear
            ldvsGame.SpriteBatch.DrawString(f, PlayfieldNoteHandler.input_lane_state(i,0).ToString(), new Vector2(LaneX(6)+i*50, 100f), Color.Red);
            ldvsGame.SpriteBatch.DrawString(f, PlayfieldNoteHandler.input_lane_state(i, 1).ToString(), new Vector2(LaneX(6) + i * 50, 200f), Color.Green);
        }

        ldvsGame.SpriteBatch.DrawString(f, PlayfieldNoteHandler.JudgeMarvelousCount.ToString(), new Vector2(10f, 200f), Color.LightGoldenrodYellow);
        ldvsGame.SpriteBatch.DrawString(f, PlayfieldNoteHandler.JudgePerfectCount.ToString(), new Vector2(10f, 220f), Color.Yellow);
        ldvsGame.SpriteBatch.DrawString(f, PlayfieldNoteHandler.JudgeGreatCount.ToString(), new Vector2(10f, 240f), Color.Green);
        ldvsGame.SpriteBatch.DrawString(f, PlayfieldNoteHandler.JudgeGoodCount.ToString(), new Vector2(10f, 260f), Color.Cyan);
        ldvsGame.SpriteBatch.DrawString(f, PlayfieldNoteHandler.JudgeMissCount.ToString(), new Vector2(10f, 280f), Color.Red);

        NoteLane1.Draw(new Vector2(LaneX(0),0f));
        NoteLane2.Draw(new Vector2(LaneX(1), 0f));
        NoteLane3.Draw(new Vector2(LaneX(2), 0f));
        NoteLane4.Draw(new Vector2(LaneX(3), 0f));
        NoteLaneJ.Draw(new Vector2(LaneX(0)-100f, receptorY)); // pain

        if (PlayfieldNoteHandler.mostRecentJudge != null)
        {
            ldvsGame.SpriteBatch.DrawString(f, PlayfieldNoteHandler.mostRecentJudge.ToString(),new Vector2(LaneX(5), 820f),Color.Red);
        }

        ldvsGame.SpriteBatch.DrawString(f, $"Current SVMULT {GetTimingAt(t,_timingPoints).svMultiplier}", new Vector2(LaneX(5), 820f), Color.Red);

        DrawNotes();

        ldvsGame.SpriteBatch.End();

        base.Draw(_);
    }
}
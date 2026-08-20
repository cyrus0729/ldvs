using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Scenes;

namespace ldvs.Core.Content.Entities;

public class SongSelectScreen: Scene
{

    public LinkedList<BeatmapSet> MapsetList;
    public LinkedList<Beatmap> MapList;
    LinkedListNode<BeatmapSet> currentSelectedMapset;
    LinkedListNode<Beatmap> currentSelectedMap;

    public int songsVisiblePastCurrent = 3;

    int songBaseSize = 1;

    public override void Initialize()
    {
        MapsetList = new SongParser().ParseSongsFolder();
        if (MapsetList.Count == 0) {
            return;
        }
        currentSelectedMapset = MapsetList.First;
        MapList = currentSelectedMapset.Value.maps;
        currentSelectedMap = MapList.First;
        base.Initialize();
    }

    int getMapInd(LinkedList<Beatmap> list, LinkedListNode<Beatmap> node)
    {
        int i = 0;
        for (var n = list.First; n != null; n = n.Next, i++)
        {
            if (ReferenceEquals(n, node))
                return i;
        }
        return 0; // fallback if unfound :3
    }

    LinkedListNode<Beatmap> getNode(LinkedList<Beatmap> list, int index)
    {
        if (list.First == null)
            return null;
        int i = 0;
        var n = list.First;
        for (; n != null && i < index; n = n.Next, i++) { }
        return n ?? list.Last;
    }


    public override void Update(GameTime gameTime)
    {
        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Escape))
        {
            ldvsGame.ChangeScene(new TitleScreen());
        }

        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Enter))
        {
            ldvsGame.ChangeScene(new Playfield(currentSelectedMapset.Value, currentSelectedMap.Value));
        }

        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Up))
        {
            int diffIndex = getMapInd(MapList, currentSelectedMap);

            currentSelectedMapset = currentSelectedMapset is { Previous: not null }
                                        ? currentSelectedMapset.Previous
                                        : MapsetList.Last;
            MapList = currentSelectedMapset.Value.maps;
            currentSelectedMap = getNode(MapList, diffIndex);
        }

        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Down))
        {
            int diffIndex = getMapInd(MapList, currentSelectedMap);

            currentSelectedMapset = currentSelectedMapset is { Next: not null }
                                        ? currentSelectedMapset.Next
                                        : MapsetList.Last;
            MapList = currentSelectedMapset.Value.maps;
            currentSelectedMap = getNode(MapList, diffIndex);
        }

        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Tab))
        {
            currentSelectedMap = currentSelectedMap is { Next: not null }
                                     ? currentSelectedMap.Next
                                     : MapList.First;
        }

        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        ldvsGame.GraphicsDevice.Clear(Color.LightPink);
        ldvsGame.SpriteBatch.Begin();

        var f = Content.Load<SpriteFont>("Fonts/Hud");

        var start = currentSelectedMapset;

        for (int i = 0; i < songsVisiblePastCurrent; i++)
        {
            if (start?.Previous == null) break;
            start = start.Previous;
        }

        int count = 0;
        int max = songsVisiblePastCurrent * 2 + 1;

        float yCenterTitle = 400f;
        float yCenterSource = 390f;
        float yCenterArtist = 430f;
        float yCenterDifficulty = 400f;
        float yCenterDiffName = 450f;

        float itemStep = 100f;

        for (LinkedListNode<BeatmapSet> n = start; n != null && count < max; n = n.Next)
        {
            float dist = count - songsVisiblePastCurrent; // 0 = selected row, +/-1, +/-2, ...
            float yOffset = dist * itemStep;

            bool isSelectedMapset = ReferenceEquals(n, currentSelectedMapset);

            // dist in rows (0..songsVisiblePastCurrent)
            float absDist = Math.Abs(dist);
            // - absDist = 0 => t = 1 (selected is biggest)
            // - absDist = songsVisiblePastCurrent => t = 0 (farthest is smallest)
            float t = 1f - absDist / songsVisiblePastCurrent;

            float minTitleScale = 1f;
            float maxTitleScale = 2f;

            float minOtherScale = 0.75f;
            float maxOtherScale = 1.5f;

            // dont go negative pls
            t = MathHelper.Clamp(t, 0f, 1f);

            float scaleTitle = MathHelper.Lerp(minTitleScale, maxTitleScale, t) * songBaseSize;
            float scaleOther = MathHelper.Lerp(minOtherScale, maxOtherScale, t) * songBaseSize;

            float yTitle  = yCenterTitle + yOffset;
            float ySource = yCenterSource + yOffset;
            float yArtist = yCenterArtist + yOffset;
            float yDifficulty = yCenterDifficulty + yOffset;
            float yDiffName = yCenterDiffName + yOffset;

            // --- always draw mapset info for all preview rows ---
            ldvsGame.SpriteBatch.DrawString(
                f,
                n.Value.Metadata.Title,
                new Vector2(300f, yTitle),
                Color.Black,
                0f,
                Vector2.One / 2,
                scaleTitle,
                SpriteEffects.None,
                0f);

            ldvsGame.SpriteBatch.DrawString(
                f,
                n.Value.Metadata.Source,
                new Vector2(300f, ySource),
                Color.Black,
                0f,
                Vector2.One / 2,
                scaleOther,
                SpriteEffects.None,
                0f);

            ldvsGame.SpriteBatch.DrawString(
                f,
                n.Value.Metadata.Artist,
                new Vector2(300f, yArtist),
                Color.Black,
                0f,
                Vector2.One / 2,
                scaleOther,
                SpriteEffects.None,
                0f);

            if (isSelectedMapset)
            {
                ldvsGame.SpriteBatch.DrawString(
                    f,
                    currentSelectedMap.Value.BMPMeta.Difficulty,
                    new Vector2(1000f, yDifficulty),
                    Color.Black,
                    0f,
                    Vector2.One / 2,
                    scaleTitle,
                    SpriteEffects.None,
                    0f);

                ldvsGame.SpriteBatch.DrawString(
                    f,
                    currentSelectedMap.Value.BMPMeta.DifficultyName,
                    new Vector2(1000f, yDiffName),
                    Color.Black,
                    0f,
                    Vector2.One / 2,
                    scaleTitle,
                    SpriteEffects.None,
                    0f);
            }

            count++;
        }

        ldvsGame.SpriteBatch.End();
        base.Draw(gameTime);
    }
}
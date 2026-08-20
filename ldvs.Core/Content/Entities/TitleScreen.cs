using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Scenes;

namespace ldvs.Core.Content.Entities;

public class TitleScreen : Scene
{
    public float FloatFactor;
    public List<MenuOption> Options;
    public IEnumerable IEOptions { get; }
    LinkedList<MenuOption> titlemenuoptions;
    LinkedListNode<MenuOption> titlemenuoption;

    // In TitleScreen:
    private Dictionary<MenuOption, float> currentScale = new();
    private float targetMultiplier = 1.25f;
    private float unselectedMultiplier = 1.0f;
    private float speed = 10f; // higher = faster


    Sprite start;
    Sprite edit;
    Sprite option;

    public enum TitleScreenState
    {
        Start,
        Loop,
        End,
    }

    public override void Initialize()
    {
        FloatFactor = 0f;

        var s = Content.Load<Texture2D>("Sprites/Menu/thing");
        start = new Sprite(s, Color.White, 5f, Vector2.One/2);
        edit = new Sprite(s, Color.White, 4f, Vector2.One / 2);
        option = new Sprite(s, Color.White, 3f, Vector2.One / 2);

        titlemenuoptions = new LinkedList<MenuOption>([
            new MenuOption(new SongSelectScreen(), start, new Vector2(300f, 400f)),
            new MenuOption(new TitleScreen(), edit, new Vector2(600f, 700f)),
            new MenuOption(new TitleScreen(), option, new Vector2(300f, 900f))
        ]);

        foreach (var opt in titlemenuoptions)
        {
            currentScale[opt] = opt.BaseScale;
        }


        titlemenuoption = titlemenuoptions.First;
        base.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Enter))
        {
            ldvsGame.ChangeScene(titlemenuoption.Value.sceneTo);
        }

        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Left))
        {
                titlemenuoption = titlemenuoption is { Previous: not null } ? titlemenuoption.Previous : titlemenuoptions.Last;
        }

        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Right))
        {
                titlemenuoption = titlemenuoption is { Next: not null } ? titlemenuoption.Next : titlemenuoptions.First;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        for (var node = titlemenuoptions.First; node != null; node = node.Next)
        {
            var opt = node.Value;

            float target = opt.BaseScale * (ReferenceEquals(node, titlemenuoption) ? targetMultiplier : unselectedMultiplier);
            currentScale[opt] = MathHelper.Lerp(currentScale[opt], target, 1f - (float)Math.Exp(-speed * dt));
        }

        FloatFactor = (float)Math.Sin(gameTime.TotalGameTime.TotalMilliseconds/1600)*4;
        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        var f = Content.Load<SpriteFont>("Fonts/Hud");

        ldvsGame.GraphicsDevice.Clear(Color.LightPink);
        ldvsGame.SpriteBatch.Begin();

        ldvsGame.SpriteBatch.DrawString(f, "this is the title screen", Vector2.Zero, Color.Black);

        for (var node = titlemenuoptions.First; node != null; node = node.Next)
        {
            var opt = node.Value;

            float prev = opt.sprite.Scale;
            opt.sprite.Scale = currentScale[opt];

            opt.Draw(ldvsGame.SpriteBatch, new Vector2(opt.position.X, opt.position.Y + FloatFactor));

            opt.sprite.Scale = prev;
        }

        ldvsGame.SpriteBatch.End();
        base.Draw(gameTime);
    }

}
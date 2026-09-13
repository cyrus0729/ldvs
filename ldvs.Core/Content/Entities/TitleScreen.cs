using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Scenes;

namespace ldvs.Core.Content.Entities;

public class MenuOption
{
    public string Name { get; }
    public Scene sceneTo { get; }
    public Sprite sprite { get; }
    public Vector2 position { get; }
    public Vector2 BaseScale { get; }

    public MenuOption(string name,
        Scene scene,
        Sprite sprite,
        Vector2 position)
    {
        Name = name;
        sceneTo = scene;
        this.sprite = sprite;
        this.position = position;
        BaseScale = sprite.Scale;
    }

    public void Draw(Vector2 pos)
    {
        sprite.Draw(pos);
    }
}


public class TitleScreen : Scene
{
    private readonly List<MenuOption> _menuOptions = new();
    private readonly Dictionary<MenuOption, Vector2> _currentScales = new();

    private SpriteFont _font = null!;

    private int _selectedIndex;
    private float _floatFactor;

    private const float SelectedScaleMultiplier = 1.25f;
    private const float UnselectedScaleMultiplier = 1.0f;
    private const float ScaleSpeed = 10f;

    public override void Draw(GameTime gameTime)
    {
        ldvsGame.GraphicsDevice.Clear(Color.DarkSlateBlue);

        ldvsGame.SpriteBatch.Begin();

        DrawMenuOptions();

        ldvsGame.SpriteBatch.End();

        base.Draw(gameTime);
    }

    public override void Initialize()
    {
        _font = Content.Load<SpriteFont>("Fonts/Hud");

        Texture2D menuTexture =
            Content.Load<Texture2D>("Sprites/Menu/thing");

        Sprite startSprite = new Sprite(menuTexture, Color.White, 5f, Vector2.One / 2f);

        Sprite editSprite = new Sprite(menuTexture, Color.White, 4f, Vector2.One / 2f);

        Sprite optionsSprite = new Sprite(menuTexture, Color.White, 4f, Vector2.One / 2f);

        _menuOptions.AddRange(
        [
            new MenuOption("PLAY", new SongSelectScreen(), startSprite, new Vector2(300f, 400f)),
            new MenuOption("EDIT", new TitleScreen(), editSprite, new Vector2(500f, 700f)),
            new MenuOption("CFGS", new TitleScreen(), optionsSprite, new Vector2(300f, 1000f))
        ]);

        foreach (MenuOption option in _menuOptions)
        {
            _currentScales[option] = option.BaseScale;
        }

        _selectedIndex = 0;
        base.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
        HandleInput();
        UpdateMenuScales(gameTime);

        _floatFactor =
            (float)Math.Sin(
                gameTime.TotalGameTime.TotalMilliseconds / 160f) * 4f;

        base.Update(gameTime);
    }

    private void DrawMenuOptions()
    {
        foreach (MenuOption option in _menuOptions)
        {
            Vector2 oldScale = option.sprite.Scale;

            option.sprite.Scale = _currentScales[option];

            Vector2 spritePosition = new Vector2(
                option.position.X,
                option.position.Y + _floatFactor);

            option.Draw(spritePosition);

            DrawOptionText(
                option,
                spritePosition);

            option.sprite.Scale = oldScale;
        }
    }

    private void DrawOptionText(MenuOption option, Vector2 spritePosition)
    {
        Vector2 textSize = _font.MeasureString(option.Name);

        Vector2 textPosition = spritePosition - textSize * 2f;

        bool isSelected = _menuOptions[_selectedIndex] == option;

        Color textColor =
            isSelected
                ? Color.Black
                : Color.DarkSlateGray;

        ldvsGame.SpriteBatch.DrawString(
            _font,
            option.Name,
            textPosition,
            textColor,0f,Vector2.Zero,5f,SpriteEffects.None,0f);
    }

    private void HandleInput()
    {
        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Up))
        {
            _selectedIndex = Math.Max(_selectedIndex - 1, 0);
        }

        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Down))
        {
            _selectedIndex = Math.Min(_selectedIndex + 1, _menuOptions.Count - 1);
        }

        if (ldvsGame.Input.Keyboard.WasKeyJustPressed(Keys.Enter))
        {
            MenuOption selectedOption = _menuOptions[_selectedIndex];

            ldvsGame.ChangeScene(selectedOption.sceneTo);
        }
    }

    private void UpdateMenuScales(GameTime gameTime)
    {
        float deltaTime =
            (float)gameTime.ElapsedGameTime.TotalSeconds;

        float interpolation =
            1f - (float)Math.Exp(-ScaleSpeed * deltaTime);

        for (int i = 0; i < _menuOptions.Count; i++)
        {
            MenuOption option = _menuOptions[i];

            float scaleMultiplier =
                i == _selectedIndex
                    ? SelectedScaleMultiplier
                    : UnselectedScaleMultiplier;

            Vector2 targetScale =
                option.BaseScale * scaleMultiplier;

            _currentScales[option] = Vector2.Lerp(
                _currentScales[option],
                targetScale,
                interpolation);
        }
    }
}
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Scenes;

namespace ldvs.Core.Content.Entities;

public class MenuOption
{
    public Scene sceneTo;
    public Sprite sprite;
    public Vector2 position;

    public Vector2 BaseScale { get; }

    public MenuOption(Scene sceneTo, Sprite sprite, Vector2 position)
    {
        this.sceneTo = sceneTo;
        this.sprite = sprite;
        this.position = position;

        BaseScale = sprite.Scale; // cache original scale
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        sprite.Draw(position);
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 customPosition)
    {
        sprite.Draw(customPosition);
    }

    public void OnConfirm()
    {
        ldvsGame.ChangeScene(sceneTo);
    }
}
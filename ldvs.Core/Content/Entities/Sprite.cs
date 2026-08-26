using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices.Swift;
using ldvs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameLibrary.Graphics;

static class SpriteExtensions
{
    public static void CreateBorder(this Sprite sprite, int borderWidth, Color borderColor)
    {
        Color[] colors = new Color[(int)(sprite.Scale.X * sprite.Scale.Y)];

        for (int x = 0; x < sprite.Width; x++)
        {
            for (int y = 0; y < sprite.Height; y++)
            {
                bool colored = false;

                for (int i = 0; i <= borderWidth; i++)
                {
                    if (x == i || y == i || x == sprite.Width - 1 - i || y == sprite.Height - 1 - i)
                    {
                        colors[(int)(x + y * sprite.Scale.X)] = borderColor;
                        colored = true;

                        break;
                    }
                }

                if (colored == false)
                    colors[(int)(x + y * sprite.Scale.X)] = Color.Transparent;
            }
        }

        sprite.texture.SetData(colors);
    }
}


public class Sprite
{
    public Texture2D texture { get; set; }

    /// <summary>
    /// Gets or Sets the color mask to apply when rendering this sprite.
    /// </summary>
    /// <remarks>
    /// Default value is Color.White
    /// </remarks>
    public Color Color { get; set; } = Color.White;

    /// <summary>
    /// Gets or Sets the amount of rotation, in radians, to apply when rendering this sprite.
    /// </summary>
    /// <remarks>
    /// Default value is 0.0f
    /// </remarks>
    public float Rotation { get; set; } = 0.0f;

    /// <summary>
    /// Gets or Sets the scale factor to apply to the x- and y-axes when rendering this sprite.
    /// </summary>
    public Vector2 Scale { get; set; } = Vector2.One;

    /// <summary>
    /// Gets or Sets the xy-coordinate origin point, relative to the top-left corner, of this sprite.
    /// </summary>
    /// <remarks>
    /// Default value is Vector2.Zero
    /// </remarks>
    public Vector2 Origin { get; set; } = Vector2.Zero;

    /// <summary>
    /// Gets or Sets the sprite effects to apply when rendering this sprite.
    /// </summary>
    /// <remarks>
    /// Default value is SpriteEffects.None
    /// </remarks>
    public SpriteEffects Effects { get; set; } = SpriteEffects.None;

    /// <summary>
    /// Gets or Sets the layer depth to apply when rendering this sprite.
    /// </summary>
    /// <remarks>
    /// Default value is 0.0f
    /// </remarks>
    public float LayerDepth { get; set; } = 0.0f;

    /// <summary>
    /// Gets the width, in pixels, of this sprite.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Gets the height, in pixels, of this sprite.
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// Creates a new sprite.
    /// </summary>
    public Sprite() { }

    public Sprite(Texture2D texture,
                  Color color = default,
                  Vector2 scale = default,
                  Vector2 justify = default,
                  float rotation = 0,
                  SpriteEffects spriteEffects = default,
                  float layerDepth = 0)
    {
        this.texture = texture;
        Color = color == default ? Color.White : color;

        Width = texture.Width;
        Height = texture.Height;
        Scale = scale;
        Origin = justify;

        Rotation = rotation;
        Effects = spriteEffects;
        LayerDepth = layerDepth;
    }

    public Sprite(Texture2D texture,
        Color color = default,
        float scale = 1f,
        Vector2 justify = default,
        float rotation = 0,
        SpriteEffects spriteEffects = default,
        float layerDepth = 0)
    {
        this.texture = texture;
        Color = color == default ? Color.White : color;

        Width = texture.Width;
        Height = texture.Height;
        Scale = Vector2.One*scale;

        justify = new Vector2(MathHelper.Clamp(justify.X, 0f, 1f), MathHelper.Clamp(justify.Y, 0f, 1f)); // idk what justify negative or above 1 would entail so no >:(

        Origin = new Vector2(Width * justify.X, Height * justify.Y);

        Rotation = rotation;
        Effects = spriteEffects;
        LayerDepth = layerDepth;
    }

    /// <summary>
    /// Sets the origin of this sprite to the center.
    /// </summary>
    public void CenterOrigin()
    {
        Origin = new Vector2(Width, Height) * 0.5f;
    }

    /// <summary>
    /// Submit this sprite for drawing to the current batch.
    /// </summary>
    /// <param name="position">The xy-coordinate position to render this sprite at.</param>
    public void Draw(Vector2 position)
    {
        ldvsGame.SpriteBatch.Draw(texture, position, null, Color, Rotation, Origin, Scale, Effects, LayerDepth);
    }

    public void Draw(Rectangle rectangle)
    {
        ldvsGame.SpriteBatch.Draw(texture, rectangle, Color);
    }
}
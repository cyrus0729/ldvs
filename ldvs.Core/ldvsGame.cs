using System;
using ldvs.Core.Localization;
using System.Collections.Generic;
using System.Globalization;
using ldvs.Core.Content.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Audio;
using MonoGameLibrary.Input;
using MonoGameLibrary.Scenes;

// ReSharper disable all ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace ldvs.Core
{
    /// <summary>
    /// The main class for the game, responsible for managing game components, settings, 
    /// and platform-specific configurations.
    /// </summary>
    public class ldvsGame : Game
    {
        internal static ldvsGame s_instance;

        /// <summary>
        /// Gets a reference to the Core instance.
        /// </summary>
        public static ldvsGame Instance => s_instance;

        private static Scene s_activeScene;
        private static Scene s_nextScene;

        /// <summary>
        /// Gets the graphics device manager to control the presentation of graphics.
        /// </summary>
        public static GraphicsDeviceManager Graphics { get; private set; }

        /// <summary>
        /// Gets the graphics device used to create graphical resources and perform primitive rendering.
        /// </summary>
        public new static GraphicsDevice GraphicsDevice { get; private set; }

        /// <summary>
        /// Gets the sprite batch used for all 2D rendering.
        /// </summary>
        public static SpriteBatch SpriteBatch { get; private set; }

        /// <summary>
        /// Gets the content manager used to load global assets.
        /// </summary>
        public new static ContentManager Content { get; private set; }

        /// <summary>
        /// Gets a reference to to the input management system.
        /// </summary>
        public static InputHandler Input { get; private set; }

        /// <summary>
        /// Gets a reference to the audio control system.
        /// </summary>
        public static AudioController Audio { get; private set; }

        /// <summary>
        /// Initializes a new instance of the game. Configures platform-specific settings, 
        /// initializes services like settings and leaderboard managers, and sets up the 
        /// screen manager for screen transitions.
        /// </summary>
        public ldvsGame(string title, int width, int height, bool fullScreen)
        {
            if (s_instance != null)
            {
                throw new InvalidOperationException($"Only a single Core instance can be created");
            }

            s_instance = this;

            Graphics = new GraphicsDeviceManager(this);
            Graphics.PreferredBackBufferWidth = width;
            Graphics.PreferredBackBufferHeight = height;
            Graphics.IsFullScreen = fullScreen;
            Graphics.ApplyChanges();

            Window.Title = title;

            Content = base.Content;
            Content.RootDirectory = "Content";

            IsMouseVisible = true;
        }

        public static void ChangeScene(Scene next)
        {
            if (s_activeScene != next)
            {
                s_nextScene = next;
            }
        }

        /// <summary>
        /// Draws the game's graphics, called once per frame.
        /// </summary>
        /// <param name="gameTime">
        /// Provides a snapshot of timing values used for rendering.
        /// </param>
        protected override void Draw(GameTime gameTime)
        {
            if (s_activeScene != null)
            {
                s_activeScene.Draw(gameTime);
            }
            base.Draw(gameTime);
        }

        /// <summary>
        /// Initializes the game, including setting up localization and adding the 
        /// initial screens to the ScreenManager.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            GraphicsDevice = base.GraphicsDevice;
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            Audio = new AudioController();
            Input = new InputHandler();

            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();
            var languages = new List<CultureInfo>();

            for (int i = 0; i < cultures.Count; i++)
            {
                languages.Add(cultures[i]);
            }

            Window.AllowUserResizing = true;
            IsFixedTimeStep = false;
            Window.ClientSizeChanged += Window_ClientSizeChanged;

            // TODO load this from a settings file or similar
            var selectedLanguage = LocalizationManager.DEFAULT_CULTURE_CODE;
            LocalizationManager.SetCulture(selectedLanguage);

            ChangeScene(new TitleScreen());
        }

        /// <summary>
        /// Loads game content, such as textures and particle systems.
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            base.OnExiting(sender, args);
        }

        /// <summary>
        /// Updates the game's logic, called once per frame.
        /// </summary>
        /// <param name="gameTime">
        /// Provides a snapshot of timing values used for game updates.
        /// </param>
        protected override void Update(GameTime gameTime)
        {

            Input.Update(gameTime);
            Audio.Update();
            if (s_nextScene != null)
            {
                Logger.Log(@"Changing scene to " + s_nextScene);
                TransitionScene();
            }
            if (s_activeScene != null)
            {
                s_activeScene.Update(gameTime);
            }
            base.Update(gameTime);
        }

        private static void TransitionScene()
        {
            if (s_activeScene != null)
            {
                s_activeScene.Dispose();
            }
            GC.Collect();
            s_activeScene = s_nextScene;
        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            s_nextScene = null;
        #pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            if (s_activeScene != null)
            {
                s_activeScene.Initialize();
            }
        }

        void Window_ClientSizeChanged(object? sender, EventArgs e)
        {
        }
    }
}
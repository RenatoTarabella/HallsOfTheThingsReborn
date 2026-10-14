using System.Numerics;
using Halls.Core;
using Raylib_cs;

namespace Halls.Game;

/// <summary>The window and the loop: front end, game, result, as the front end of 1983 (FrontLoop).</summary>
public sealed class App : IDisposable
{
    enum State { Front, Playing, Result, Keys }

    readonly Settings settings = Settings.Load();
    readonly Spectrum spectrum;
    readonly Renderer3D renderer3D;
    readonly MapView mapView;
    bool showMap;
    readonly Danger danger = new();
    bool quit;
    readonly Options cli;
    State state = State.Front;
    Core.Game? game;
    uint seed;
    double pending;

    // the result: the score, the name being written
    int resultScore, resultLine;
    GameEnd resultEnd;
    int resultRings, resultDead;
    bool enteringName;
    string name = "";

    public sealed record Options(string? Screenshot, int Frames, bool? View3D, int? Floor, uint? Seed, float Zoom = 1, bool Map = false, bool Suspend = false, bool Resume = false, int Wounds = -1);

    public App(Options options)
    {
        cli = options;
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint | ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(1280, 1024, "Halls of the Things");
        Raylib.SetExitKey(KeyboardKey.Null);
        // inside the exe, so that the single file of the release needs nothing beside it
        using (var icon = typeof(App).Assembly.GetManifestResourceStream("Halls.icon.png"))
            if (icon != null)
            {
                var bytes = new byte[icon.Length];
                icon.ReadExactly(bytes);
                var img = Raylib.LoadImageFromMemory(".png", bytes);
                Raylib.SetWindowIcon(img);
                Raylib.UnloadImage(img);
            }
        Raylib.SetTargetFPS(60);
        if (options.Screenshot == null) RestoreWindow();
        spectrum = new Spectrum();
        renderer3D = new Renderer3D(spectrum) { Preset = settings.CameraPreset, StrongInRed = settings.StrongInRed, ZoomLevel = settings.Zoom };
        mapView = new MapView(spectrum);
        if (options.View3D is bool v) settings.View = v ? ViewMode.TopDown3D : ViewMode.Classic2D;
        // FRAMES at loading time, once: the sequence goes on from game to game
        seed = options.Seed ?? (uint)Environment.TickCount;
    }

    public void Run()
    {
        if (cli.Screenshot != null) { RunScreenshot(); return; }
        while (!Raylib.WindowShouldClose() && !quit)
        {
            float dt = Raylib.GetFrameTime();
            GlobalKeys();
            RememberWindow();
            switch (state)
            {
                case State.Front: FrontFrame(); break;
                case State.Playing: GameFrame(dt); break;
                case State.Result: ResultFrame(); break;
                case State.Keys: KeysFrame(); break;
            }
        }
        // closing the window in the middle of a game keeps it for the next time
        if (state == State.Playing && game is { End: GameEnd.None }) SaveGame();
        settings.Save();
    }

    /// <summary>--screenshot: a game started, some cycles run, one picture saved (for the tests of the graphics).</summary>
    void RunScreenshot()
    {
        if (cli.Frames < 0)
        {
            // the page to redefine the keys
            keyMessage = "Swapped with Lightning";
            KeysFrame();
            Raylib.TakeScreenshot(cli.Screenshot!);
            return;
        }
        if (cli.Frames == 0)
        {
            // the front page
            FrontFrame();
            Raylib.TakeScreenshot(cli.Screenshot!);
            return;
        }
        if (!(cli.Resume && ResumeGame())) StartGame();
        if (cli.Floor is int f && game != null) { game.DebugRecreate(f); game.DebugPlaceMan(f == 0 ? 102 * 128 + 100 : 52 * 128 + 52); renderer3D.Snap(); }
        renderer3D.ZoomLevel = cli.Zoom;
        if (cli.Wounds >= 0) game!.DebugSetWounds(cli.Wounds);
        for (int i = 0; i < cli.Frames; i++)
        {
            var g = game!;
            // wander about, so that the map has something seen
            if (cli.Map && i % 40 == 0) { int d = i / 40 % 4; g.Left = d == 3; g.Right = d == 1; g.Up = d == 0; g.Down = d == 2; }
            g.Update(6);
            var (x0, y0, x1, y1) = renderer3D.Shown(g);
            mapView.MarkSeen(g, x0, y0, x1, y1);
            Raylib.BeginDrawing();
            if (cli.Map && i == cli.Frames - 1) mapView.Draw(g, 1 / 60f);
            else DrawGame(1 / 60f);
            Raylib.EndDrawing();
        }
        Raylib.TakeScreenshot(cli.Screenshot!);
        if (cli.Suspend) SaveGame();
        // TakeScreenshot writes into the working folder of raylib; the frame after is safe
        Raylib.BeginDrawing(); Raylib.EndDrawing();
    }

    void GlobalKeys()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F11) || (Raylib.IsKeyPressed(KeyboardKey.Enter) && Raylib.IsKeyDown(KeyboardKey.LeftAlt)))
        {
            Raylib.ToggleBorderlessWindowed();
            settings.Fullscreen = !settings.Fullscreen;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.F2)) ToggleView();
        if (Raylib.IsKeyPressed(KeyboardKey.F3))
        {
            renderer3D.Preset = settings.CameraPreset = (settings.CameraPreset + 1) % 3;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.F12)) Raylib.TakeScreenshot($"halls-{DateTime.Now:yyyyMMdd-HHmmss}.png");
    }

    /// <summary>The window of the last time: where it was, how large, maximized, full screen.</summary>
    void RestoreWindow()
    {
        if (settings.Window is [int x, int y, int w, int h] && w >= 320 && h >= 256 && OnAMonitor(x, y, w))
        {
            Raylib.SetWindowSize(w, h);
            Raylib.SetWindowPosition(x, y);
        }
        if (settings.Maximized) Raylib.MaximizeWindow();
        // after the rest: leaving the full screen gives back the window above
        if (settings.Fullscreen) Raylib.ToggleBorderlessWindowed();
    }

    /// <summary>The title bar on some monitor, so that a monitor no longer there does not hide the window.</summary>
    static bool OnAMonitor(int x, int y, int w)
    {
        for (int m = 0; m < Raylib.GetMonitorCount(); m++)
        {
            var p = Raylib.GetMonitorPosition(m);
            int mx = (int)p.X, my = (int)p.Y, mw = Raylib.GetMonitorWidth(m), mh = Raylib.GetMonitorHeight(m);
            if (x + w > mx + 100 && x < mx + mw - 100 && y >= my - 10 && y < my + mh - 50) return true;
        }
        return false;
    }

    /// <summary>Every frame, what goes into the settings: the window only while it is a plain window.</summary>
    void RememberWindow()
    {
        settings.Zoom = renderer3D.ZoomLevel;
        if (Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode) || Raylib.IsWindowMinimized()) return;
        settings.Maximized = Raylib.IsWindowMaximized();
        if (settings.Maximized) return;
        var p = Raylib.GetWindowPosition();
        settings.Window = [(int)p.X, (int)p.Y, Raylib.GetScreenWidth(), Raylib.GetScreenHeight()];
    }

    /// <summary>Pressed now or repeating while held.</summary>
    static bool Pressed(KeyboardKey k) => Raylib.IsKeyPressed(k) || Raylib.IsKeyPressedRepeat(k);

    void ToggleView()
    {
        settings.View = settings.View == ViewMode.Classic2D ? ViewMode.TopDown3D : ViewMode.Classic2D;
        renderer3D.Snap();
    }

    // ---------------- front end ----------------

    void FrontFrame()
    {
        if (Raylib.IsKeyPressed(settings.ModernKeys ? KeyboardKey.M : KeyboardKey.Tab)) settings.ModernKeys = !settings.ModernKeys;
        if (Raylib.IsKeyPressed(KeyboardKey.K) && settings.ModernKeys)
        {
            keySelected = 0; keyWaiting = false; keyMessage = "";
            state = State.Keys;
            return;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.R)) renderer3D.StrongInRed = settings.StrongInRed = !settings.StrongInRed;
        // the characters, not the keys: + is elsewhere on an Italian keyboard
        bool kp = Raylib.IsKeyDown(KeyboardKey.KpAdd) || Raylib.IsKeyDown(KeyboardKey.KpSubtract);
        if (Pressed(KeyboardKey.KpAdd)) settings.CyclesPerSecond = Math.Min(900, settings.CyclesPerSecond + 25);
        if (Pressed(KeyboardKey.KpSubtract)) settings.CyclesPerSecond = Math.Max(100, settings.CyclesPerSecond - 25);
        for (int ch = Raylib.GetCharPressed(); ch != 0; ch = Raylib.GetCharPressed())
        {
            if (kp) continue;
            if (ch == '+') settings.CyclesPerSecond = Math.Min(900, settings.CyclesPerSecond + 25);
            if (ch == '-') settings.CyclesPerSecond = Math.Max(100, settings.CyclesPerSecond - 25);
        }
        bool saved = File.Exists(Settings.SavedGamePath);
        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            settings.Save();
            if (!saved || !ResumeGame()) StartGame();
            return;
        }
        if (saved && Raylib.IsKeyPressed(KeyboardKey.N))
        {
            // a new game: the saved one is given up
            File.Delete(Settings.SavedGamePath);
            settings.Save();
            StartGame();
            return;
        }
        spectrum.Begin();
        Screens.Front(spectrum, settings, renderer3D.ModelCount, renderer3D.ModelErrors.Count, saved);
        spectrum.End();
        Present2D();
    }

    void StartGame()
    {
        // "Please wait for a second": Gen8 takes a few milliseconds here
        game = new Core.Game(seed, new GameOptions { ModernControls = settings.ModernKeys, FollowMan = settings.FollowMan, TapStep = settings.ModernKeys });
        game.Start();
        pending = 0;
        renderer3D.Snap();
        mapView.Reset();
        showMap = false;
        state = State.Playing;
    }

    // ---------------- save and resume ----------------

    /// <summary>F5 or closing the window: the whole game into suspended.sav.</summary>
    void SaveGame()
    {
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            string tmp = Settings.SavedGamePath + ".tmp";
            using (var w = new BinaryWriter(File.Create(tmp)))
            {
                game!.Save(w);
                mapView.Save(w);
            }
            File.Move(tmp, Settings.SavedGamePath, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>The saved game, once: it is deleted as soon as it is taken again.</summary>
    bool ResumeGame()
    {
        Core.Game? g = null;
        try
        {
            using (var r = new BinaryReader(File.OpenRead(Settings.SavedGamePath)))
            {
                g = Core.Game.Load(r, new GameOptions { ModernControls = settings.ModernKeys, FollowMan = settings.FollowMan, TapStep = settings.ModernKeys });
                if (g != null) mapView.Load(r);
            }
        }
        catch (IOException) { g = null; }
        catch (UnauthorizedAccessException) { g = null; }
        try { File.Delete(Settings.SavedGamePath); } catch (IOException) { }
        if (g == null) { mapView.Reset(); return false; }
        game = g;
        pending = 0;
        showMap = false;
        renderer3D.Snap();
        state = State.Playing;
        return true;
    }

    // ---------------- the game ----------------

    void GameFrame(float dt)
    {
        var g = game!;
        // + and - zoom the 3D view, from the classic screen to twice as far:
        // the keys of the keypad, or the characters (+ is elsewhere on an Italian keyboard)
        bool kp = Raylib.IsKeyDown(KeyboardKey.KpAdd) || Raylib.IsKeyDown(KeyboardKey.KpSubtract);
        if (Pressed(KeyboardKey.KpAdd)) renderer3D.ZoomLevel /= 1.08f;
        if (Pressed(KeyboardKey.KpSubtract)) renderer3D.ZoomLevel *= 1.08f;
        for (int ch = Raylib.GetCharPressed(); ch != 0; ch = Raylib.GetCharPressed())
        {
            if (kp) continue;
            if (ch == '+') renderer3D.ZoomLevel /= 1.08f;
            if (ch == '-') renderer3D.ZoomLevel *= 1.08f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.F5) && g.End == GameEnd.None)
        {
            // save and quit: the game goes on from here at the next start
            SaveGame();
            quit = true;
            return;
        }
        if (Raylib.IsKeyPressed(Controls.MapKey(settings)) && !g.ShowingStatus) showMap = !showMap;
        if (showMap)
        {
            // the map: the game stands still
            pending = 0;
            Raylib.BeginDrawing();
            mapView.Draw(g, dt);
            Raylib.EndDrawing();
            return;
        }
        Controls.Read(g, settings);
        if (Raylib.IsKeyPressed(Controls.CentreKey(settings)) && Controls.Shift)
        {
            settings.FollowMan = !settings.FollowMan;
            g.SetFollow(settings.FollowMan);
        }
        if (Raylib.IsKeyPressed(Controls.StatisticsKey(settings))) settings.Statistics = !settings.Statistics;
        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0) renderer3D.Zoom(wheel);

        // fixed cycles of GameLoop, at most 40 behind (as the RISC OS version)
        pending += dt * settings.CyclesPerSecond;
        if (pending > 40) pending = 40;
        int n = (int)pending;
        pending -= n;
        int floor = g.Floor;
        g.Update(n);
        if (g.Floor != floor) renderer3D.Snap();
        if (!g.ShowingStatus)
        {
            if (settings.View == ViewMode.TopDown3D)
            {
                var (x0, y0, x1, y1) = renderer3D.Shown(g);
                mapView.MarkSeen(g, x0, y0, x1, y1);
            }
            else
            {
                int fx = g.FrameX > 128 ? g.FrameX - 256 : g.FrameX, fy = g.FrameY > 128 ? g.FrameY - 256 : g.FrameY;
                mapView.MarkSeen(g, fx, fy, fx + Core.Game.ScreenW - 1, fy + Core.Game.ScreenH - 1);
            }
        }

        Raylib.BeginDrawing();
        DrawGame(dt);
        Raylib.EndDrawing();

        if (g.End != GameEnd.None) EndGame();
    }

    void DrawGame(float dt)
    {
        var g = game!;
        danger.Update(g, dt);
        spectrum.Begin();
        if (g.ShowingStatus) Screens.Status(spectrum, g);
        else
        {
            if (settings.View == ViewMode.Classic2D) Screens.Map(spectrum, g, settings.StrongInRed, danger.Colour);
            if (settings.Statistics) Screens.Statistics(spectrum, g);
        }
        spectrum.End();

        if (settings.View == ViewMode.Classic2D || g.ShowingStatus)
        {
            Raylib.ClearBackground(Color.Black);
            spectrum.Present(Spectrum.Fit(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Spectrum.Width, Spectrum.Height));
            return;
        }
        renderer3D.ManColour = danger.Colour;
        renderer3D.Draw(g, dt);
        if (settings.Statistics)
        {
            // the statistics of the border along the bottom of the window
            int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
            float s = MathF.Max(1, MathF.Floor(MathF.Min(w / 320f, h / 256f)));
            var dest = new Rectangle((w - 320 * s) / 2, h - 32 * s, 320 * s, 32 * s);
            Raylib.DrawRectangleRec(dest, new Color(0, 0, 0, 200));
            spectrum.Present(dest, fromY: 224, rows: 32);
        }
    }

    void Present2D()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        spectrum.Present(Spectrum.Fit(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Spectrum.Width, Spectrum.Height));
        Raylib.EndDrawing();
    }

    // ---------------- redefine keys ----------------

    int keySelected;
    bool keyWaiting;
    string keyMessage = "";

    void KeysFrame()
    {
        if (keyWaiting)
        {
            for (int k = Raylib.GetKeyPressed(); k != 0; k = Raylib.GetKeyPressed())
            {
                var key = (KeyboardKey)k;
                if (key == KeyboardKey.Backspace) { keyWaiting = false; keyMessage = ""; break; }
                if (!KeyMap.Allowed(key)) { keyMessage = KeyMap.Name(key) + " is kept by the game"; continue; }
                var (action, label, _) = KeyMap.All[keySelected];
                var swapped = KeyMap.Set(settings, action, key);
                keyMessage = swapped is Binding o
                    ? $"Swapped with {KeyMap.All.First(a => a.Action == o).Label}"
                    : $"{label}: {KeyMap.Name(key)}";
                keyWaiting = false;
                settings.Save();
                break;
            }
        }
        else
        {
            if (Pressed(KeyboardKey.Up)) keySelected = (keySelected + KeyMap.All.Length - 1) % KeyMap.All.Length;
            if (Pressed(KeyboardKey.Down)) keySelected = (keySelected + 1) % KeyMap.All.Length;
            if (Raylib.IsKeyPressed(KeyboardKey.R)) { KeyMap.Reset(settings); settings.Save(); keyMessage = "All keys as at first"; }
            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
            {
                // the RETURN that asked is not the new key
                keyWaiting = true;
                keyMessage = "";
            }
            else if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Backspace))
            {
                settings.Save();
                state = State.Front;
                return;
            }
            // the queue of keys is read only while waiting
            while (Raylib.GetKeyPressed() != 0) { }
        }
        while (Raylib.GetCharPressed() != 0) { }
        spectrum.Begin();
        Screens.Keys(spectrum, settings, keySelected, keyWaiting, keyMessage);
        spectrum.End();
        Present2D();
    }

    // ---------------- the end ----------------

    void EndGame()
    {
        var g = game!;
        seed = g.Seed;
        resultEnd = g.End;
        resultRings = g.RingsFound;
        resultDead = g.DeadThings;
        resultScore = g.Score();
        enteringName = resultScore > settings.HighScore;
        name = "";
        // what was typed while playing is not the name
        while (Raylib.GetCharPressed() != 0) { }
        state = State.Result;
        game = null;
    }

    void ResultFrame()
    {
        spectrum.Begin();
        resultLine = Screens.Result(spectrum, resultEnd, resultRings, resultDead, resultScore);
        int y = resultLine;
        if (enteringName)
        {
            spectrum.Text(0, y, "Well done !");
            spectrum.Text(0, y + 1, "you have the high score");
            spectrum.Text(0, y + 2, "and have bettered " + settings.HighName);
            spectrum.Text(0, y + 3, $"by {resultScore - settings.HighScore} points.");
            spectrum.Text(0, y + 5, "Please enter your name");
            spectrum.Text(0, y + 6, name.PadRight(12, '_'));
            for (int ch = Raylib.GetCharPressed(); ch != 0; ch = Raylib.GetCharPressed())
                if (ch >= 32 && ch < 127 && name.Length < 12) name += (char)ch;
            if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && name.Length > 0)
                name = name[..^1];
            if (Raylib.IsKeyPressed(KeyboardKey.Enter))
            {
                string n = name.Trim();
                settings.HighScore = resultScore;
                settings.HighName = n.Length == 0 ? "someone" : n;
                enteringName = false;
                settings.Save();
            }
        }
        else
        {
            if (resultScore > 0 && resultScore == settings.HighScore) spectrum.Text(0, y, "You have the high score.");
            spectrum.Text(0, 23, "Press SPACE to continue");
            if (Raylib.IsKeyPressed(KeyboardKey.Space)) state = State.Front;
        }
        spectrum.End();
        Present2D();
    }

    public void Dispose()
    {
        mapView.Dispose();
        renderer3D.Dispose();
        spectrum.Dispose();
        Raylib.CloseWindow();
    }
}

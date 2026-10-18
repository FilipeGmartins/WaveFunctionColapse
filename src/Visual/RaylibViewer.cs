using System.Numerics;
using Raylib_cs;
using WaveFunctionCollapse.Application;
using WaveFunctionCollapse.Simulation;
using WaveFunctionCollapse.Simulation.Parts;

namespace WaveFunctionCollapse.Visual;

public sealed class RaylibViewer : ISimulationDriver, IDisposable
{
    private readonly PlaybackClock clock = new();
    private readonly GridRenderer gridRenderer = new();
    private readonly CellRenderer cellRenderer = new();
    private readonly bool smoke;
    private readonly string? capture;
    private bool initialized, manual, showPerception = true, autoAdvance;

    public RaylibViewer(bool smoke = false, string? capture = null)
    {
        this.smoke = smoke;
        this.capture = capture is null ? null : Path.GetFullPath(capture);
    }

    public bool Run(SimulationWorld world, int maxSteps, int totalGenerations, Action<MovementIntent?> step)
    {
        EnsureWindow();
        clock.SetPaused(clock.Paused);
        double finishedSeconds = 0;
        int frames = 0;
        while (!Raylib.WindowShouldClose())
        {
            bool finished = !world.Agent.IsAlive || world.Tick >= maxSteps;
            bool lastGeneration = world.Generation + 1 >= totalGenerations;
            var board = BoardLayout.Create(Math.Max(1100, Raylib.GetScreenWidth()),
                Math.Max(760, Raylib.GetScreenHeight()), world.Environment.Width, world.Environment.Height);
            int buttonY = Raylib.GetScreenHeight() - 112;
            var pauseButton = new Rectangle(24, buttonY, 128, 38);
            var stepButton = new Rectangle(160, buttonY, 112, 38);
            var slowerButton = new Rectangle(280, buttonY, 44, 38);
            var fasterButton = new Rectangle(332, buttonY, 44, 38);
            var nextButton = new Rectangle(388, buttonY, 220, 38);
            bool singleStep = Raylib.IsKeyPressed(KeyboardKey.N) || Clicked(stepButton);
            if (Raylib.IsKeyPressed(KeyboardKey.Space) || Clicked(pauseButton)) clock.SetPaused(!clock.Paused);
            if (Raylib.IsKeyPressed(KeyboardKey.Minus) || Clicked(slowerButton)) clock.SetSpeed(Math.Max(0.25, clock.Speed / 2));
            if (Raylib.IsKeyPressed(KeyboardKey.Equal) || Clicked(fasterButton)) clock.SetSpeed(Math.Min(32, clock.Speed * 2));
            if (Raylib.IsKeyPressed(KeyboardKey.A)) manual = !manual;
            if (Raylib.IsKeyPressed(KeyboardKey.H)) showPerception = !showPerception;
            if (Raylib.IsKeyPressed(KeyboardKey.G)) autoAdvance = !autoAdvance;
            if (finished && (Raylib.IsKeyPressed(KeyboardKey.Enter) || Clicked(nextButton))) return !lastGeneration;

            float frameSeconds = Raylib.GetFrameTime();
            if (!finished)
            {
                if (singleStep) clock.SetPaused(true);
                int ticks = smoke || singleStep ? 1 : clock.Advance(frameSeconds, world.FixedDeltaSeconds);
                for (int tick = 0; tick < ticks && world.Agent.IsAlive && world.Tick < maxSteps; tick++)
                    step(manual ? ReadMovement() : null);
            }
            else if (!clock.Paused && autoAdvance && !lastGeneration)
            {
                finishedSeconds += Math.Min(frameSeconds, 0.25);
                if (finishedSeconds >= 1.5) return true;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Theme.Background);
            DrawHeader(world, totalGenerations);
            gridRenderer.Draw(world, board, showPerception);
            var center = board.ToScreen(world.Agent.X, world.Agent.Y);
            cellRenderer.Draw(world.Agent, center, Math.Max(5, board.TileSize * 0.4f), world.ElapsedSeconds);
            DrawInspector(world, board, maxSteps);
            DrawButton(pauseButton, clock.Paused ? "RETOMAR" : "PAUSAR", true);
            DrawButton(stepButton, "PASSO / N", !finished);
            DrawButton(slowerButton, "-", true);
            DrawButton(fasterButton, "+", true);
            DrawButton(nextButton, lastGeneration ? "CONCLUIR / ENTER" : "PROXIMA / ENTER", finished);
            Theme.TextAt($"{clock.Speed:0.##}x  |  {(manual ? "MANUAL / SETAS" : "AUTOMATICO")}  |  FPS {Raylib.GetFPS()}",
                24, buttonY + 52, 17, Theme.Cyan);
            Theme.TextAt("ESPACO pausa   N passo   +/- velocidade   A controle   H percepcao   G auto-geracao   ESC sair",
                24, Raylib.GetScreenHeight() - 28, 15, Theme.Muted);
            Raylib.EndDrawing();
            frames++;
            if (Raylib.IsKeyPressed(KeyboardKey.F12) && capture is not null) SaveCapture();
            if (smoke && frames >= 16)
            {
                if (capture is not null) SaveCapture();
                return false;
            }
        }
        return false;
    }

    private void DrawHeader(SimulationWorld world, int totalGenerations)
    {
        Raylib.DrawCircle(35, 39, 10, Theme.Cyan);
        Theme.TextAt("BIO / GEN", 56, 22, 30);
        Theme.TextAt("WFC + EVOLUCAO CELULAR", 26, 66, 16, Theme.Muted);
        int x = Math.Max(620, Raylib.GetScreenWidth() - 490);
        Theme.TextAt($"GERACAO {world.Generation + 1:00} / {totalGenerations:00}", x, 26, 22, Theme.Cyan);
        Theme.TextAt($"SEED {world.Environment.MapSeed}   |   TICK {world.Tick}", x, 60, 16, Theme.Muted);
    }

    private void DrawInspector(SimulationWorld world, BoardLayout board, int maxSteps)
    {
        int x = Raylib.GetScreenWidth() - 324, y = 108;
        Theme.Card(new(x, y, 300, Raylib.GetScreenHeight() - 132));
        x += 20;
        var agent = world.Agent;
        bool complete = !agent.IsAlive || world.Tick >= maxSteps;
        string status = !agent.IsAlive ? "MORTE REGISTRADA" : complete ? "LIMITE DE TEMPO" : clock.Paused ? "PAUSADO" : "EM SIMULACAO";
        Theme.TextAt(status, x, y + 20, 19, agent.IsAlive ? Theme.Cyan : Theme.Red);
        Theme.TextAt($"Tempo  {world.ElapsedSeconds:0.0} s", x, y + 55, 19);
        Theme.TextAt($"Posicao  {agent.X:0.00}, {agent.Y:0.00}", x, y + 82, 17, Theme.Muted);
        DrawBar("ATP / ENERGIA", agent.Energy, agent.Genome.MaxEnergy, x, y + 122, Theme.Gold);
        DrawBar("HP / INTEGRIDADE", agent.Health, agent.Genome.MaxHealth, x, y + 183, Theme.Cyan);
        Theme.TextAt($"Manutencao  {agent.Genome.BasalCost + agent.Genome.Maintenance:0.000} ATP/s", x, y + 245, 16, Theme.Muted);
        Theme.TextAt($"Deficit total  {agent.TotalEnergyDeficit:0.00}", x, y + 269, 16, Theme.Muted);

        var tile = board.HitTest(Raylib.GetMousePosition()) ?? ((int)agent.X, (int)agent.Y);
        var sample = world.Environment.Sample(tile.Item1, tile.Item2);
        Theme.TextAt($"PISO [{sample.X}, {sample.Y}] / ID {sample.TileId}", x, y + 311, 18, Theme.Cyan);
        Theme.TextAt($"Glicose {sample.Glucose:0.0}   Lipidios {sample.Lipids:0.0}", x, y + 343, 16);
        Theme.TextAt($"Toxina {sample.Toxin:0.0}  {sample.ToxinType ?? "-"}", x, y + 369, 16, sample.Toxin > 0 ? Theme.Red : Theme.Muted);
        Theme.TextAt($"Temperatura {sample.Temperature:0.0} C", x, y + 395, 16, Theme.Muted);
        Theme.TextAt($"Densidade {sample.Density:0.00}", x, y + 421, 16, Theme.Muted);
        Theme.TextAt("GENOMA / PARTES", x, y + 465, 18, Theme.Cyan);
        Theme.TextAt($"Flagelos {agent.Genome.Parts.OfType<Flagellum>().Count()}    Bocas {agent.Genome.Parts.OfType<Mouth>().Count()}", x, y + 497, 17);
        Theme.TextAt($"Receptores {agent.Genome.Parts.OfType<ChemicalReceptor>().Count()}   Raio {agent.Genome.PerceptionRange}", x, y + 523, 17);
        Theme.TextAt($"Resistencia {agent.Genome.ToxinResistance:P0}", x, y + 549, 16, Theme.Muted);
        if (world.Death is { } death)
            Theme.TextAt($"Causa: {CauseLabel(death.Cause)}", x, y + 587, 18, Theme.Red);
        else Theme.TextAt(autoAdvance ? "Auto-geracao: LIGADA" : "Auto-geracao: DESLIGADA", x, y + 587, 16, Theme.Muted);
    }

    private static string CauseLabel(Simulation.Events.DeathCause cause) => cause switch
    {
        Simulation.Events.DeathCause.Starvation => "Inanicao",
        Simulation.Events.DeathCause.Toxin => "Toxina",
        _ => "Temperatura"
    };

    private static void DrawBar(string label, double value, double max, int x, int y, Color color)
    {
        Theme.TextAt(label, x, y, 14, Theme.Muted);
        string values = $"{value:0.0} / {max:0.0}";
        Theme.TextAt(values, x + 260 - Raylib.MeasureText(values, 14), y, 14, color);
        Raylib.DrawRectangleRounded(new(x, y + 25, 260, 10), 0.8f, 6, Theme.Border);
        float width = (float)Math.Clamp(value / max, 0, 1) * 260;
        if (width > 0) Raylib.DrawRectangleRounded(new(x, y + 25, width, 10), 0.8f, 6, color);
    }

    private static bool Clicked(Rectangle rect) => Raylib.IsMouseButtonPressed(MouseButton.Left)
        && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);

    private static void DrawButton(Rectangle rect, string label, bool enabled)
    {
        bool hover = enabled && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
        Raylib.DrawRectangleRounded(rect, 0.2f, 5, hover ? new Color(38, 71, 78) : Theme.Panel);
        Raylib.DrawRectangleRoundedLinesEx(rect, 0.2f, 5, 1, enabled ? Theme.Cyan : Theme.Border);
        int width = Raylib.MeasureText(label, 16);
        Theme.TextAt(label, (int)(rect.X + (rect.Width - width) / 2), (int)rect.Y + 11, 16, enabled ? Theme.Text : Theme.Muted);
    }

    private static MovementIntent ReadMovement()
    {
        if (Raylib.IsKeyDown(KeyboardKey.Up)) return MovementIntent.North;
        if (Raylib.IsKeyDown(KeyboardKey.Right)) return MovementIntent.East;
        if (Raylib.IsKeyDown(KeyboardKey.Down)) return MovementIntent.South;
        if (Raylib.IsKeyDown(KeyboardKey.Left)) return MovementIntent.West;
        return MovementIntent.Stay;
    }

    private void EnsureWindow()
    {
        if (initialized) return;
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | (smoke ? ConfigFlags.HiddenWindow : 0));
        Raylib.InitWindow(1280, 820, "Bio / Gen - WFC Cellular Evolution");
        initialized = true;
        if (!Raylib.IsWindowReady()) throw new InvalidOperationException("Raylib could not initialize an OpenGL window.");
        Raylib.SetWindowMinSize(1100, 760);
        Raylib.SetTargetFPS(60);
    }

    private void SaveCapture()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(capture!)!);
        var frame = Raylib.LoadImageFromScreen();
        try
        {
            if (!Raylib.ExportImage(frame, capture!)) throw new IOException("Raylib did not save the screenshot.");
        }
        finally { Raylib.UnloadImage(frame); }
    }

    public void Dispose()
    {
        if (initialized) { Raylib.CloseWindow(); initialized = false; }
    }
}

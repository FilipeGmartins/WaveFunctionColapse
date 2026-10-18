using System.Numerics;
using Raylib_cs;
using WaveFunctionCollapse.Simulation.Agents;
using WaveFunctionCollapse.Simulation.Parts;

namespace WaveFunctionCollapse.Visual;

public sealed class CellRenderer
{
    public void Draw(CellAgent agent, Vector2 center, float radius, double seconds)
    {
        Color color = agent.IsAlive ? Theme.Cyan : Theme.Red;
        int count = agent.Genome.Parts.Count;
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(i * Math.Tau / Math.Max(1, count) - Math.PI / 2);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tip = center + direction * radius;
            switch (agent.Genome.Parts[i])
            {
                case Flagellum:
                    var last = tip;
                    for (int segment = 1; segment <= 8; segment++)
                    {
                        var normal = new Vector2(-direction.Y, direction.X);
                        var next = tip + direction * (radius * segment * 0.27f)
                            + normal * (MathF.Sin((float)seconds * 7 - segment * 0.8f) * radius * 0.22f);
                        Raylib.DrawLineEx(last, next, Math.Max(1, radius * 0.12f), color);
                        last = next;
                    }
                    break;
                case Mouth:
                    Raylib.DrawCircleV(tip, radius * 0.36f, Theme.Gold);
                    Raylib.DrawCircleV(tip + direction * 2, radius * 0.19f, Theme.Background);
                    break;
                case ChemicalReceptor:
                    var end = center + direction * radius * 1.55f;
                    Raylib.DrawLineEx(tip, end, Math.Max(1, radius * 0.1f), color);
                    Raylib.DrawCircleV(end, Math.Max(2, radius * 0.16f), Theme.Gold);
                    break;
            }
        }
        Raylib.DrawCircleV(center, radius, new Color(20, 64, 66));
        Raylib.DrawCircleLinesV(center, radius, color);
        Raylib.DrawCircleV(center - new Vector2(radius * 0.17f), radius * 0.35f, color);
        if (!agent.IsAlive)
        {
            Raylib.DrawLineEx(center - new Vector2(radius * 0.5f), center + new Vector2(radius * 0.5f), 2, Theme.Red);
            Raylib.DrawLineEx(center + new Vector2(radius * 0.5f, -radius * 0.5f),
                center + new Vector2(-radius * 0.5f, radius * 0.5f), 2, Theme.Red);
        }
    }
}

namespace WaveFunctionCollapse.Application;

public sealed record RunConfiguration(int Seed, int Generations, int Steps, string? LogPath,
    bool Primitive, bool MapOnly)
{
    public static RunConfiguration Parse(string[] args)
    {
        int seed = 42, generations = 1, steps = 3000;
        string? log = null;
        bool primitive = false, mapOnly = false, hasSeed = false;
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : throw new ArgumentException($"Missing value for {args[i - 1]}.");
            switch (args[i])
            {
                case "--generations": generations = int.Parse(Next()); break;
                case "--steps": steps = int.Parse(Next()); break;
                case "--log": log = Next(); ArgumentException.ThrowIfNullOrWhiteSpace(log); break;
                case "--primitive": primitive = true; break;
                case "--map-only": mapOnly = true; break;
                default:
                    if (hasSeed || !int.TryParse(args[i], out seed)) throw new ArgumentException($"Unknown argument: {args[i]}.");
                    hasSeed = true;
                    break;
            }
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(generations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(steps);
        return new(seed, generations, steps, log, primitive, mapOnly);
    }
}

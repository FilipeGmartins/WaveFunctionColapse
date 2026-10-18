using WaveFunctionCollapse.Application;
using WaveFunctionCollapse.Visual;

try
{
    var simulationArgs = new List<string>();
    bool smoke = false;
    string? capture = null;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--smoke-test") smoke = true;
        else if (args[i] == "--capture")
        {
            if (++i >= args.Length) throw new ArgumentException("Missing screenshot path.");
            capture = Path.GetFullPath(args[i]);
            if (!capture.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Capture must be PNG.");
        }
        else simulationArgs.Add(args[i]);
    }
    var config = RunConfiguration.Parse(simulationArgs.ToArray());
    if (config.MapOnly) return new GenerationLoop().Run(config);
    using var viewer = new RaylibViewer(smoke, capture);
    return new GenerationLoop().Run(config, viewer);
}
catch (Exception error) when (error is ArgumentException or IOException or FormatException or OverflowException
    or DllNotFoundException or BadImageFormatException or InvalidOperationException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

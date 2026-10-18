using WaveFunctionCollapse.Application;

try
{
    var config = RunConfiguration.Parse(args);
    return new GenerationLoop().Run(config);
}
catch (Exception error) when (error is ArgumentException or IOException or FormatException or OverflowException)
{
    Console.Error.WriteLine(error.Message);
    Console.Error.WriteLine("Usage: Runner [seed] [--generations N] [--steps N] [--log path] [--primitive] [--map-only]");
    return 1;
}

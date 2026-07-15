using BenchmarkDotNet.Running;

// Runs all benchmarks in this assembly. Examples:
//   dotnet run -c Release --project src/Tessera.Benchmarks -- --filter *
//   dotnet run -c Release --project src/Tessera.Benchmarks -- --filter *Compositor*
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

internal sealed partial class Program;

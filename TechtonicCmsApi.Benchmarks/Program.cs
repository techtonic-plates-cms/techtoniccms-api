using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace TechtonicCmsApi.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}

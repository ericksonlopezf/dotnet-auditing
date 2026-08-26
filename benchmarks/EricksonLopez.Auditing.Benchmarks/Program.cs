// Copyright © Erickson Lopez. MIT License.
using BenchmarkDotNet.Running;

namespace EricksonLopez.Auditing.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

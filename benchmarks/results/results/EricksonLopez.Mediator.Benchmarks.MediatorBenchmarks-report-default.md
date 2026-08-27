
BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.85GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host]    : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  ShortRun  : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

IterationCount=3  LaunchCount=1  WarmupCount=3  

 Method                           | Job       | Runtime   | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
--------------------------------- |---------- |---------- |------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
 DirectCall                       | .NET 10.0 | .NET 10.0 |   0.7109 ns |  0.2253 ns | 0.0123 ns |     ? |       ? |      - |         - |           ? |
 SendCommand_NoBehaviors          | .NET 10.0 | .NET 10.0 |  22.4821 ns |  2.9237 ns | 0.1603 ns |     ? |       ? |      - |         - |           ? |
 SendQuery_NoBehaviors            | .NET 10.0 | .NET 10.0 |  15.9994 ns |  0.6210 ns | 0.0340 ns |     ? |       ? |      - |         - |           ? |
 SendCommand_OneBehavior          | .NET 10.0 | .NET 10.0 |  47.9586 ns |  1.1517 ns | 0.0631 ns |     ? |       ? |      - |         - |           ? |
 SendCommand_FiveBehaviors        | .NET 10.0 | .NET 10.0 | 145.5764 ns |  1.5881 ns | 0.0870 ns |     ? |       ? |      - |         - |           ? |
 PublishNotification_OneHandler   | .NET 10.0 | .NET 10.0 |  41.6132 ns |  5.4110 ns | 0.2966 ns |     ? |       ? | 0.0014 |      24 B |           ? |
 PublishNotification_FiveHandlers | .NET 10.0 | .NET 10.0 |  99.8562 ns | 25.3832 ns | 1.3913 ns |     ? |       ? | 0.0072 |     120 B |           ? |
 PublishNotification_Parallel     | .NET 10.0 | .NET 10.0 | 114.6355 ns | 29.2893 ns | 1.6054 ns |     ? |       ? | 0.0114 |     192 B |           ? |
 NestedSend                       | .NET 10.0 | .NET 10.0 |  83.6209 ns |  1.8866 ns | 0.1034 ns |     ? |       ? | 0.0014 |      24 B |           ? |
 DirectCall                       | .NET 8.0  | .NET 8.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 SendCommand_NoBehaviors          | .NET 8.0  | .NET 8.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 SendQuery_NoBehaviors            | .NET 8.0  | .NET 8.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 SendCommand_OneBehavior          | .NET 8.0  | .NET 8.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 SendCommand_FiveBehaviors        | .NET 8.0  | .NET 8.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 PublishNotification_OneHandler   | .NET 8.0  | .NET 8.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 PublishNotification_FiveHandlers | .NET 8.0  | .NET 8.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 PublishNotification_Parallel     | .NET 8.0  | .NET 8.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 NestedSend                       | .NET 8.0  | .NET 8.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 DirectCall                       | .NET 9.0  | .NET 9.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 SendCommand_NoBehaviors          | .NET 9.0  | .NET 9.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 SendQuery_NoBehaviors            | .NET 9.0  | .NET 9.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 SendCommand_OneBehavior          | .NET 9.0  | .NET 9.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 SendCommand_FiveBehaviors        | .NET 9.0  | .NET 9.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 PublishNotification_OneHandler   | .NET 9.0  | .NET 9.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 PublishNotification_FiveHandlers | .NET 9.0  | .NET 9.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 PublishNotification_Parallel     | .NET 9.0  | .NET 9.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 NestedSend                       | .NET 9.0  | .NET 9.0  |          NA |         NA |        NA |     ? |       ? |     NA |        NA |           ? |
 DirectCall                       | ShortRun  | .NET 10.0 |   0.6985 ns |  0.0405 ns | 0.0022 ns |     ? |       ? |      - |         - |           ? |
 SendCommand_NoBehaviors          | ShortRun  | .NET 10.0 |  22.5806 ns |  0.5847 ns | 0.0320 ns |     ? |       ? |      - |         - |           ? |
 SendQuery_NoBehaviors            | ShortRun  | .NET 10.0 |  16.8762 ns |  1.3093 ns | 0.0718 ns |     ? |       ? |      - |         - |           ? |
 SendCommand_OneBehavior          | ShortRun  | .NET 10.0 |  48.2026 ns |  1.4048 ns | 0.0770 ns |     ? |       ? |      - |         - |           ? |
 SendCommand_FiveBehaviors        | ShortRun  | .NET 10.0 | 150.3734 ns |  5.6899 ns | 0.3119 ns |     ? |       ? |      - |         - |           ? |
 PublishNotification_OneHandler   | ShortRun  | .NET 10.0 |  42.2704 ns |  5.0845 ns | 0.2787 ns |     ? |       ? | 0.0014 |      24 B |           ? |
 PublishNotification_FiveHandlers | ShortRun  | .NET 10.0 | 103.3139 ns | 12.8079 ns | 0.7020 ns |     ? |       ? | 0.0072 |     120 B |           ? |
 PublishNotification_Parallel     | ShortRun  | .NET 10.0 | 113.0222 ns | 16.7687 ns | 0.9192 ns |     ? |       ? | 0.0114 |     192 B |           ? |
 NestedSend                       | ShortRun  | .NET 10.0 |  83.4121 ns |  3.9391 ns | 0.2159 ns |     ? |       ? | 0.0014 |      24 B |           ? |

Benchmarks with issues:
  MediatorBenchmarks.DirectCall: .NET 8.0(Runtime=.NET 8.0, Toolchain=net8.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.SendCommand_NoBehaviors: .NET 8.0(Runtime=.NET 8.0, Toolchain=net8.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.SendQuery_NoBehaviors: .NET 8.0(Runtime=.NET 8.0, Toolchain=net8.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.SendCommand_OneBehavior: .NET 8.0(Runtime=.NET 8.0, Toolchain=net8.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.SendCommand_FiveBehaviors: .NET 8.0(Runtime=.NET 8.0, Toolchain=net8.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.PublishNotification_OneHandler: .NET 8.0(Runtime=.NET 8.0, Toolchain=net8.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.PublishNotification_FiveHandlers: .NET 8.0(Runtime=.NET 8.0, Toolchain=net8.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.PublishNotification_Parallel: .NET 8.0(Runtime=.NET 8.0, Toolchain=net8.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.NestedSend: .NET 8.0(Runtime=.NET 8.0, Toolchain=net8.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.DirectCall: .NET 9.0(Runtime=.NET 9.0, Toolchain=net9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.SendCommand_NoBehaviors: .NET 9.0(Runtime=.NET 9.0, Toolchain=net9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.SendQuery_NoBehaviors: .NET 9.0(Runtime=.NET 9.0, Toolchain=net9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.SendCommand_OneBehavior: .NET 9.0(Runtime=.NET 9.0, Toolchain=net9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.SendCommand_FiveBehaviors: .NET 9.0(Runtime=.NET 9.0, Toolchain=net9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.PublishNotification_OneHandler: .NET 9.0(Runtime=.NET 9.0, Toolchain=net9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.PublishNotification_FiveHandlers: .NET 9.0(Runtime=.NET 9.0, Toolchain=net9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.PublishNotification_Parallel: .NET 9.0(Runtime=.NET 9.0, Toolchain=net9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)
  MediatorBenchmarks.NestedSend: .NET 9.0(Runtime=.NET 9.0, Toolchain=net9.0, IterationCount=3, LaunchCount=1, WarmupCount=3)

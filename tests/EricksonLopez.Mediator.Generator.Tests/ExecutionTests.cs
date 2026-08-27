// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Result;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

public class ExecutionTests
{
    private static Compilation CreateCompilation(string source)
    {
        var extraRefs = new[]
        {
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ServiceProvider).Assembly.Location)
        };
        return RoslynTestHelper.CreateCompilation(source, "ExecutionTestsComp", extraRefs);
    }

    [Fact]
    public async Task GeneratedMediator_ExecutesPipelineAndHandlersCorrectly()
    {
        string source = @"
using System.Runtime.CompilerServices;

namespace TestApp
{
    [UseBehavior(typeof(MyBehavior))]
    public class MyCommand : ICommand<int> 
    { 
        public int Value;
    }
    
    public class MyCommandHandler : ICommandHandler<MyCommand, int>
    {
        public ValueTask<int> Handle(MyCommand command, CancellationToken ct) 
        {
            return new(command.Value * 2);
        }
    }

    public class MyBehavior : IPipelineBehavior<MyCommand, int>
    {
        public async ValueTask<int> Handle<TNext>(MyCommand request, TNext next, CancellationToken ct)
            where TNext : struct, INext<int>
        {
            request.Value += 1;
            var result = await next.InvokeAsync();
            return result + 1;
        }
    }

    public class MyQuery : IQuery<int>
    {
        public int Value;
    }

    public class MyQueryHandler : IQueryHandler<MyQuery, int>
    {
        public ValueTask<int> Handle(MyQuery query, CancellationToken ct) 
        {
            return new(query.Value * 3);
        }
    }

    public class MyEvent : INotification
    {
        public int Count;
    }

    public class MyEventHandler1 : INotificationHandler<MyEvent>
    {
        public ValueTask Handle(MyEvent notification, CancellationToken ct)
        {
            notification.Count += 10;
            return default;
        }
    }

    [PublishStrategy(PublishStrategy.Parallel)]
    public class MyParallelEvent : INotification
    {
        public int Count;
    }

    public class MyParallelEventHandler1 : INotificationHandler<MyParallelEvent>
    {
        public async ValueTask Handle(MyParallelEvent notification, CancellationToken ct)
        {
            await Task.Delay(10);
            Interlocked.Add(ref notification.Count, 10);
        }
    }

    public class MyParallelEventHandler2 : INotificationHandler<MyParallelEvent>
    {
        public async ValueTask Handle(MyParallelEvent notification, CancellationToken ct)
        {
            await Task.Delay(10);
            Interlocked.Add(ref notification.Count, 20);
        }
    }

    [UseBehavior(typeof(MyNotificationBehavior))]
    public class MyPipelineEvent : INotification
    {
        public int Count;
    }

    public class MyPipelineEventHandler : INotificationHandler<MyPipelineEvent>
    {
        public ValueTask Handle(MyPipelineEvent notification, CancellationToken ct)
        {
            notification.Count += 10;
            return default;
        }
    }

    public class MyNotificationBehavior : INotificationBehavior<MyPipelineEvent>
    {
        public async ValueTask Handle<TNext>(MyPipelineEvent notification, TNext next, CancellationToken cancellationToken)
            where TNext : struct, INext
        {
            notification.Count += 2;
            await next.InvokeAsync();
            notification.Count += 2;
        }
    }

    public class MyStreamRequest : IStreamRequest<int>
    {
        public int Value;
    }

    public class MyStreamHandler : IStreamRequestHandler<MyStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(MyStreamRequest request, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield return request.Value;
            yield return request.Value * 2;
        }
    }
}
";
        var compilation = CreateCompilation(source);
        var generator = new MediatorSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        using var ms = new MemoryStream();
        var result = outputCompilation.Emit(ms);
        Assert.True(result.Success, "Compilation failed: " + string.Join("\n", result.Diagnostics));

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        var services = new ServiceCollection();

        // Call AddEricksonLopezMediator extension method
        var extType = assembly.GetType("Microsoft.Extensions.DependencyInjection.GeneratedMediatorExtensions")!;
        var addMediatorMethod = extType.GetMethod("AddEricksonLopezMediator")!;
        addMediatorMethod.Invoke(null, new object[] { services });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // 1. Test Command (with behavior)
        var cmdType = assembly.GetType("TestApp.MyCommand")!;
        var cmd = (ICommand<int>)Activator.CreateInstance(cmdType)!;
        cmdType.GetField("Value")!.SetValue(cmd, 5);
        var cmdResult = await mediator.Send(cmd);
        Assert.Equal(13, cmdResult); // (5+1)*2 + 1 = 13

        // 2. Test Query (without behavior)
        var qryType = assembly.GetType("TestApp.MyQuery")!;
        var qry = (IQuery<int>)Activator.CreateInstance(qryType)!;
        qryType.GetField("Value")!.SetValue(qry, 5);
        var qryResult = await mediator.Send(qry);
        Assert.Equal(15, qryResult); // 5 * 3

        // 3. Test Sequential Notification
        var evtType = assembly.GetType("TestApp.MyEvent")!;
        var evt = (INotification)Activator.CreateInstance(evtType)!;
        await mediator.Publish((dynamic)evt);
        Assert.Equal(10, evtType.GetField("Count")!.GetValue(evt));

        // 4. Test Parallel Notification
        var pEvtType = assembly.GetType("TestApp.MyParallelEvent")!;
        var pEvt = (INotification)Activator.CreateInstance(pEvtType)!;
        await mediator.Publish((dynamic)pEvt);
        Assert.Equal(30, pEvtType.GetField("Count")!.GetValue(pEvt));

        // 5. Test Notification Pipeline Behavior
        var plEvtType = assembly.GetType("TestApp.MyPipelineEvent")!;
        var plEvt = (INotification)Activator.CreateInstance(plEvtType)!;
        await mediator.Publish((dynamic)plEvt);
        Assert.Equal(14, plEvtType.GetField("Count")!.GetValue(plEvt));

        // 5. Test Stream Request
        var strType = assembly.GetType("TestApp.MyStreamRequest")!;
        var str = (IStreamRequest<int>)Activator.CreateInstance(strType)!;
        strType.GetField("Value")!.SetValue(str, 5);
        var stream = (IAsyncEnumerable<int>)mediator.CreateStream((dynamic)str);

        var list = new List<int>();
        await foreach (var item in stream)
        {
            list.Add(item);
        }
        Assert.Equal(2, list.Count);
        Assert.Equal(5, list[0]);
        Assert.Equal(10, list[1]);
    }
}







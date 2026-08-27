// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace EricksonLopez.Mediator.Generator;

/// <summary>
/// Provides analysis and model building capabilities for mediator components in a Roslyn compilation.
/// </summary>
public static class MediatorModelBuilder
{
    /// <summary>
    /// Builds the mediator model from the specified compilation and discovered types.
    /// </summary>
    /// <param name="compilation">The current Roslyn compilation.</param>
    /// <param name="types">The types discovered in the compilation that potentially represent mediator components.</param>
    /// <param name="context">The source production context used to report diagnostics.</param>
    /// <returns>A model representing the discovered mediator components.</returns>
    public static MediatorModel Build(Compilation compilation, List<INamedTypeSymbol> types, SourceProductionContext context)
    {
        var globalBehaviors = GetGlobalBehaviors(compilation);
        var externalTypes = GetExternalTypes(compilation);
        var allTypes = types.Concat(externalTypes).Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();

        var requestTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        var commandHandlers = new List<HandlerDefinition>();
        var queryHandlers = new List<HandlerDefinition>();
        var notificationHandlers = new List<HandlerDefinition>();
        var streamHandlers = new List<HandlerDefinition>();
        var allBehaviors = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
        var resultResponseTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var type in allTypes)
        {
            if (type.IsAbstract)
            {
                continue;
            }

            // ELM005: Open generic handlers are skipped — emit a warning so the developer is informed.
            if (type.IsGenericType)
            {
                // Only warn if the open generic type actually implements a mediator handler interface
                var implementsMediatorHandler = type.AllInterfaces.Any(i =>
                    i.ContainingNamespace.ToDisplayString() == "EricksonLopez.Mediator" &&
                    (i.Name == "ICommandHandler" || i.Name == "IQueryHandler" || i.Name == "INotificationHandler" || i.Name == "IStreamRequestHandler"));
                if (implementsMediatorHandler)
                {
                    ValidateOpenGenericHandler(type, context);
                }
                continue;
            }

            foreach (var iface in type.AllInterfaces)
            {
                if (iface.ContainingNamespace.ToDisplayString() == "EricksonLopez.Mediator")
                {
                    if (iface.Name == "ICommand" && iface.TypeArguments.Length == 1)
                    {
                        requestTypes.Add(type);
                    }
                    else if (iface.Name == "IQuery" && iface.TypeArguments.Length == 1)
                    {
                        requestTypes.Add(type);
                    }
                    else if (iface.Name == "ICommandHandler" && iface.TypeArguments.Length == 2)
                    {
                        var reqType = (INamedTypeSymbol)iface.TypeArguments[0];
                        var resType = (INamedTypeSymbol)iface.TypeArguments[1];
                        TrackResultResponse(resultResponseTypes, resType);

                        if (ValidateHandlerSignature(type, reqType, resType, context))
                        {
                            var behaviors = GetBehaviorsForHandler(type, reqType, resType, globalBehaviors, allBehaviors, context);
                            var lifetime = GetLifetime(type);
                            var validations = GetPropertyValidations(reqType);
                            commandHandlers.Add(new HandlerDefinition(type, reqType, resType, behaviors.ToArray(), lifetime, validations));
                        }
                    }
                    else if (iface.Name == "IQueryHandler" && iface.TypeArguments.Length == 2)
                    {
                        var reqType = (INamedTypeSymbol)iface.TypeArguments[0];
                        var resType = (INamedTypeSymbol)iface.TypeArguments[1];
                        TrackResultResponse(resultResponseTypes, resType);

                        if (ValidateHandlerSignature(type, reqType, resType, context))
                        {
                            var behaviors = GetBehaviorsForHandler(type, reqType, resType, globalBehaviors, allBehaviors, context);
                            var lifetime = GetLifetime(type);
                            var validations = GetPropertyValidations(reqType);
                            queryHandlers.Add(new HandlerDefinition(type, reqType, resType, behaviors.ToArray(), lifetime, validations));
                        }
                    }
                    else if (iface.Name == "INotificationHandler" && iface.TypeArguments.Length == 1)
                    {
                        var reqType = (INamedTypeSymbol)iface.TypeArguments[0];
                        if (ValidateNotificationHandlerSignature(type, reqType, context))
                        {
                            var lifetime = GetLifetime(type);
                            notificationHandlers.Add(new HandlerDefinition(type, reqType, reqType, Array.Empty<INamedTypeSymbol>(), lifetime));
                        }
                    }
                    else if (iface.Name == "IPipelineBehavior" && iface.TypeArguments.Length == 2)
                    {
                        ValidateBehaviorSignature(type, (INamedTypeSymbol)iface.TypeArguments[0], (INamedTypeSymbol)iface.TypeArguments[1], context);
                    }
                    else if (iface.Name == "IStreamRequest")
                    {
                        requestTypes.Add(type);
                    }
                    else if (iface.Name == "IStreamRequestHandler")
                    {
                        var reqType = (INamedTypeSymbol)iface.TypeArguments[0];
                        var resType = (INamedTypeSymbol)iface.TypeArguments[1];

                        if (ValidateStreamHandlerSignature(type, reqType, resType, context))
                        {
                            var lifetime = GetLifetime(type);
                            streamHandlers.Add(new HandlerDefinition(type, reqType, resType, Array.Empty<INamedTypeSymbol>(), lifetime));
                        }
                    }
                }
            }
        }

        var notificationGroups = notificationHandlers.GroupBy(h => h.RequestType, SymbolEqualityComparer.Default);
        var notifications = new List<NotificationDefinition>();
        foreach (var group in notificationGroups)
        {
            var reqType = (INamedTypeSymbol)group.Key!;
            var behaviors = GetBehaviorsForNotification(reqType, globalBehaviors, allBehaviors, context);
            var strategy = GetPublishStrategy(reqType);
            notifications.Add(new NotificationDefinition(reqType, behaviors.ToArray(), strategy, group.ToArray()));
        }

        ValidateSingleHandler(commandHandlers, compilation, context, "ELM002", "Multiple command handlers found for");
        ValidateSingleHandler(queryHandlers, compilation, context, "ELM003", "Multiple query handlers found for");

        ValidateMissingHandlers(commandHandlers, compilation, requestTypes.Where(t => t.AllInterfaces.Any(i => i.Name == "ICommand")), context);
        ValidateMissingHandlers(queryHandlers, compilation, requestTypes.Where(t => t.AllInterfaces.Any(i => i.Name == "IQuery")), context);
        ValidateMissingNotificationHandlers(notificationHandlers, compilation, allTypes.Where(t => t.AllInterfaces.Any(i => i.Name == "INotification")), context);
        ValidateSingleHandler(streamHandlers, compilation, context, "ELM010", "Multiple stream handlers found for");
        ValidateMissingStreamHandlers(streamHandlers, compilation, requestTypes.Where(t => t.AllInterfaces.Any(i => i.Name == "IStreamRequest")), context);

        var allBehaviorsList = allBehaviors.Select(kv => new BehaviorDefinition(kv.Key, kv.Value)).ToArray();

        return new MediatorModel(
            commandHandlers.ToArray(),
            queryHandlers.ToArray(),
            notificationHandlers.ToArray(),
            notifications.ToArray(),
            streamHandlers.ToArray(),
            new EquatableArray<BehaviorDefinition>(allBehaviorsList),
            resultResponseTypes.ToArray()
        );
    }

    private static bool ValidateHandlerSignature(INamedTypeSymbol handlerType, INamedTypeSymbol reqType, INamedTypeSymbol resType, SourceProductionContext context)
    {
        var handleMethod = handlerType.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.Parameters.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, reqType) &&
                m.Parameters[1].Type.Name == "CancellationToken" &&
                m.ReturnType.Name == "ValueTask" &&
                ((INamedTypeSymbol)m.ReturnType).TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(((INamedTypeSymbol)m.ReturnType).TypeArguments[0], resType)
            );

        if (handleMethod == null)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor("ELM004", "Invalid Handler Signature",
                    $"Handler {handlerType.ToDisplayString()} must have a method 'ValueTask<{resType.Name}> Handle({reqType.Name} command, CancellationToken cancellationToken)'.",
                    "Design", DiagnosticSeverity.Error, true),
                GetLocation(handlerType));
            context.ReportDiagnostic(diagnostic);
            return false;
        }
        return true;
    }

    private static bool ValidateNotificationHandlerSignature(INamedTypeSymbol handlerType, INamedTypeSymbol reqType, SourceProductionContext context)
    {
        var handleMethod = handlerType.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.Parameters.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, reqType) &&
                m.Parameters[1].Type.Name == "CancellationToken" &&
                m.ReturnType.Name == "ValueTask" &&
                ((INamedTypeSymbol)m.ReturnType).TypeArguments.Length == 0
            );

        if (handleMethod == null)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor("ELM004", "Invalid Handler Signature",
                    $"Notification handler {handlerType.ToDisplayString()} must have a method 'ValueTask Handle({reqType.Name} notification, CancellationToken cancellationToken)'.",
                    "Design", DiagnosticSeverity.Error, true),
                GetLocation(handlerType));
            context.ReportDiagnostic(diagnostic);
            return false;
        }
        return true;
    }

    private static void ValidateBehaviorSignature(INamedTypeSymbol behaviorType, INamedTypeSymbol reqType, INamedTypeSymbol resType, SourceProductionContext context)
    {
        // Validate that the behavior has a Handle method with correct parameter count.
        // Full generic TNext constraint is not checkable at symbol level without deeper resolution,
        // but we can check for obvious wrong signatures.
        var handleMethods = behaviorType.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length == 3)
            .ToList();

        if (handleMethods.Count == 0)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "ELM004",
                    "Invalid Behavior Signature",
                    $"Behavior {behaviorType.ToDisplayString()} must have a method 'ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<TResponse>'.",
                    "Design",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                GetLocation(behaviorType));
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void TrackResultResponse(HashSet<INamedTypeSymbol> resultResponseTypes, INamedTypeSymbol responseType)
    {
        if (GetContainingNamespace(responseType) == "EricksonLopez.Result" && responseType.Name == "Result")
        {
            resultResponseTypes.Add(responseType);
        }
    }

    private static string GetLifetime(INamedTypeSymbol handlerClass)
    {
        foreach (var attr in handlerClass.GetAttributes())
        {
            if (GetAttributeClassName(attr) == "ServiceLifetimeAttribute" && attr.ConstructorArguments.Length == 1)
            {
                var val = (int)attr.ConstructorArguments[0].Value!;
                return val switch
                {
                    0 => "Singleton",
                    1 => "Scoped",
                    _ => "Transient"
                };
            }
        }
        return "Transient";
    }

    private static string GetPublishStrategy(INamedTypeSymbol requestType)
    {
        foreach (var attr in requestType.GetAttributes())
        {
            if (GetAttributeClassName(attr) == "PublishStrategyAttribute" && attr.ConstructorArguments.Length == 1)
            {
                var val = (int)attr.ConstructorArguments[0].Value!;
                return val switch
                {
                    1 => "Parallel",
                    2 => "SequentialAggregateExceptions",
                    _ => "Sequential"
                };
            }
        }
        return "Sequential";
    }

    private static void ValidateMissingHandlers(List<HandlerDefinition> handlers, Compilation compilation, IEnumerable<INamedTypeSymbol> requests, SourceProductionContext context)
    {
        var handledRequestTypes = new HashSet<INamedTypeSymbol>(handlers.Select(h => h.RequestType), SymbolEqualityComparer.Default);

        foreach (var req in requests)
        {
            if (!handledRequestTypes.Contains(req))
            {
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "ELM001",
                        "Handler Not Found",
                        $"No handler found for {req.ToDisplayString()}",
                        "Design",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    GetLocation(req, compilation));

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void ValidateMissingNotificationHandlers(List<HandlerDefinition> handlers, Compilation compilation, IEnumerable<INamedTypeSymbol> requests, SourceProductionContext context)
    {
        var handledRequestTypes = new HashSet<INamedTypeSymbol>(handlers.Select(h => h.RequestType), SymbolEqualityComparer.Default);

        foreach (var req in requests)
        {
            if (!handledRequestTypes.Contains(req))
            {
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "ELM006",
                        "Notification Handler Not Found",
                        $"No handler found for notification {req.ToDisplayString()}. It will be ignored when published.",
                        "Design",
                        DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    GetLocation(req, compilation));

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void ValidateMissingStreamHandlers(List<HandlerDefinition> handlers, Compilation compilation, IEnumerable<INamedTypeSymbol> requests, SourceProductionContext context)
    {
        var handledRequestTypes = new HashSet<INamedTypeSymbol>(handlers.Select(h => h.RequestType), SymbolEqualityComparer.Default);

        foreach (var req in requests)
        {
            if (!handledRequestTypes.Contains(req))
            {
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "ELM009",
                        "Stream Handler Not Found",
                        $"No stream handler found for {req.ToDisplayString()}",
                        "Design",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    GetLocation(req, compilation));

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static List<INamedTypeSymbol> GetBehaviorsForHandler(INamedTypeSymbol handlerType, INamedTypeSymbol requestType, INamedTypeSymbol responseType, List<(INamedTypeSymbol Behavior, int Order)> globalBehaviors, Dictionary<INamedTypeSymbol, string> allBehaviors, SourceProductionContext context)
    {
        var behaviors = new List<(INamedTypeSymbol Behavior, int Order)>();

        foreach (var gb in globalBehaviors)
        {
            var closed = CloseBehaviorType(gb.Behavior, requestType, responseType, context);
            if (closed != null) behaviors.Add((closed, gb.Order));
        }

        foreach (var sb in GetSpecificBehaviors(requestType))
        {
            var closed = CloseBehaviorType(sb.Behavior, requestType, responseType, context);
            if (closed != null) behaviors.Add((closed, sb.Order));
        }

        // Sort by Order
        var sorted = behaviors.OrderBy(b => b.Order).ToList();

        // Detect ELM008: ordering conflict
        var groups = sorted.GroupBy(b => b.Order).Where(g => g.Count() > 1);
        foreach (var group in groups)
        {
            var conflictTypes = string.Join(", ", group.Select(b => b.Behavior.ToDisplayString()));
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor("ELM008", "Behavior Order Conflict",
                    $"Behaviors {conflictTypes} have the same order ({group.Key}). The execution order between them is not deterministic.",
                    "Design", DiagnosticSeverity.Warning, true),
                GetLocation(requestType));
            context.ReportDiagnostic(diagnostic);
        }

        var result = sorted.Select(b => b.Behavior).ToList();

        foreach (var b in result)
        {
            if (!allBehaviors.ContainsKey(b))
            {
                var lifetime = GetLifetime(b);
                allBehaviors.Add(b, lifetime);
            }
        }

        return result;
    }

    private static INamedTypeSymbol? CloseBehaviorType(INamedTypeSymbol behavior, INamedTypeSymbol request, INamedTypeSymbol response, SourceProductionContext context)
    {
        if (behavior.TypeParameters.Length > 0)
        {
            var original = behavior.OriginalDefinition;
            if (original.TypeParameters.Length == 2)
            {
                var reqParam = original.TypeParameters[0];
                foreach (var constraint in reqParam.ConstraintTypes)
                {
                    bool satisfies = request.AllInterfaces.Contains(constraint, SymbolEqualityComparer.Default) ||
                                      SymbolEqualityComparer.Default.Equals(request, constraint) ||
                                      SymbolEqualityComparer.Default.Equals(request.BaseType, constraint);
                    if (!satisfies)
                    {
                        return null;
                    }
                }
                return original.Construct(request, response);
            }
            else if (original.TypeParameters.Length == 1)
            {
                var reqParam = original.TypeParameters[0];
                foreach (var constraint in reqParam.ConstraintTypes)
                {
                    bool satisfies = request.AllInterfaces.Contains(constraint, SymbolEqualityComparer.Default) ||
                                      SymbolEqualityComparer.Default.Equals(request, constraint) ||
                                      SymbolEqualityComparer.Default.Equals(request.BaseType, constraint);
                    if (!satisfies)
                    {
                        return null;
                    }
                }
                return original.Construct(request);
            }
            else
            {
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "ELM007",
                        "Unsupported Open Generic Behavior",
                        $"Behavior {behavior.ToDisplayString()} has {original.TypeParameters.Length} type parameters. Only 2 are supported (TRequest, TResponse).",
                        "Design",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    GetLocation(behavior));
                context.ReportDiagnostic(diagnostic);
                return null;
            }
        }
        else
        {
            if (behavior.AllInterfaces.Any(i => i.Name == "IPipelineBehavior"))
            {
                return behavior;
            }
            return null;
        }
    }

    private static INamedTypeSymbol? CloseNotificationBehaviorType(INamedTypeSymbol behavior, INamedTypeSymbol notification, bool isGlobal, SourceProductionContext context)
    {
        if (behavior.TypeParameters.Length > 0)
        {
            var original = behavior.OriginalDefinition;
            if (original.TypeParameters.Length == 1)
            {
                var notifParam = original.TypeParameters[0];
                foreach (var constraint in notifParam.ConstraintTypes)
                {
                    bool satisfies = notification.AllInterfaces.Contains(constraint, SymbolEqualityComparer.Default) ||
                                      SymbolEqualityComparer.Default.Equals(notification, constraint) ||
                                      SymbolEqualityComparer.Default.Equals(notification.BaseType, constraint);
                    if (!satisfies)
                    {
                        return null;
                    }
                }
                return original.Construct(notification);
            }
            else
            {
                if (!isGlobal)
                {
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "ELM007",
                            "Unsupported Open Generic Behavior",
                            $"Behavior {behavior.ToDisplayString()} has {original.TypeParameters.Length} type parameters. Only 1 is supported (TNotification) for notifications.",
                            "Design",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true),
                        GetLocation(behavior));
                    context.ReportDiagnostic(diagnostic);
                }
                return null;
            }
        }
        else
        {
            if (behavior.AllInterfaces.Any(i => i.Name == "INotificationBehavior"))
            {
                return behavior;
            }
            return null;
        }
    }

    private static List<INamedTypeSymbol> GetBehaviorsForNotification(
        INamedTypeSymbol notificationType,
        List<(INamedTypeSymbol Behavior, int Order)> globalBehaviors,
        Dictionary<INamedTypeSymbol, string> allBehaviors,
        SourceProductionContext context)
    {
        var behaviors = new List<(INamedTypeSymbol Behavior, int Order, bool IsGlobal)>();
        foreach (var gb in globalBehaviors)
        {
            behaviors.Add((gb.Behavior, gb.Order, true));
        }

        foreach (var sb in GetSpecificBehaviors(notificationType))
        {
            behaviors.Add((sb.Behavior, sb.Order, false));
        }

        var sorted = behaviors.OrderBy(b => b.Order).ToList();
        var result = new List<INamedTypeSymbol>();

        foreach (var b in sorted)
        {
            var closed = CloseNotificationBehaviorType(b.Behavior, notificationType, b.IsGlobal, context);
            if (closed != null)
            {
                result.Add(closed);
                if (!allBehaviors.ContainsKey(closed))
                {
                    var lifetime = GetLifetime(b.Behavior);
                    allBehaviors.Add(closed, lifetime);
                }
            }
        }

        return result;
    }

    private static List<(INamedTypeSymbol Behavior, int Order)> GetGlobalBehaviors(Compilation compilation)
    {
        var behaviors = new List<(INamedTypeSymbol, int)>();
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            if (GetAttributeClassName(attribute) == "UseGlobalBehaviorAttribute" &&
                attribute.ConstructorArguments.ElementAtOrDefault(0).Value is INamedTypeSymbol behaviorType)
            {
                int order = attribute.ConstructorArguments.ElementAtOrDefault(1).Value is int o ? o : 0;
                behaviors.Add((behaviorType, order));
            }
        }
        return behaviors;
    }

    private static List<(INamedTypeSymbol Behavior, int Order)> GetSpecificBehaviors(INamedTypeSymbol requestType)
    {
        var behaviors = new List<(INamedTypeSymbol, int)>();
        foreach (var attribute in requestType.GetAttributes())
        {
            if (GetAttributeClassName(attribute) == "UseBehaviorAttribute" &&
                attribute.ConstructorArguments.ElementAtOrDefault(0).Value is INamedTypeSymbol behaviorType)
            {
                int order = attribute.ConstructorArguments.ElementAtOrDefault(1).Value is int o ? o : 0;
                behaviors.Add((behaviorType, order));
            }
        }
        return behaviors;
    }

    private static void ValidateSingleHandler(List<HandlerDefinition> handlers, Compilation compilation, SourceProductionContext context, string diagnosticId, string message)
    {
        var grouped = handlers.GroupBy(h => h.RequestType, SymbolEqualityComparer.Default);
        foreach (var group in grouped)
        {
            if (group.Count() > 1)
            {
                var requestType = (INamedTypeSymbol)group.Key!;

                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        diagnosticId,
                        "Multiple Handlers",
                        $"{message} {{0}}",
                        "Design",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    GetLocation(requestType, compilation),
                    requestType.ToDisplayString());

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>
    /// ELM005: Emits a warning when an open-generic handler is detected.
    /// Open-generic handlers (e.g. <c>class GenericHandler&lt;T&gt; : ICommandHandler&lt;T, Result&gt;</c>)
    /// are skipped by the source generator because they cannot be resolved to a concrete request type
    /// at compile time. This diagnostic informs the developer that such a handler will NOT be wired up.
    /// </summary>
    internal static void ValidateOpenGenericHandler(INamedTypeSymbol handlerType, SourceProductionContext context)
    {
        var diagnostic = Diagnostic.Create(
            new DiagnosticDescriptor(
                "ELM005",
                "Open Generic Handler Not Supported",
                $"Handler '{handlerType.ToDisplayString()}' is an open generic type and will NOT be registered by the source generator. " +
                $"Only concrete (closed) handler types are supported. " +
                $"Create a concrete handler that inherits from or wraps this generic handler if needed.",
                "Design",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true),
            GetLocation(handlerType));
        context.ReportDiagnostic(diagnostic);
    }

    private static bool ValidateStreamHandlerSignature(INamedTypeSymbol handlerType, INamedTypeSymbol reqType, INamedTypeSymbol resType, SourceProductionContext context)
    {
        var handleMethod = handlerType.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.Parameters.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, reqType) &&
                m.Parameters[1].Type.Name == "CancellationToken" &&
                m.ReturnType.Name == "IAsyncEnumerable" &&
                ((INamedTypeSymbol)m.ReturnType).TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(((INamedTypeSymbol)m.ReturnType).TypeArguments[0], resType)
            );

        if (handleMethod == null)
        {
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor("ELM011", "Invalid Stream Handler Signature",
                    $"Stream Handler {handlerType.ToDisplayString()} must have a method 'IAsyncEnumerable<{resType.Name}> Handle({reqType.Name} request, CancellationToken cancellationToken)'.",
                    "Design", DiagnosticSeverity.Error, true),
                GetLocation(handlerType));
            context.ReportDiagnostic(diagnostic);
            return false;
        }
        return true;
    }

    private static List<INamedTypeSymbol> GetExternalTypes(Compilation compilation)
    {
        var externalTypes = new List<INamedTypeSymbol>();
        var visitedAssemblies = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (GetAttributeClassName(attr) == "DiscoverHandlersAttribute" &&
                attr.ConstructorArguments.ElementAtOrDefault(0).Value is INamedTypeSymbol markerType &&
                markerType.ContainingAssembly is { } assembly &&
                visitedAssemblies.Add(assembly))
            {
                CollectTypes(assembly.GlobalNamespace, externalTypes);
            }
        }

        return externalTypes;
    }

    private static void CollectTypes(INamespaceSymbol namespaceSymbol, List<INamedTypeSymbol> collected)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            if (type.AllInterfaces.Any(i => i.ContainingNamespace.ToDisplayString() == "EricksonLopez.Mediator"))
            {
                collected.Add(type);
            }
            CollectTypes(type, collected);
        }

        foreach (var ns in namespaceSymbol.GetNamespaceMembers())
        {
            CollectTypes(ns, collected);
        }
    }

    private static void CollectTypes(INamedTypeSymbol parentType, List<INamedTypeSymbol> collected)
    {
        foreach (var nestedType in parentType.GetTypeMembers())
        {
            if (nestedType.AllInterfaces.Any(i => i.ContainingNamespace.ToDisplayString() == "EricksonLopez.Mediator"))
            {
                collected.Add(nestedType);
            }
            CollectTypes(nestedType, collected);
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static string? GetAttributeClassName(AttributeData attribute)
        => attribute.AttributeClass?.Name;

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static string? GetContainingNamespace(ISymbol symbol)
        => symbol.ContainingNamespace?.ToDisplayString();

    private static EquatableArray<PropertyValidation> GetPropertyValidations(INamedTypeSymbol requestType)
    {
        var validations = new List<PropertyValidation>();

        var properties = requestType.GetMembers().OfType<IPropertySymbol>();
        foreach (var prop in properties)
        {
            var propType = prop.Type.ToDisplayString();
            var propName = prop.Name;

            foreach (var attr in prop.GetAttributes())
            {
                var attrName = GetAttributeClassName(attr);

                if (attrName == "ValidateNotNullAttribute")
                {
                    string? msg = GetErrorMessage(attr, 0);
                    validations.Add(new PropertyValidation(propName, propType, "NotNull", msg, 0, 0, 0, 0, null));
                }
                else if (attrName == "ValidateNotEmptyAttribute")
                {
                    string? msg = GetErrorMessage(attr, 0);
                    validations.Add(new PropertyValidation(propName, propType, "NotEmpty", msg, 0, 0, 0, 0, null));
                }
                else if (attrName == "ValidateRangeAttribute")
                {
                    double min = attr.ConstructorArguments.ElementAtOrDefault(0).Value is double d1 ? d1 : 0;
                    double max = attr.ConstructorArguments.ElementAtOrDefault(1).Value is double d2 ? d2 : 0;
                    string? msg = GetErrorMessage(attr, 2);
                    validations.Add(new PropertyValidation(propName, propType, "Range", msg, min, max, 0, 0, null));
                }
                else if (attrName == "ValidateLengthAttribute")
                {
                    int min = attr.ConstructorArguments.ElementAtOrDefault(0).Value is int i1 ? i1 : 0;
                    int max = attr.ConstructorArguments.ElementAtOrDefault(1).Value is int i2 ? i2 : 0;
                    string? msg = GetErrorMessage(attr, 2);
                    validations.Add(new PropertyValidation(propName, propType, "Length", msg, 0, 0, min, max, null));
                }
                else if (attrName == "ValidateRegexAttribute")
                {
                    string? pattern = attr.ConstructorArguments.ElementAtOrDefault(0).Value as string;
                    string? msg = GetErrorMessage(attr, 1);
                    validations.Add(new PropertyValidation(propName, propType, "Regex", msg, 0, 0, 0, 0, pattern));
                }
            }
        }

        return new EquatableArray<PropertyValidation>(validations.ToArray());
    }

    private static string? GetErrorMessage(AttributeData attr, int ctorIndex)
    {
        if (attr.NamedArguments.FirstOrDefault(kv => kv.Key == "ErrorMessage").Value.Value is string namedMsg)
        {
            return namedMsg;
        }
        return attr.ConstructorArguments.ElementAtOrDefault(ctorIndex).Value as string;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static Location GetLocation(ISymbol symbol, Compilation? compilation = null)
    {
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef != null && (compilation == null || compilation.ContainsSyntaxTree(syntaxRef.SyntaxTree)))
        {
            return syntaxRef.GetSyntax().GetLocation();
        }
        var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource && l.SourceTree != null && (compilation == null || compilation.ContainsSyntaxTree(l.SourceTree)));
        return loc ?? Location.None;
    }
}







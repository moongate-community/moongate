using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using MessagePack;
using Moongate.Api.Attributes;
using Moongate.Api.Interfaces.Contracts;
using Moongate.Api.Interfaces.Handlers;
using Moongate.Api.Interfaces.Internal.Registry;
using Moongate.Api.Registry.Internal;

namespace Moongate.Api.Registry;

/// <summary>Registers typed operations during single-threaded composition, then freezes them for concurrent use.</summary>
public sealed class ApiRegistry
{
    private readonly Dictionary<ushort, IApiOperationRegistration> _operations = [];
    private readonly Dictionary<Type, IApiOperationRegistration> _requests = [];
    private ExceptionDispatchInfo? _freezeFailure;

    /// <summary>Gets the number of registered contracts.</summary>
    public int ContractCount => _operations.Count;

    /// <summary>Gets the number of registered incoming handlers.</summary>
    public int HandlerCount => _operations.Values.Count(operation => operation.HasHandler);

    /// <summary>Gets whether the registry no longer accepts changes.</summary>
    public bool IsFrozen { get; private set; }

    /// <summary>Resolves handler singletons once and seals registration. Failed resolution remains a failed registry.</summary>
    public void Freeze()
    {
        if (IsFrozen)
        {
            _freezeFailure?.Throw();

            return;
        }

        IsFrozen = true;

        try
        {
            foreach (var operation in _operations.Values)
            {
                operation.ResolveHandler();
            }
        }
        catch (Exception exception)
        {
            _freezeFailure = ExceptionDispatchInfo.Capture(exception);

            throw;
        }
    }

    /// <summary>Registers an outgoing request/response pair without an incoming handler.</summary>
    public void RegisterContract<TRequest, TResponse>() where TRequest : IApiRequest<TResponse>
    {
        EnsureMutable();
        var id = ValidateContract(typeof(TRequest), typeof(TResponse));
        var operation = new ApiOperationRegistration<TRequest, TResponse>(id);

        if (!_operations.TryAdd(id, operation))
        {
            throw new InvalidOperationException("An operation with this identifier is already registered.");
        }

        _requests.Add(typeof(TRequest), operation);
    }

    /// <summary>Registers a deferred singleton handler factory, deriving the request and response from its interface.</summary>
    public void RegisterHandler<THandler>(Func<THandler> factory) where THandler : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ValidateHandler(typeof(THandler));
        var types = HandlerContract(typeof(THandler)).GetGenericArguments();
        var id = ValidateContract(types[0], types[1]);
        var method =
            typeof(ApiRegistry).GetMethod(nameof(CreateRegistration), BindingFlags.NonPublic | BindingFlags.Static)!;
        var entry = (IApiOperationRegistration)method.MakeGenericMethod(types[0], types[1], typeof(THandler))
                                                     .Invoke(null, [id, factory])!;
        _operations[id] = entry;
        _requests[types[0]] = entry;
    }

    /// <summary>Validates a handler type and operation identity without changing the registry.</summary>
    public void ValidateHandler(Type handlerType)
    {
        EnsureMutable();
        var contract = HandlerContract(handlerType);
        var types = contract.GetGenericArguments();
        var id = ValidateContract(types[0], types[1]);

        if (_operations.TryGetValue(id, out var existing) &&
            (existing.HasHandler || existing.RequestType != types[0] || existing.ResponseType != types[1]))
        {
            throw new InvalidOperationException("An operation with this identifier is already registered.");
        }
    }

    internal IApiOperationRegistration Get<TRequest, TResponse>() where TRequest : IApiRequest<TResponse>
    {
        EnsureReady();

        if (!_requests.TryGetValue(typeof(TRequest), out var operation) || operation.ResponseType != typeof(TResponse))
        {
            throw new InvalidOperationException("The request contract is not registered.");
        }

        return operation;
    }

    internal bool TryGet(ushort operationId, [NotNullWhen(true)] out IApiOperationRegistration? operation)
    {
        EnsureReady();

        return _operations.TryGetValue(operationId, out operation);
    }

    private static IApiOperationRegistration CreateRegistration<TRequest, TResponse, THandler>(
        ushort id,
        Func<THandler> factory
    )
        where TRequest : IApiRequest<TResponse> where THandler : class, IApiHandler<TRequest, TResponse>
        => new ApiOperationRegistration<TRequest, TResponse>(id, () => factory());

    private void EnsureMutable()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("The API registry is frozen.");
        }
    }

    private void EnsureReady()
    {
        if (!IsFrozen)
        {
            throw new InvalidOperationException("The API registry must be frozen before use.");
        }

        _freezeFailure?.Throw();
    }

    private static Type HandlerContract(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        var contracts = handlerType.GetInterfaces()
                                   .Where(
                                       type => type.IsGenericType &&
                                               type.GetGenericTypeDefinition() == typeof(IApiHandler<,>)
                                   )
                                   .ToArray();

        if (!handlerType.IsClass || handlerType.IsAbstract || handlerType.ContainsGenericParameters || contracts.Length != 1)
        {
            throw new InvalidOperationException("A concrete handler must implement exactly one typed handler interface.");
        }

        return contracts[0];
    }

    private static ushort ValidateContract(Type requestType, Type responseType)
    {
        var attribute = requestType.GetCustomAttribute<ApiOperationAttribute>() ??
                        throw new InvalidOperationException("The request needs an explicit operation identifier.");
        var contracts = requestType.GetInterfaces()
                                   .Where(
                                       type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IApiRequest<>)
                                   )
                                   .ToArray();

        if (contracts.Length != 1 || contracts[0].GetGenericArguments()[0] != responseType)
        {
            throw new InvalidOperationException("The request must identify exactly one matching response type.");
        }

        ValidateDto(requestType);
        ValidateDto(responseType);

        return attribute.Id;
    }

    private static void ValidateDto(Type type)
    {
        var format = type.GetCustomAttribute<MessagePackObjectAttribute>();

        if (format is null || format.KeyAsPropertyName)
        {
            throw new InvalidOperationException("API contracts require explicit MessagePack integer keys.");
        }

        var keys = new HashSet<int>();

        foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public))
        {
            if (member is not (PropertyInfo or FieldInfo) || member.IsDefined(typeof(IgnoreMemberAttribute)))
            {
                continue;
            }

            var key = member.GetCustomAttribute<KeyAttribute>();

            if (key?.IntKey is not { } index || index < 0 || !keys.Add(index))
            {
                throw new InvalidOperationException("Every serialized contract member requires a nonnegative integer key.");
            }
        }
    }
}

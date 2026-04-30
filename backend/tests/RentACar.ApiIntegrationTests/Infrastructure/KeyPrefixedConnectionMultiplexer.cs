using System.Net;
using System.Reflection;
using StackExchange.Redis;
using StackExchange.Redis.Maintenance;
using StackExchange.Redis.Profiling;

namespace RentACar.ApiIntegrationTests.Infrastructure;

/// <summary>
/// Wraps a Redis connection and applies a fixed key prefix to every database access.
/// </summary>
internal sealed class KeyPrefixedConnectionMultiplexer : IConnectionMultiplexer
{
    private readonly IConnectionMultiplexer _inner;
    private readonly string _keyPrefix;

    public KeyPrefixedConnectionMultiplexer(IConnectionMultiplexer inner, string keyPrefix)
    {
        _inner = inner;
        _keyPrefix = keyPrefix;
    }

    public string ClientName => _inner.ClientName;
    public string Configuration => _inner.Configuration;
    public int TimeoutMilliseconds => _inner.TimeoutMilliseconds;
    public long OperationCount => _inner.OperationCount;

#pragma warning disable CS0618
    public bool PreserveAsyncOrder
    {
        get => _inner.PreserveAsyncOrder;
        set => _inner.PreserveAsyncOrder = value;
    }

    public bool IsConnected => _inner.IsConnected;
    public bool IsConnecting => _inner.IsConnecting;
    public bool IncludeDetailInExceptions
    {
        get => _inner.IncludeDetailInExceptions;
        set => _inner.IncludeDetailInExceptions = value;
    }
#pragma warning restore CS0618

    public int StormLogThreshold
    {
        get => _inner.StormLogThreshold;
        set => _inner.StormLogThreshold = value;
    }

    public event EventHandler<RedisErrorEventArgs>? ErrorMessage
    {
        add => _inner.ErrorMessage += value;
        remove => _inner.ErrorMessage -= value;
    }

    public event EventHandler<ConnectionFailedEventArgs>? ConnectionFailed
    {
        add => _inner.ConnectionFailed += value;
        remove => _inner.ConnectionFailed -= value;
    }

    public event EventHandler<InternalErrorEventArgs>? InternalError
    {
        add => _inner.InternalError += value;
        remove => _inner.InternalError -= value;
    }

    public event EventHandler<ConnectionFailedEventArgs>? ConnectionRestored
    {
        add => _inner.ConnectionRestored += value;
        remove => _inner.ConnectionRestored -= value;
    }

    public event EventHandler<EndPointEventArgs>? ConfigurationChanged
    {
        add => _inner.ConfigurationChanged += value;
        remove => _inner.ConfigurationChanged -= value;
    }

    public event EventHandler<EndPointEventArgs>? ConfigurationChangedBroadcast
    {
        add => _inner.ConfigurationChangedBroadcast += value;
        remove => _inner.ConfigurationChangedBroadcast -= value;
    }

    public event EventHandler<HashSlotMovedEventArgs>? HashSlotMoved
    {
        add => _inner.HashSlotMoved += value;
        remove => _inner.HashSlotMoved -= value;
    }

    public event EventHandler<ServerMaintenanceEvent>? ServerMaintenanceEvent
    {
        add => _inner.ServerMaintenanceEvent += value;
        remove => _inner.ServerMaintenanceEvent -= value;
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public ISubscriber GetSubscriber(object? asyncState = null) => _inner.GetSubscriber(asyncState);
    public int HashSlot(RedisKey key) => _inner.HashSlot(key);
    public int GetHashSlot(RedisKey key) => _inner.GetHashSlot(key);
    public void Wait(Task task) => _inner.Wait(task);
    public T Wait<T>(Task<T> task) => _inner.Wait(task);
    public void WaitAll(params Task[] tasks) => _inner.WaitAll(tasks);
    public IDatabase GetDatabase(int db = -1, object? asyncState = null) => KeyPrefixedDatabaseProxy.Create(_inner.GetDatabase(db, asyncState), _keyPrefix);
    public EndPoint[] GetEndPoints(bool configuredOnly = false) => _inner.GetEndPoints(configuredOnly);
    public IServer GetServer(string host, int port, object? asyncState = null) => _inner.GetServer(host, port, asyncState);
    public IServer GetServer(string hostAndPort, object? asyncState = null) => _inner.GetServer(hostAndPort, asyncState);
    public IServer GetServer(IPAddress host, int port) => _inner.GetServer(host, port);
    public IServer GetServer(EndPoint endpoint, object? asyncState = null) => _inner.GetServer(endpoint, asyncState);
    public IServer GetServer(RedisKey key, object? asyncState = null, CommandFlags flags = CommandFlags.None) => _inner.GetServer(key, asyncState, flags);
    public IServer[] GetServers() => _inner.GetServers();
    public long PublishReconfigure(CommandFlags flags = CommandFlags.None) => _inner.PublishReconfigure(flags);
    public Task<long> PublishReconfigureAsync(CommandFlags flags = CommandFlags.None) => _inner.PublishReconfigureAsync(flags);
    public int GetHashSlot(RedisChannel channel) => throw new NotSupportedException();
    public ServerCounters GetCounters() => _inner.GetCounters();
    public void ResetStormLog() => _inner.ResetStormLog();
    public string GetStormLog() => _inner.GetStormLog() ?? string.Empty;
    public void RegisterProfiler(Func<ProfilingSession?> profilingSessionProvider) => _inner.RegisterProfiler(profilingSessionProvider);
    public bool Configure(TextWriter? log = null) => _inner.Configure(log);
    public Task<bool> ConfigureAsync(TextWriter? log = null) => _inner.ConfigureAsync(log);
    public void ExportConfiguration(Stream destination, ExportOptions options = (ExportOptions)0) => _inner.ExportConfiguration(destination, options);
    public string GetStatus() => _inner.GetStatus() ?? string.Empty;
    public void GetStatus(TextWriter log) => _inner.GetStatus(log);
    public void Close(bool allowCommandsToComplete = true) => _inner.Close(allowCommandsToComplete);
    public Task CloseAsync(bool allowCommandsToComplete = true) => _inner.CloseAsync(allowCommandsToComplete);
    public void AddLibraryNameSuffix(string suffix) => _inner.AddLibraryNameSuffix(suffix);
    public override string ToString() => _inner.ToString() ?? nameof(KeyPrefixedConnectionMultiplexer);
}

internal class KeyPrefixedDatabaseProxy : DispatchProxy
{
    private IDatabase _target = null!;
    private string _keyPrefix = string.Empty;

    public static IDatabase Create(IDatabase target, string keyPrefix)
    {
        var proxy = Create<IDatabase, KeyPrefixedDatabaseProxy>();
        var databaseProxy = (KeyPrefixedDatabaseProxy)(object)proxy;
        databaseProxy._target = target;
        databaseProxy._keyPrefix = keyPrefix;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        var rewrittenArguments = args is null
            ? null
            : args.Select(RewriteArgument).ToArray();

        return targetMethod.Invoke(_target, rewrittenArguments);
    }

    private object? RewriteArgument(object? argument) => argument switch
    {
        RedisKey redisKey => Prefix(redisKey),
        RedisKey[] redisKeys => redisKeys.Select(Prefix).ToArray(),
        KeyValuePair<RedisKey, RedisValue>[] pairs => pairs
            .Select(pair => new KeyValuePair<RedisKey, RedisValue>(Prefix(pair.Key), pair.Value))
            .ToArray(),
        _ => argument
    };

    private RedisKey Prefix(RedisKey redisKey) => (RedisKey)$"{_keyPrefix}{redisKey}";
}

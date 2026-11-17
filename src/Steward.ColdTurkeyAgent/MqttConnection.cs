using System.Net.Sockets;
using System.Security.Authentication;
using MQTTnet;
using MQTTnet.Adapter;

namespace Steward.ColdTurkeyAgent;

public static class MqttConnection
{
    private sealed class BrokerRejectedException(string code) : IOException($"Broker rejected MQTT connection ({code}). Check authentication, client ID, protocol support and broker permissions.");
    public static async Task ConnectAsync(IMqttClient client, MqttClientOptions options, CancellationToken token)
    {
        var result = await client.ConnectAsync(options, token);
        if ((int)result.ResultCode != 0) throw new BrokerRejectedException(result.ResultCode.ToString());
    }

    public static MqttClientOptionsBuilder Options(AgentConfiguration configuration) 
    {
        var options = new MqttClientOptionsBuilder().WithTcpServer(configuration.Host, configuration.Port)
            .WithClientId(configuration.AgentId).WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311).WithCleanSession();
        if (configuration.Tls) options.WithTlsOptions(tls => tls.UseTls());
        if (configuration.Username is not null) options.WithCredentials(configuration.Username, configuration.Password);
        return options;
    }
    public static void Report(AgentConfiguration configuration)
    {
        Log.Write($"Connecting to MQTT: {Log.Value(configuration.Host)}:{configuration.Port}");
        Log.Detail($"TCP · TLS {(configuration.Tls ? "on" : "off")} · {(configuration.Username is null ? "no authentication" : "username/password authentication")}");
    }

    // Do not print arbitrary exception messages or broker-supplied text: these may contain credentials.
    public static string Failure(Exception exception)
    {
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
        {
            if (cause is BrokerRejectedException rejected) return rejected.Message;
            if (cause is MqttConnectingFailedException)
                return "Broker rejected MQTT connection. Check authentication, client ID, protocol support and broker permissions.";
            if (cause is SocketException socket)
                return $"Network failure ({socket.SocketErrorCode}). Check the hostname/IP, port, broker listener and network reachability."
                    + (OperatingSystem.IsMacOS() && socket.SocketErrorCode is SocketError.HostUnreachable or SocketError.NetworkUnreachable
                        ? " On macOS, check System Settings > Privacy & Security > Local Network for this agent. A Terminal test can succeed while a login agent is denied access." : "");
            if (cause is AuthenticationException)
                return "TLS handshake failed. Check the TLS port, certificate trust and hostname.";
            if (cause is OperationCanceledException or TimeoutException || cause.GetType().Name.Contains("Timeout", StringComparison.Ordinal))
                return "MQTT operation timed out. Check the broker address, port, TCP transport, TLS setting and network reachability.";
        }
        return $"MQTT operation failed ({exception.GetType().Name}). Compare host, port, TLS and authentication with your working MQTT client; check broker logs.";
    }
    public static async Task CheckAsync(AgentConfiguration configuration, CancellationToken token)
    {
        Report(configuration);
        using var client = new MqttClientFactory().CreateMqttClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await ConnectAsync(client, Options(configuration).Build(), timeout.Token);
            Log.Write("MQTT connection succeeded. This check does not publish registration or change Cold Turkey. Run the agent, then confirm registration in Steward.");
            Log.Detail("This tests the current process only; it does not verify the background agent's connection or write to agent.log.");
            if (OperatingSystem.IsMacOS())
                Log.Detail("Terminal and login agents can have different Local Network access. Check the background log separately.");
            await client.DisconnectAsync(cancellationToken: timeout.Token);
        }
        catch (Exception ex) when (!token.IsCancellationRequested) { throw new IOException(Failure(ex)); }
    }
}

using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MQTTnet;
using MQTTnet.Protocol;
using Steward.Messaging;
using Steward.Messaging.Messages;

namespace Steward.ColdTurkeyAgent;

public sealed class MqttAgent : IAsyncDisposable
{
    private readonly IMqttClient client = new MqttClientFactory().CreateMqttClient();
    private readonly AgentConfiguration configuration;
    private readonly GrantProcessor processor;
    private readonly IReadOnlyList<ResourceDefinition> resources;
    private readonly Channel<(string Topic, byte[] Payload, bool Retained)> inbox = Channel.CreateBounded<(string, byte[], bool)>(256);
    public MqttAgent(AgentConfiguration configuration, IReadOnlyList<ResourceDefinition> resources, GrantProcessor processor)
    {
        this.configuration = configuration;
        this.processor = processor;
        this.resources = resources;
        client.DisconnectedAsync += e =>
        {
            Log.Write($"MQTT disconnected ({e.Reason}). While running, the agent reconnects without changing CT state.");
            return Task.CompletedTask;
        };
        client.ApplicationMessageReceivedAsync += e =>
        {
            if (e.ApplicationMessage.Payload.Length > 65536) Log.Write("Ignored MQTT payload larger than 64 KiB.");
            else if (!inbox.Writer.TryWrite((e.ApplicationMessage.Topic, e.ApplicationMessage.Payload.ToArray(), e.ApplicationMessage.Retain)))
                Log.Write("Inbox full; message dropped without application acknowledgement.");
            return Task.CompletedTask;
        };
    }
    public async Task RunAsync(CancellationToken token)
    {
        var retryAt = DateTimeOffset.MinValue;
        var backoff = 1;
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!client.IsConnected && DateTimeOffset.UtcNow >= retryAt)
                {
                    try { await ConnectAsync(token); backoff = 1; }
                    catch (Exception ex) when (!token.IsCancellationRequested)
                    {
                        Log.Write($"{MqttConnection.Failure(ex)} Retry in {backoff}s.");
                        retryAt = DateTimeOffset.UtcNow.AddSeconds(backoff);
                        backoff = Math.Min(backoff * 2, 30);
                    }
                }
                if (inbox.Reader.TryRead(out var message))
                {
                    try
                    {
                        if (message.Topic == MqttTopics.AgentRefresh) await RegisterAsync(token);
                        else if (message.Topic == MqttTopics.AccessRequest(configuration.AgentId))
                        {
                            if (message.Retained) { Log.Write("Ignored retained access request."); continue; }
                            var request = StewardMessage.Deserialize<AccessRequestMessage>(Encoding.UTF8.GetString(message.Payload));
                            if (request is null) { Log.Write("Ignored null access request."); continue; }
                            Log.Break();
                            Log.Write($"Grant request: {Log.Value(request.RequestId)}");
                            Log.Detail($"Valid until {request.AllowedUntil.ToLocalTime():HH:mm:ss zzz}");
                            await processor.ProcessAsync(request, response => ReplyAsync(response, token), token);
                        }
                    }
                    catch (JsonException) { Log.Write("Ignored malformed request or missing required fields."); }
                    catch (Exception ex) when (!token.IsCancellationRequested) { Log.Write($"Message processing failed: {ex.GetType().Name}."); }
                }
                else await Task.Delay(50, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            // No CT changes here: completed allowances continue independently.
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                if (client.IsConnected)
                {
                    await PublishAsync(MqttTopics.AgentStatus(configuration.AgentId), new StatusMessage { State = AgentConnectionState.Offline }, true, shutdown.Token);
                    await client.DisconnectAsync(cancellationToken: shutdown.Token);
                }
            }
            catch (Exception ex) { Log.Write($"Offline notification failed ({ex.GetType().Name}); Last Will covers connection loss."); }
        }
    }
    private async Task ConnectAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        MqttConnection.Report(configuration);
        var options = MqttConnection.Options(configuration)
            .WithWillTopic(MqttTopics.AgentStatus(configuration.AgentId))
            .WithWillPayload(StewardMessage.Serialize(new StatusMessage { State = AgentConnectionState.Offline }))
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).WithWillRetain();
        await MqttConnection.ConnectAsync(client, options.Build(), timeout.Token);
        Log.Write("MQTT connected. Subscribing to Steward requests...");
        try
        {
            var subscription = await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(MqttTopics.AgentRefresh).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                .WithTopicFilter(f => f.WithTopic(MqttTopics.AccessRequest(configuration.AgentId)).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                .Build(), timeout.Token);
            if (subscription.Items.Any(i => (int)i.ResultCode >= 128))
            {
                Log.Write("Broker denied a Steward topic subscription. Check broker permissions for this agent's request topic and steward/agents/refresh.");
                throw new IOException("Broker denied a Steward topic subscription.");
            }

            await RegisterAsync(timeout.Token);
            await PublishAsync(MqttTopics.AgentStatus(configuration.AgentId), new StatusMessage { State = AgentConnectionState.Online }, true, timeout.Token);
            Log.Write("Ready for grants. Resource registration published.");
            Log.Detail("Confirm the agent appears in Steward; registration has no server acknowledgement.");
            Log.Break();
        }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try { await client.DisconnectAsync(cancellationToken: cleanup.Token); } catch { }
            throw;
        }
    }
    private Task RegisterAsync(CancellationToken token) => PublishAsync(MqttTopics.AgentRegister, new RegistrationMessage
    {
        Version = "0.1.0-alpha.8", AgentId = configuration.AgentId, InstanceId = configuration.InstanceId, Name = "Cold Turkey on " + AgentConfiguration.DeviceName,
        Devices = [new() { DeviceId = AgentConfiguration.DeviceId, Name = AgentConfiguration.DeviceName }],
        Resources = resources.Select(r => new ResourceAdvertisement { ResourceId = r.Id, Name = r.Name }).ToList()
    }, false, token);
    private async Task ReplyAsync(AccessResponseMessage response, CancellationToken token)
    {
        Log.Write($"{response.RequestStatus}: {response.Message}");
        Log.Detail($"Request: {Log.Value(response.RequestId)}");
        try { await PublishAsync(MqttTopics.AccessResponse(configuration.AgentId), response, false, token); }
        catch (Exception ex) { Log.Write($"Response not delivered request={Log.Value(response.RequestId)} ({ex.GetType().Name}); CT operation is not replayed."); }
    }
    private async Task PublishAsync<T>(string topic, T payload, bool retained, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(StewardMessage.Serialize(payload))
            .WithRetainFlag(retained).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), timeout.Token);
    }
    public ValueTask DisposeAsync() { client.Dispose(); return ValueTask.CompletedTask; }
}

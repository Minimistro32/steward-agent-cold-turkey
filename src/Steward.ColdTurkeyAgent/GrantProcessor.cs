using Steward.Messaging;
using Steward.Messaging.Messages;

namespace Steward.ColdTurkeyAgent;

public sealed class GrantProcessor(IReadOnlyList<ResourceDefinition> resources, IBlockController controller, Func<DateTimeOffset>? clock = null)
{
    private sealed record History(string Fingerprint, DateTimeOffset Expiry, AccessResponseMessage Result);
    private readonly Dictionary<string, History> history = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim serial = new(1, 1);
    private DateTimeOffset Now => clock?.Invoke() ?? DateTimeOffset.UtcNow;

    public async Task ProcessAsync(AccessRequestMessage request, Func<AccessResponseMessage, Task> reply, CancellationToken token)
    {
        await serial.WaitAsync(token);
        try { await ProcessCoreAsync(request, reply, token); }
        finally { serial.Release(); }
    }
    private async Task ProcessCoreAsync(AccessRequestMessage request, Func<AccessResponseMessage, Task> reply, CancellationToken token)
    {
        AccessResponseMessage Response(AccessRequestStatus status, string message) => new() { RequestId = request.RequestId, RequestStatus = status, Message = message };
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 128 || request.RequestId.Any(char.IsControl))
        { Log.Write("Ignored request without a trustworthy request ID."); return; }
        var fingerprint = StewardMessage.Serialize(new { Devices = request.DeviceIds?.Distinct().Order().ToArray(), Resources = request.ResourceIds?.Distinct().Order().ToArray(), request.AllowedUntil });
        if (history.TryGetValue(request.RequestId, out var prior))
        {
            await reply(prior.Fingerprint == fingerprint ? prior.Result : Response(AccessRequestStatus.Failed, "Request ID reused with different contents."));
            return;
        }
        var now = Now;
        if (request.DeviceIds is null || request.ResourceIds is null || request.DeviceIds.Any(id => id != AgentConfiguration.DeviceId)
            || request.ResourceIds.Any(id => resources.All(r => r.Id != id))
            || (request.ResourceIds.Count == 0 && !request.DeviceIds.Contains(AgentConfiguration.DeviceId)))
        { await reply(Response(AccessRequestStatus.Failed, "Unknown or missing targets.")); return; }
        if (request.AllowedUntil <= now)
        { await reply(Response(AccessRequestStatus.Failed, "Grant already expired; transit tolerance does not revive expired grants.")); return; }
        var targets = resources.Where(r => request.ResourceIds.Count == 0 || request.ResourceIds.Contains(r.Id)).ToArray();
        var budget = request.AllowedUntil - now + TimeSpan.FromSeconds(30);
        var selections = targets.Select(r => (Resource: r, Preset: r.Presets.Where(p => TimeSpan.FromMinutes(p.Minutes) <= budget).MaxBy(p => p.Minutes))).ToArray();
        if (selections.Any(s => s.Preset is null))
        { await reply(Response(AccessRequestStatus.Failed, "No preset fits one or more requested resources; nothing changed.")); return; }
        foreach (var id in history.Where(p => p.Value.Expiry <= now).Select(p => p.Key).ToArray()) history.Remove(id);
        if (history.Count >= 4096)
        { await reply(Response(AccessRequestStatus.Failed, "In-memory request history full; retry after outstanding requests expire.")); return; }
        // Mark uncertain BEFORE any CT mutation. Failed/interrupted grants are never automatically reset again.
        history.Add(request.RequestId, new(fingerprint, request.AllowedUntil, Response(AccessRequestStatus.Failed, "Previous application was interrupted or uncertain; not replayed.")));
        await reply(Response(AccessRequestStatus.Accepted, "Validated; applying Cold Turkey usage presets."));
        var applied = new List<string>();
        var failed = new List<string>();
        foreach (var selection in selections)
        {
            if (token.IsCancellationRequested) { failed.Add(selection.Resource.Id); continue; }
            var r = selection.Resource;
            var preset = selection.Preset!;
            Log.Write($"Apply {Log.Value(r.Name.Length == 0 ? r.Id : r.Name)} — {preset.Minutes} minute(s) of usage.");
            try
            {
                await controller.SetAsync(r.Baseline, true, token);
                foreach (var p in r.Presets) await controller.SetAsync(p.Block, false, token);
                await controller.SetAsync(preset.Block, true, token);
                // Do not open access after a long transition has crossed the server's grant deadline.
                if (Now >= request.AllowedUntil) throw new IOException("Grant expired during transition.");
                await controller.SetAsync(r.Baseline, false, token);
                applied.Add(r.Id);
            }
            catch (Exception ex)
            {
                failed.Add(r.Id);
                Log.Write($"Failed to apply {Log.Value(r.Id)}: {Log.Value(ex.Message)}. Restoring baseline.");
                // Recovery is bounded and independent of shutdown cancellation. Never restart an allowance here.
                using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(ColdTurkeyCli.CommandTimeoutSeconds * 2 + 1));
                try { await controller.SetAsync(r.Baseline, true, recovery.Token); }
                catch (Exception recoveryError) { Log.Write($"BASELINE UNCONFIRMED resource={Log.Value(r.Id)} reason={Log.Value(recoveryError.Message)}; inspect CT manually."); }
            }
        }
        var summaries = new List<string>();
        if (applied.Count > 0) summaries.Add($"Applied resources: {string.Join(", ", applied)}.");
        if (failed.Count > 0) summaries.Add($"Failed resources: {string.Join(", ", failed)}.");
        if (applied.Count > 0) summaries.Add("Allowances measure usage minutes, not wall-clock expiry.");
        var result = Response(failed.Count == 0 ? AccessRequestStatus.Completed : AccessRequestStatus.Failed, string.Join(" ", summaries));
        history[request.RequestId] = new(fingerprint, request.AllowedUntil, result);
        await reply(result);
    }
}

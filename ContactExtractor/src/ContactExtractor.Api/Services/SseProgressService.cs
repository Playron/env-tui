using System.Collections.Concurrent;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ContactExtractor.Api.Services;

/// <summary>
/// Singleton-tjeneste som formidler fremdriftshendelser fra ExtractionConsumer til SSE-endepunktet.
/// Løser race condition ved sen tilkobling: terminal-event lagres og leveres umiddelbart.
/// </summary>
public sealed class SseProgressService
{
    private sealed record SessionState(
        Channel<SseProgressEvent> Channel,
        SseProgressEvent? FinalEvent);

    private readonly ConcurrentDictionary<Guid, SessionState> _sessions = new();

    /// <summary>Opprettes av consumer ved start av prosessering.</summary>
    public void Create(Guid sessionId)
    {
        var channel = Channel.CreateUnbounded<SseProgressEvent>(
            new UnboundedChannelOptions { SingleReader = true });
        _sessions[sessionId] = new SessionState(channel, null);
    }

    /// <summary>Publiserer en fremdriftshendelse. Ignoreres hvis sesjonen er fullført.</summary>
    public void Publish(Guid sessionId, SseProgressEvent evt)
    {
        if (_sessions.TryGetValue(sessionId, out var state) && state.FinalEvent is null)
            state.Channel.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Markerer sesjonen som ferdig. Terminal-event lagres for sen tilkobling.
    /// Kalles med "done" eller "failed" event.
    /// </summary>
    public void Complete(Guid sessionId, SseProgressEvent finalEvent)
    {
        if (!_sessions.TryGetValue(sessionId, out var state)) return;

        state.Channel.Writer.TryWrite(finalEvent);
        state.Channel.Writer.TryComplete();
        // Atomisk oppdatering med FinalEvent satt – sen klient ser dette
        _sessions[sessionId] = new SessionState(state.Channel, finalEvent);
    }

    /// <summary>Returnerer true hvis sesjonen finnes i minnet (aktiv eller nylig fullført).</summary>
    public bool Exists(Guid sessionId) => _sessions.ContainsKey(sessionId);

    /// <summary>
    /// Strømmer SSE-hendelser for en sesjon som <see cref="IAsyncEnumerable{T}"/>.
    /// Brukes med <c>TypedResults.ServerSentEvents(...)</c> i .NET 10.
    /// - Sesjonen finnes ikke: ingen events (yield break)
    /// - Allerede fullført: leverer terminal-event umiddelbart
    /// - Aktiv: strømmer events fra channel til "done" eller "failed"
    /// </summary>
    public async IAsyncEnumerable<SseItem<SseProgressEvent>> StreamAsync(
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            yield break;

        if (state.FinalEvent is not null)
        {
            yield return Wrap(state.FinalEvent);
            yield break;
        }

        await foreach (var evt in state.Channel.Reader.ReadAllAsync(ct))
        {
            yield return Wrap(evt);
            if (evt.Stage is "done" or "failed") break;
        }
    }

    /// <summary>
    /// Fjerner sesjonen fra minnet. Kalles med forsinkelse etter at SSE-klienten er ferdig,
    /// slik at eventuelle forsinkede tilkoblinger fortsatt får FinalEvent.
    /// </summary>
    public void Remove(Guid sessionId) => _sessions.TryRemove(sessionId, out _);

    private static SseItem<SseProgressEvent> Wrap(SseProgressEvent evt) =>
        new(evt, evt.Stage);
}

public record SseProgressEvent(
    Guid SessionId,
    string Stage,               // "pending" | "extracting" | "regex_done" | "ai_started" | "ai_complete" | "done" | "failed"
    string Message,             // Brukerlesbar melding
    int? ContactsFoundSoFar,
    double? Progress);          // 0.0 – 1.0

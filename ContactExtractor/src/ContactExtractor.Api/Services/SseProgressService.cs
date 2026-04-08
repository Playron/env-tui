using System.Collections.Concurrent;
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

    /// <summary>
    /// Returnerer reader for SSE-endepunktet.
    /// - reader == null: sesjonen finnes ikke (ukjent sessionId)
    /// - immediateEvent != null: sesjonen er allerede fullført – lever event umiddelbart
    /// - reader != null og immediateEvent == null: koble til aktiv strøm
    /// </summary>
    public (ChannelReader<SseProgressEvent>? Reader, SseProgressEvent? ImmediateEvent)
        GetReader(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            return (null, null);

        return (state.Channel.Reader, state.FinalEvent);
    }

    /// <summary>
    /// Fjerner sesjonen fra minnet. Kalles med forsinkelse etter at SSE-klienten er ferdig,
    /// slik at eventuelle forsinkede tilkoblinger fortsatt får FinalEvent.
    /// </summary>
    public void Remove(Guid sessionId) => _sessions.TryRemove(sessionId, out _);
}

public record SseProgressEvent(
    Guid SessionId,
    string Stage,               // "pending" | "extracting" | "regex_done" | "ai_started" | "ai_complete" | "done" | "failed"
    string Message,             // Brukerlesbar melding
    int? ContactsFoundSoFar,
    double? Progress);          // 0.0 – 1.0

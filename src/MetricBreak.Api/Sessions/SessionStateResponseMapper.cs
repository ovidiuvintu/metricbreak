namespace MetricBreak.Api.Sessions;

public static class SessionStateResponseMapper
{
    public static SessionStateResponse Map(
        ReplaySessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sample = snapshot.CurrentSample;

        return new SessionStateResponse(
            SessionId: snapshot.SessionId,
            RecordingId: snapshot.RecordingId,
            RecordingName: snapshot.RecordingName,
            SampleCount: snapshot.SampleCount,
            ExecutionState:
                snapshot.ExecutionState.Status.ToString(),
            PauseReason:
                snapshot.ExecutionState.PauseReason.ToString(),
            CurrentSample: new CurrentSampleResponse(
                Sequence: sample.SequenceNumber,
                TimestampNanoseconds:
                    sample.Timestamp.UnixNanoseconds,
                Channels: sample.Values));
    }
}

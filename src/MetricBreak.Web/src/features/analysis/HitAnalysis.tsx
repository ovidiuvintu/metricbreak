import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";

import { recordingCatalogApi, type RecordingCatalogItem } from "../recordings/recordingCatalogApi";
import type { SessionState } from "../recordings/sessionTypes";
import { analysisApi } from "./analysisApi";
import type {
  AnalysisReading,
  ConditionRun,
  EventAnalysis,
  RecordingComparison,
} from "./analysisTypes";

const defaultLookback = 10;
const maxLookback = 10_000;

interface EventAnalysisSectionProps {
  session: SessionState;
  breakpointKey: string;
  onInspectSample: (sequence: number) => void;
}

export function EventAnalysisSection({
  session,
  breakpointKey,
  onInspectSample,
}: EventAnalysisSectionProps) {
  const sequence = session.currentSample?.sequence ?? 0;
  const [lookbackInput, setLookbackInput] = useState(String(defaultLookback));
  const [lookback, setLookback] = useState(defaultLookback);
  const [analysis, setAnalysis] = useState<EventAnalysis | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (session.sampleCount < 1) {
      setAnalysis(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);
    void analysisApi.getEvent(session.sessionId, sequence, lookback)
      .then((result) => {
        if (!cancelled) setAnalysis(result);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setAnalysis(null);
        setError(err instanceof Error ? err.message : "Unable to inspect this sample.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [session.sessionId, session.sampleCount, sequence, lookback, breakpointKey]);

  const applyLookback = () => {
    const parsed = Number(lookbackInput);
    if (!Number.isInteger(parsed) || parsed < 0 || parsed > maxLookback) {
      setError(`Enter a lookback from 0 to ${maxLookback.toLocaleString()} samples.`);
      return;
    }
    setError(null);
    setLookback(parsed);
  };

  return (
    <Box>
      <Typography variant="h6">At the replay cursor</Typography>
      <Typography variant="body2" color="text.secondary">
        Sample {sequence.toLocaleString()}
        {analysis
          ? ` · ${formatTimestamp(analysis.eventTimestampNanoseconds, session.timestampOrigin)}`
          : ""}
        . These answers follow the cursor.
      </Typography>
      {error && <Alert severity="error" sx={{ mt: 1 }}>{error}</Alert>}

      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center", mt: 1 }}>
        <TextField
          size="small"
          label="Samples before cursor"
          value={lookbackInput}
          onChange={(event) => setLookbackInput(event.target.value)}
          sx={{ width: 180 }}
        />
        <Button size="small" variant="outlined" onClick={applyLookback} disabled={loading}>
          Update lookback
        </Button>
      </Stack>

      {analysis && (
        <Stack spacing={2} sx={{ mt: 2 }}>
          <Box>
            <Typography variant="subtitle2">What changed immediately before this sample?</Typography>
            {analysis.precedingSequence == null ? (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                This is the first sample, so nothing precedes it.
              </Typography>
            ) : analysis.immediateChanges.length === 0 ? (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                No channel changed between{" "}
                <SequenceLink sequence={analysis.precedingSequence} onInspectSample={onInspectSample} />
                {" "}and this sample.
              </Typography>
            ) : (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Channel</TableCell>
                    <TableCell>Previous</TableCell>
                    <TableCell>At this sample</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {analysis.immediateChanges.map((change) => (
                    <TableRow key={change.channelId} hover>
                      <TableCell>{channelLabel(change.name, change.unit)}</TableCell>
                      <TableCell>
                        {formatReading(change.previous)}
                        {" "}
                        <SequenceLink sequence={change.previousSequence} onInspectSample={onInspectSample} />
                      </TableCell>
                      <TableCell>
                        {formatReading(change.current)}
                        {" "}
                        <SequenceLink sequence={change.eventSequence} onInspectSample={onInspectSample} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </Box>

          <Box>
            <Typography variant="subtitle2">Which channels moved first?</Typography>
            <Typography variant="caption" color="text.secondary">
              Earliest departure from the value at sample {analysis.lookbackStartSequence.toLocaleString()},
              through this sample. The first row moved first.
            </Typography>
            {analysis.firstMoves.length === 0 ? (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                {lookback === 0
                  ? "Set a lookback above zero to order the lead-up."
                  : "No channel changed in this lookback."}
              </Typography>
            ) : (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Order</TableCell>
                    <TableCell>Channel</TableCell>
                    <TableCell>From</TableCell>
                    <TableCell>To</TableCell>
                    <TableCell>First change</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {analysis.firstMoves.map((move, index) => (
                    <TableRow key={move.channelId} hover>
                      <TableCell>{index + 1}</TableCell>
                      <TableCell>{channelLabel(move.name, move.unit)}</TableCell>
                      <TableCell>{formatReading(move.baseline)}</TableCell>
                      <TableCell>{formatReading(move.value)}</TableCell>
                      <TableCell>
                        <SequenceLink sequence={move.firstChangeSequence} onInspectSample={onInspectSample} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </Box>

          <Box>
            <Typography variant="subtitle2">State of all channels at this sample</Typography>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Channel</TableCell>
                  <TableCell>Value</TableCell>
                  <TableCell>Sample</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {analysis.states.map((state) => (
                  <TableRow key={state.channelId} hover>
                    <TableCell>{channelLabel(state.name, state.unit)}</TableCell>
                    <TableCell>{formatReading(state.value)}</TableCell>
                    <TableCell>
                      <SequenceLink sequence={state.sequence} onInspectSample={onInspectSample} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>

          <Box>
            <Typography variant="subtitle2">Did this condition persist?</Typography>
            <Typography variant="caption" color="text.secondary">
              Length of the run that contains this sample. Open the start sample to jump to where the condition became true.
            </Typography>
            {analysis.conditionRuns.length === 0 ? (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                No breakpoints are configured.
              </Typography>
            ) : (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Breakpoint</TableCell>
                    <TableCell>At this sample</TableCell>
                    <TableCell>Run</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {analysis.conditionRuns.map((run) => (
                    <TableRow key={run.breakpointId} hover>
                      <TableCell>{run.label}</TableCell>
                      <TableCell>{run.matchesAtEvent ? "Holds" : "Does not hold"}{run.isEnabled ? "" : " · disabled"}</TableCell>
                      <TableCell>{renderRun(run, onInspectSample)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </Box>
        </Stack>
      )}
    </Box>
  );
}

interface RecordingComparisonSectionProps {
  session: SessionState;
  onInspectSample: (sequence: number) => void;
}

export function RecordingComparisonSection({
  session,
  onInspectSample,
}: RecordingComparisonSectionProps) {
  const [recordings, setRecordings] = useState<RecordingCatalogItem[]>([]);
  const [catalogLoaded, setCatalogLoaded] = useState(false);
  const [referenceId, setReferenceId] = useState("");
  const [comparison, setComparison] = useState<RecordingComparison | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setCatalogLoaded(false);
    void recordingCatalogApi.list()
      .then((response) => {
        if (!cancelled) setRecordings(response.recordings);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Unable to list recordings.");
        }
      })
      .finally(() => {
        if (!cancelled) setCatalogLoaded(true);
      });
    return () => {
      cancelled = true;
    };
  }, [session.recordingId]);

  useEffect(() => {
    setComparison(null);
    setReferenceId("");
    setError(null);
  }, [session.sessionId]);

  const choices = recordings.filter((item) => item.recordingId !== session.recordingId);

  const compare = () => {
    if (!referenceId) return;
    setLoading(true);
    setError(null);
    void analysisApi.compareRecording(session.sessionId, referenceId)
      .then((result) => setComparison(result))
      .catch((err: unknown) => {
        setComparison(null);
        setError(err instanceof Error ? err.message : "Unable to compare recordings.");
      })
      .finally(() => setLoading(false));
  };

  const mismatches = comparison?.channels.filter((channel) => channel.mismatchCount > 0) ?? [];
  const matches = (comparison?.channels.length ?? 0) - mismatches.length;

  return (
    <Box>
      <Typography variant="h6">Compare with a known-good recording</Typography>
      <Typography variant="body2" color="text.secondary">
        Channels with the same name are aligned by sample number. A difference opens that sample on this recording.
      </Typography>
      {error && <Alert severity="error" sx={{ mt: 1 }}>{error}</Alert>}
      {!catalogLoaded ? null : choices.length === 0 ? (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
          Import another recording to use it as the known-good reference.
        </Typography>
      ) : (
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center", mt: 1 }}>
          <TextField
            select
            size="small"
            label="Known-good recording"
            value={referenceId}
            onChange={(event) => setReferenceId(event.target.value)}
            sx={{ minWidth: 260 }}
          >
            {choices.map((item) => (
              <MenuItem key={item.recordingId} value={item.recordingId}>
                {item.name} · {item.sampleCount.toLocaleString()} samples
              </MenuItem>
            ))}
          </TextField>
          <Button size="small" variant="contained" onClick={compare} disabled={!referenceId || loading}>
            Compare
          </Button>
        </Stack>
      )}

      {comparison && (
        <Box sx={{ mt: 1.5 }}>
          <Typography variant="body2">
            Compared the first {comparison.comparedSampleCount.toLocaleString()} samples
            {" "}of {comparison.referenceName}. This recording has {comparison.subjectSampleCount.toLocaleString()} samples.
            The known-good recording has {comparison.referenceSampleCount.toLocaleString()}.
            {" "}
            {matches === comparison.channels.length
              ? "Every shared channel matches."
              : `${matches.toLocaleString()} shared ${matches === 1 ? "channel matches" : "channels match"}.`}
          </Typography>
          {comparison.onlyInSubject.length > 0 && (
            <Typography variant="body2" color="text.secondary">
              Only in this recording: {comparison.onlyInSubject.join(", ")}
            </Typography>
          )}
          {comparison.onlyInReference.length > 0 && (
            <Typography variant="body2" color="text.secondary">
              Only in the known-good recording: {comparison.onlyInReference.join(", ")}
            </Typography>
          )}
          {mismatches.length > 0 && (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Channel</TableCell>
                  <TableCell align="right">Mismatches</TableCell>
                  <TableCell>First difference</TableCell>
                  <TableCell align="right">Largest numeric gap</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {mismatches.map((channel) => (
                  <TableRow key={channel.channelId} hover>
                    <TableCell>{channelLabel(channel.name, channel.unit)}</TableCell>
                    <TableCell align="right">
                      {channel.mismatchCount.toLocaleString()} / {channel.comparedCount.toLocaleString()}
                    </TableCell>
                    <TableCell>
                      <SequenceLink sequence={channel.firstMismatchSequence} onInspectSample={onInspectSample} />
                    </TableCell>
                    <TableCell align="right">
                      {channel.maxAbsoluteDelta == null
                        ? "—"
                        : (
                          <>
                            {channel.maxAbsoluteDelta.toLocaleString()}
                            {" at "}
                            <SequenceLink
                              sequence={channel.maxAbsoluteDeltaSequence}
                              onInspectSample={onInspectSample}
                            />
                          </>
                        )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Box>
      )}
    </Box>
  );
}

function renderRun(
  run: ConditionRun,
  onInspectSample: (sequence: number) => void,
) {
  if (!run.matchesAtEvent || run.sampleCount == null || run.startSequence == null || run.endSequence == null) {
    return "—";
  }

  const span = formatSpan(run.sampleCount, run.durationNanoseconds);
  if (run.startSequence === run.endSequence) {
    return (
      <>
        {span}
        {" · "}
        <SequenceLink sequence={run.startSequence} onInspectSample={onInspectSample} />
      </>
    );
  }

  return (
    <>
      {span}
      {" · "}
      <SequenceLink sequence={run.startSequence} onInspectSample={onInspectSample} />
      {"–"}
      <SequenceLink sequence={run.endSequence} onInspectSample={onInspectSample} />
    </>
  );
}

function formatSpan(samples: number, nanoseconds: string | null): string {
  const sampleLabel = samples === 1 ? "1 sample" : `${samples.toLocaleString()} samples`;
  const duration = nanoseconds ? formatDuration(nanoseconds) : "";
  return duration.length === 0 ? sampleLabel : `${sampleLabel} · ${duration}`;
}

function formatDuration(nanoseconds: string): string {
  let value: bigint;
  try {
    value = BigInt(nanoseconds);
  } catch {
    return "";
  }
  if (value <= 0n) return "";

  const seconds = value / 1_000_000_000n;
  const millis = (value % 1_000_000_000n) / 1_000_000n;
  if (seconds < 10n) {
    if (millis === 0n) return `${seconds}s`;
    const fraction = millis.toString().padStart(3, "0").replace(/0+$/, "");
    return `${seconds}.${fraction}s`;
  }
  if (seconds < 60n) return `${seconds}s`;
  const minutes = seconds / 60n;
  const remainder = seconds % 60n;
  if (minutes < 60n) return remainder === 0n ? `${minutes}m` : `${minutes}m ${remainder}s`;
  const hours = minutes / 60n;
  const mins = minutes % 60n;
  return mins === 0n ? `${hours}h` : `${hours}h ${mins}m`;
}

function formatReading(reading: AnalysisReading): string {
  if (reading.valueType === "Numeric") {
    return reading.numericValue == null || !Number.isFinite(reading.numericValue)
      ? "N/A"
      : reading.numericValue.toLocaleString();
  }
  if (reading.valueType === "Boolean") return reading.booleanValue ? "True" : "False";
  if (reading.valueType === "Categorical") return reading.text ?? "N/A";
  return "N/A";
}

function channelLabel(name: string, unit: string | null): string {
  const trimmed = unit?.trim();
  return trimmed ? `${name} (${trimmed})` : name;
}

function formatTimestamp(nanoseconds: string, origin: SessionState["timestampOrigin"]): string {
  try {
    const value = BigInt(nanoseconds);
    if (origin === "Elapsed") {
      const negative = value < 0n;
      const magnitude = negative ? -value : value;
      const seconds = magnitude / 1_000_000_000n;
      return `${negative ? "-" : ""}${seconds.toString()} s`;
    }
    const date = new Date(Number(value / 1_000_000n));
    return Number.isNaN(date.getTime()) ? nanoseconds : date.toISOString();
  } catch {
    return nanoseconds;
  }
}

function SequenceLink({
  sequence,
  onInspectSample,
}: {
  sequence: number | null;
  onInspectSample: (sequence: number) => void;
}) {
  if (sequence == null) return <span>—</span>;
  return (
    <Button
      size="small"
      sx={{ minWidth: 0, p: 0, verticalAlign: "baseline" }}
      onClick={() => onInspectSample(sequence)}
    >
      #{sequence.toLocaleString()}
    </Button>
  );
}

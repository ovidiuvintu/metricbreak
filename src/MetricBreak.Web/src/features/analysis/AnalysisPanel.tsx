import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  FormControlLabel,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";

import type { ChannelMetadata } from "../recordings/channelMetadata";
import type { SessionState } from "../recordings/sessionTypes";
import { analysisApi } from "./analysisApi";
import type { ChannelWindowSummary, WindowAnalysis } from "./analysisTypes";

interface AnalysisPanelProps {
  session: SessionState | null;
  channels: ChannelMetadata[];
  selectedChannelIds: string[];
  onInspectSample: (sequence: number) => void;
}

const visibleComparisons = 8;

export function AnalysisPanel({
  session,
  channels,
  selectedChannelIds,
  onInspectSample,
}: AnalysisPanelProps) {
  const [startInput, setStartInput] = useState("0");
  const [endInput, setEndInput] = useState("0");
  const [chartChannelsOnly, setChartChannelsOnly] = useState(false);
  const [analysis, setAnalysis] = useState<WindowAnalysis | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (
    start: number,
    end: number,
    chartOnly: boolean,
  ) => {
    if (!session) return;

    const channelIds = chartOnly ? selectedChannelIds : undefined;
    if (chartOnly && selectedChannelIds.length === 0) {
      setAnalysis(null);
      setError("Select one or more channels on the Recordings chart.");
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const result = await analysisApi.getWindow(
        session.sessionId,
        start,
        end,
        channelIds,
      );
      setAnalysis(result);
    } catch (err) {
      setAnalysis(null);
      setError(err instanceof Error ? err.message : "Unable to analyze this window.");
    } finally {
      setLoading(false);
    }
  }, [selectedChannelIds, session]);

  useEffect(() => {
    if (!session || session.sampleCount < 1) {
      setAnalysis(null);
      return;
    }

    const end = session.sampleCount - 1;
    setChartChannelsOnly(false);
    setStartInput("0");
    setEndInput(String(end));
    void load(0, end, false);
  }, [session?.sessionId, session?.sampleCount]);

  if (!session) {
    return (
      <Box sx={{ p: 2 }}>
        <Typography variant="h6">Analysis</Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          Import a recording to summarize channels over a sample window.
        </Typography>
      </Box>
    );
  }

  const lastSequence = Math.max(0, session.sampleCount - 1);
  const cursor = session.currentSample?.sequence ?? 0;

  const applyWindow = (start: number, end: number) => {
    const clampedStart = Math.max(0, Math.min(start, lastSequence));
    const clampedEnd = Math.max(clampedStart, Math.min(end, lastSequence));
    setStartInput(String(clampedStart));
    setEndInput(String(clampedEnd));
    void load(clampedStart, clampedEnd, chartChannelsOnly);
  };

  const updateFromInputs = () => {
    const start = Number(startInput);
    const end = Number(endInput);
    if (!Number.isInteger(start) || !Number.isInteger(end)) {
      setError("Enter whole sample sequences.");
      return;
    }
    applyWindow(start, end);
  };

  const shownComparisons = analysis?.comparisons.slice(0, visibleComparisons) ?? [];

  return (
    <Stack spacing={2} sx={{ p: { xs: 1, md: 2 }, minWidth: 0 }}>
      <Box>
        <Typography variant="h6">Analysis</Typography>
        <Typography variant="body2" color="text.secondary">
          Statistics for {session.recordingName} over one sample window.
          Choose a result to open that sample on the recording.
        </Typography>
      </Box>

      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
        <TextField
          size="small"
          label="Start sequence"
          value={startInput}
          onChange={(event) => setStartInput(event.target.value)}
          sx={{ width: 140 }}
        />
        <TextField
          size="small"
          label="End sequence"
          value={endInput}
          onChange={(event) => setEndInput(event.target.value)}
          sx={{ width: 140 }}
        />
        <Button size="small" variant="contained" onClick={updateFromInputs} disabled={loading}>
          Update
        </Button>
        <Button size="small" onClick={() => applyWindow(0, lastSequence)} disabled={loading}>
          Entire recording
        </Button>
        <Button size="small" onClick={() => applyWindow(cursor, endNumber(endInput, lastSequence))} disabled={loading}>
          Start at cursor
        </Button>
        <Button size="small" onClick={() => applyWindow(startNumber(startInput), cursor)} disabled={loading}>
          End at cursor
        </Button>
        <Button
          size="small"
          onClick={() => applyWindow(cursor - 60, cursor + 60)}
          disabled={loading}
        >
          ±60 samples
        </Button>
      </Stack>

      <FormControlLabel
        control={(
          <Checkbox
            size="small"
            checked={chartChannelsOnly}
            onChange={(event) => {
              const next = event.target.checked;
              setChartChannelsOnly(next);
              const start = startNumber(startInput);
              const end = endNumber(endInput, lastSequence);
              void load(start, end, next);
            }}
          />
        )}
        label={`Chart channels only (${selectedChannelIds.length} of ${channels.length})`}
      />

      {error && <Alert severity="error">{error}</Alert>}
      {loading && <CircularProgress size={24} />}

      {analysis && (
        <>
          <Typography variant="body2">
            Samples {analysis.startSequence.toLocaleString()}–{analysis.endSequence.toLocaleString()}
            {" · "}
            {analysis.sampleCount.toLocaleString()} samples
            {" · "}
            {formatTimestamp(analysis.startTimestampNanoseconds, session.timestampOrigin)}
            {" – "}
            {formatTimestamp(analysis.endTimestampNanoseconds, session.timestampOrigin)}
          </Typography>

          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Channel</TableCell>
                <TableCell>Type</TableCell>
                <TableCell align="right">Present</TableCell>
                <TableCell align="right">Missing</TableCell>
                <TableCell>Summary</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {analysis.channels.map((channel) => (
                <TableRow key={channel.channelId} hover>
                  <TableCell>{channelLabel(channel)}</TableCell>
                  <TableCell>{channel.valueType}</TableCell>
                  <TableCell align="right">{channel.presentCount.toLocaleString()}</TableCell>
                  <TableCell align="right">{channel.missingCount.toLocaleString()}</TableCell>
                  <TableCell>{renderSummary(channel, onInspectSample)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <Box>
            <Typography variant="subtitle2">Numeric comparison</Typography>
            <Typography variant="caption" color="text.secondary">
              Pearson correlation where both channels have a value. 1 means they move together.
              −1 means they move in opposite directions.
            </Typography>
            {shownComparisons.length === 0 ? (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                Two numeric channels in this window are needed for a comparison.
              </Typography>
            ) : (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Channels</TableCell>
                    <TableCell align="right">Overlap</TableCell>
                    <TableCell align="right">Correlation</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {shownComparisons.map((pair) => (
                    <TableRow key={`${pair.leftChannelId}:${pair.rightChannelId}`} hover>
                      <TableCell>{pair.leftChannelName} · {pair.rightChannelName}</TableCell>
                      <TableCell align="right">{pair.overlapCount.toLocaleString()}</TableCell>
                      <TableCell align="right">
                        {pair.correlation == null ? "—" : pair.correlation.toFixed(3)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
            {analysis.comparisons.length > shownComparisons.length && (
              <Typography variant="caption" color="text.secondary">
                Showing the {shownComparisons.length} strongest of {analysis.comparisons.length} pairs.
              </Typography>
            )}
          </Box>
        </>
      )}
    </Stack>
  );
}

function startNumber(value: string): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) ? parsed : 0;
}

function endNumber(value: string, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) ? parsed : fallback;
}

function channelLabel(channel: ChannelWindowSummary): string {
  return channel.unit ? `${channel.name} (${channel.unit})` : channel.name;
}

function formatMeasure(value: number): string {
  return value.toLocaleString(undefined, { maximumFractionDigits: 3 });
}

function formatTimestamp(nanoseconds: string, origin: SessionState["timestampOrigin"]): string {
  try {
    const value = BigInt(nanoseconds);
    if (origin === "Elapsed") {
      const negative = value < 0n;
      const magnitude = negative ? -value : value;
      const seconds = magnitude / 1_000_000_000n;
      const fraction = (magnitude % 1_000_000_000n)
        .toString()
        .padStart(9, "0")
        .replace(/0+$/, "");
      const body = fraction.length === 0 ? seconds.toString() : `${seconds}.${fraction}`;
      return `${negative ? "-" : ""}${body} s`;
    }

    const date = new Date(Number(value / 1_000_000n));
    return Number.isNaN(date.getTime()) ? nanoseconds : date.toISOString();
  } catch {
    return nanoseconds;
  }
}

function renderSummary(
  channel: ChannelWindowSummary,
  onInspectSample: (sequence: number) => void,
) {
  if (channel.valueType === "Numeric") {
    if (channel.minimum == null || channel.maximum == null || channel.mean == null) {
      return "No numeric values in this window.";
    }

    return (
      <span>
        min {formatMeasure(channel.minimum)} at{" "}
        <SequenceLink sequence={channel.minimumSequence} onInspectSample={onInspectSample} />
        {" · "}
        max {formatMeasure(channel.maximum)} at{" "}
        <SequenceLink sequence={channel.maximumSequence} onInspectSample={onInspectSample} />
        {" · "}
        mean {formatMeasure(channel.mean)}
      </span>
    );
  }

  if (channel.valueType === "Boolean") {
    return `${channel.trueCount ?? 0} true · ${channel.falseCount ?? 0} false · ${channel.changeCount ?? 0} changes`;
  }

  const mode = channel.mode == null
    ? "no mode"
    : `${channel.mode} (${channel.modeCount ?? 0})`;
  return `${channel.distinctCount ?? 0} distinct · mode ${mode} · ${channel.changeCount ?? 0} changes`;
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

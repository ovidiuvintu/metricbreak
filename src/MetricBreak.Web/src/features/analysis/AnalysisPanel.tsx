import { useCallback, useEffect, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  FormControlLabel,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";

import type { ChannelMetadata } from "../recordings/channelMetadata";
import type { SessionState } from "../recordings/sessionTypes";
import { analysisApi } from "./analysisApi";
import { EventAnalysisSection, RecordingComparisonSection } from "./HitAnalysis";
import type {
  ChannelCoverage,
  ChannelWindowSummary,
  SamplingGap,
  SamplingHealth,
  WindowAnalysis,
} from "./analysisTypes";

interface AnalysisPanelProps {
  session: SessionState | null;
  channels: ChannelMetadata[];
  selectedChannelIds: string[];
  breakpointKey?: string;
  breakpointHit?: { sequence: number; label: string | null } | null;
  onInspectSample: (sequence: number) => void;
  onBookmarkChange?: (request: {
    sequence: number;
    title: string;
    annotation: string;
  }) => void;
}

const visibleComparisons = 8;
type CadenceUnit = "s" | "ms" | "min";

export function AnalysisPanel({
  session,
  channels,
  selectedChannelIds,
  breakpointKey = "",
  breakpointHit = null,
  onInspectSample,
  onBookmarkChange,
}: AnalysisPanelProps) {
  const [startInput, setStartInput] = useState("0");
  const [endInput, setEndInput] = useState("0");
  const [chartChannelsOnly, setChartChannelsOnly] = useState(false);
  const [showPercentiles, setShowPercentiles] = useState(false);
  const [cadenceInput, setCadenceInput] = useState("");
  const [cadenceUnit, setCadenceUnit] = useState<CadenceUnit>("s");
  const [analysis, setAnalysis] = useState<WindowAnalysis | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const cadenceRef = useRef<string | null>(null);
  const loadedKeyRef = useRef<string | null>(null);
  const sessionKey = session && session.sampleCount > 0
    ? `${session.sessionId}:${session.sampleCount}`
    : null;
  const [resetKey, setResetKey] = useState<string | null>(null);
  if (sessionKey !== resetKey) {
    setResetKey(sessionKey);
    setCadenceInput("");
    setChartChannelsOnly(false);
    setStartInput("0");
    setEndInput(session && session.sampleCount > 0 ? String(session.sampleCount - 1) : "0");
    setAnalysis(null);
    setError(null);
  }

  const load = useCallback(async (
    start: number,
    end: number,
    chartOnly: boolean,
  ) => {
    if (!session) return;

    const limitToChart = chartOnly && selectedChannelIds.length > 0;
    const channelIds = limitToChart ? selectedChannelIds : undefined;

    setLoading(true);
    setError(null);
    try {
      const result = await analysisApi.getWindow(
        session.sessionId,
        start,
        end,
        channelIds,
        cadenceRef.current,
      );
      setAnalysis(result);
    } catch (err) {
      setAnalysis(null);
      setError(err instanceof Error ? err.message : "Unable to analyze this recording.");
    } finally {
      setLoading(false);
    }
  }, [selectedChannelIds, session]);

  useEffect(() => {
    if (!session || session.sampleCount < 1) {
      cadenceRef.current = null;
      loadedKeyRef.current = null;
      return;
    }

    const key = `${session.sessionId}:${session.sampleCount}`;
    if (loadedKeyRef.current === key) return;
    loadedKeyRef.current = key;
    cadenceRef.current = null;
    void load(0, session.sampleCount - 1, false);
  }, [session, load]);

  if (!session) {
    return (
      <Box sx={{ p: 2 }}>
        <Typography variant="h6">Analysis</Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          Import a recording to check its timeline and summarize channels.
        </Typography>
      </Box>
    );
  }

  const lastSequence = Math.max(0, session.sampleCount - 1);
  const cursor = session.currentSample?.sequence ?? 0;
  const statsHidden = chartChannelsOnly && selectedChannelIds.length === 0;
  const numericChannels = analysis?.channels.filter((channel) => channel.valueType === "Numeric") ?? [];
  const otherChannels = analysis?.channels.filter((channel) => channel.valueType !== "Numeric") ?? [];
  const shownComparisons = statsHidden
    ? []
    : analysis?.comparisons.slice(0, visibleComparisons) ?? [];

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

  const applyCadence = () => {
    const parsed = cadenceToNanoseconds(cadenceInput, cadenceUnit);
    if (parsed == null) {
      setError("Enter an expected interval greater than zero.");
      return;
    }
    cadenceRef.current = parsed;
    void load(startNumber(startInput), endNumber(endInput, lastSequence), chartChannelsOnly);
  };

  const clearCadence = () => {
    cadenceRef.current = null;
    setCadenceInput("");
    void load(startNumber(startInput), endNumber(endInput, lastSequence), chartChannelsOnly);
  };

  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        height: "100%",
        minHeight: 0,
        minWidth: 0,
      }}
    >
      <Box sx={{ flexShrink: 0, px: { xs: 1, md: 2 }, pt: { xs: 1, md: 2 }, pb: 1 }}>
        <Typography variant="h6">Analysis</Typography>
        <Typography variant="body2" color="text.secondary">
          Inspect the replay cursor, compare another recording, then summarize a sample window.
        </Typography>
        {error && <Alert severity="error" sx={{ mt: 1 }}>{error}</Alert>}
        {loading && <CircularProgress size={24} sx={{ mt: 1 }} />}
      </Box>

      <Stack
        spacing={2}
        sx={{
          flex: 1,
          minHeight: 0,
          overflow: "auto",
          px: { xs: 1, md: 2 },
          pb: { xs: 1, md: 2 },
        }}
      >
      {session.sampleCount > 0 && (
        <>
          {breakpointHit && breakpointHit.sequence !== (session.currentSample?.sequence ?? 0) && (
            <EventAnalysisSection
              session={session}
              breakpointKey={breakpointKey}
              sequence={breakpointHit.sequence}
              title={
                breakpointHit.label
                  ? `What changed before the breakpoint (${breakpointHit.label})`
                  : "What changed before the breakpoint"
              }
              onInspectSample={onInspectSample}
              onBookmarkChange={onBookmarkChange}
            />
          )}
          <EventAnalysisSection
            session={session}
            breakpointKey={breakpointKey}
            title={
              breakpointHit && breakpointHit.sequence === (session.currentSample?.sequence ?? 0)
                ? "What changed before this breakpoint"
                : undefined
            }
            onInspectSample={onInspectSample}
            onBookmarkChange={onBookmarkChange}
          />
          <RecordingComparisonSection
            session={session}
            onInspectSample={onInspectSample}
          />
        </>
      )}

      {analysis && (
        <SamplingSection
          sampling={analysis.sampling}
          origin={session.timestampOrigin}
          cadenceInput={cadenceInput}
          cadenceUnit={cadenceUnit}
          loading={loading}
          onCadenceInput={setCadenceInput}
          onCadenceUnit={setCadenceUnit}
          onApplyCadence={applyCadence}
          onClearCadence={clearCadence}
          onInspectSample={onInspectSample}
        />
      )}

      <Box>
        <Typography variant="h6">Descriptive statistics</Typography>
        <Typography variant="body2" color="text.secondary">
          Numeric channels in the selected window. Standard deviation uses the sample formula.
          Min and max open that sample on the recording.
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

      <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: "wrap" }}>
        <FormControlLabel
          control={(
            <Checkbox
              size="small"
              checked={chartChannelsOnly}
              onChange={(event) => {
                const next = event.target.checked;
                setChartChannelsOnly(next);
                void load(startNumber(startInput), endNumber(endInput, lastSequence), next);
              }}
            />
          )}
          label={`Chart channels only (${selectedChannelIds.length} of ${channels.length})`}
        />
        <FormControlLabel
          control={(
            <Checkbox
              size="small"
              checked={showPercentiles}
              onChange={(event) => setShowPercentiles(event.target.checked)}
            />
          )}
          label="Show percentiles"
        />
      </Stack>

      {analysis && statsHidden && (
        <Typography variant="body2" color="text.secondary">
          Select one or more channels on the Recordings chart.
        </Typography>
      )}

      {analysis && !statsHidden && (
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

          <TableContainer sx={{ overflow: "visible" }}>
            <Table
              size="small"
              stickyHeader
              sx={{ "& .MuiTableCell-stickyHeader": { bgcolor: "background.paper" } }}
            >
              <TableHead>
                <TableRow>
                  <TableCell>Channel</TableCell>
                  <TableCell align="right">Valid</TableCell>
                  <TableCell align="right">Missing</TableCell>
                  <TableCell>Min</TableCell>
                  <TableCell>Max</TableCell>
                  <TableCell align="right">Mean</TableCell>
                  <TableCell align="right">Median</TableCell>
                  <TableCell align="right">Std dev</TableCell>
                  {showPercentiles && (
                    <>
                      <TableCell align="right">P5</TableCell>
                      <TableCell align="right">P25</TableCell>
                      <TableCell align="right">P75</TableCell>
                      <TableCell align="right">P95</TableCell>
                    </>
                  )}
                </TableRow>
              </TableHead>
              <TableBody>
                {numericChannels.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={showPercentiles ? 12 : 8}>
                      No numeric channels in this window.
                    </TableCell>
                  </TableRow>
                ) : numericChannels.map((channel) => (
                  <TableRow key={channel.channelId} hover>
                    <TableCell>{channelLabel(channel)}</TableCell>
                    <TableCell align="right">{channel.presentCount.toLocaleString()}</TableCell>
                    <TableCell align="right">{channel.missingCount.toLocaleString()}</TableCell>
                    <TableCell>
                      <Extreme
                        value={channel.minimum}
                        sequence={channel.minimumSequence}
                        onInspectSample={onInspectSample}
                      />
                    </TableCell>
                    <TableCell>
                      <Extreme
                        value={channel.maximum}
                        sequence={channel.maximumSequence}
                        onInspectSample={onInspectSample}
                      />
                    </TableCell>
                    <TableCell align="right">{formatOptional(channel.mean)}</TableCell>
                    <TableCell align="right">{formatOptional(channel.median)}</TableCell>
                    <TableCell align="right">{formatOptional(channel.standardDeviation)}</TableCell>
                    {showPercentiles && (
                      <>
                        <TableCell align="right">{formatOptional(channel.percentile05)}</TableCell>
                        <TableCell align="right">{formatOptional(channel.percentile25)}</TableCell>
                        <TableCell align="right">{formatOptional(channel.percentile75)}</TableCell>
                        <TableCell align="right">{formatOptional(channel.percentile95)}</TableCell>
                      </>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          {otherChannels.length > 0 && (
            <Box>
              <Typography variant="subtitle2">Other channels</Typography>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Channel</TableCell>
                    <TableCell>Type</TableCell>
                    <TableCell align="right">Valid</TableCell>
                    <TableCell align="right">Missing</TableCell>
                    <TableCell>Summary</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {otherChannels.map((channel) => (
                    <TableRow key={channel.channelId} hover>
                      <TableCell>{channelLabel(channel)}</TableCell>
                      <TableCell>{channel.valueType}</TableCell>
                      <TableCell align="right">{channel.presentCount.toLocaleString()}</TableCell>
                      <TableCell align="right">{channel.missingCount.toLocaleString()}</TableCell>
                      <TableCell>{renderOtherSummary(channel)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          )}

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
    </Box>
  );
}

function SamplingSection({
  sampling,
  origin,
  cadenceInput,
  cadenceUnit,
  loading,
  onCadenceInput,
  onCadenceUnit,
  onApplyCadence,
  onClearCadence,
  onInspectSample,
}: {
  sampling: SamplingHealth;
  origin: SessionState["timestampOrigin"];
  cadenceInput: string;
  cadenceUnit: CadenceUnit;
  loading: boolean;
  onCadenceInput: (value: string) => void;
  onCadenceUnit: (unit: CadenceUnit) => void;
  onApplyCadence: () => void;
  onClearCadence: () => void;
  onInspectSample: (sequence: number) => void;
}) {
  const completeChannels = sampling.channelCount - sampling.missingChannels.length;
  return (
    <Stack spacing={1.5}>
      <Box>
        <Typography variant="h6">Data health</Typography>
        <Typography variant="body2" color="text.secondary">
          Recording span, observed spacing, duplicate timestamps, missing values, and gaps.
          Duplicate samples stay in the recording.
        </Typography>
      </Box>

      <Typography variant="body2">
        {formatTimestamp(sampling.startTimestampNanoseconds, origin)}
        {" – "}
        {formatTimestamp(sampling.endTimestampNanoseconds, origin)}
        {" · "}
        {formatDuration(durationBetween(
          sampling.startTimestampNanoseconds,
          sampling.endTimestampNanoseconds,
        ))}
        {" · "}
        {sampling.sampleCount.toLocaleString()} samples
      </Typography>

      <Typography variant="body2">
        {sampling.positiveIntervalCount === 0
          ? "No positive interval between samples."
          : (
            <>
              Observed intervals: min {formatDuration(sampling.intervalMinimumNanoseconds)}
              {" · "}
              median {formatDuration(sampling.intervalMedianNanoseconds)}
              {" · "}
              most common {formatDuration(sampling.intervalModeNanoseconds)}
              {" ("}
              {sampling.intervalModeCount.toLocaleString()}
              {" of "}
              {sampling.positiveIntervalCount.toLocaleString()}
              {") · "}
              max {formatDuration(sampling.intervalMaximumNanoseconds)}
            </>
          )}
      </Typography>

      <Typography variant="body2">
        {cadenceSentence(sampling)}
      </Typography>

      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
        <TextField
          size="small"
          label="Expected interval"
          value={cadenceInput}
          onChange={(event) => onCadenceInput(event.target.value)}
          sx={{ width: 160 }}
        />
        <TextField
          select
          size="small"
          label="Unit"
          value={cadenceUnit}
          onChange={(event) => onCadenceUnit(event.target.value as CadenceUnit)}
          sx={{ width: 130 }}
        >
          <MenuItem value="ms">milliseconds</MenuItem>
          <MenuItem value="s">seconds</MenuItem>
          <MenuItem value="min">minutes</MenuItem>
        </TextField>
        <Button size="small" variant="outlined" onClick={onApplyCadence} disabled={loading}>
          Declare
        </Button>
        {sampling.cadenceSource === "declared" && (
          <Button size="small" onClick={onClearCadence} disabled={loading}>
            Clear declared interval
          </Button>
        )}
      </Stack>

      <Box>
        <Typography variant="subtitle2">Duplicate timestamps</Typography>
        {sampling.duplicateGroupCount === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No duplicate timestamps.
          </Typography>
        ) : (
          <>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>
              {sampling.duplicateGroupCount.toLocaleString()} timestamps are shared by{" "}
              {sampling.duplicateSampleCount.toLocaleString()} samples. Each sample is kept.
            </Typography>
            <TableContainer sx={{ maxHeight: 240 }}>
              <Table size="small" stickyHeader>
                <TableHead>
                  <TableRow>
                    <TableCell>Timestamp</TableCell>
                    <TableCell>Samples</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {sampling.duplicates.map((group) => (
                    <TableRow key={group.timestampNanoseconds} hover>
                      <TableCell>{formatTimestamp(group.timestampNanoseconds, origin)}</TableCell>
                      <TableCell>
                        <SequenceList sequences={group.sequences} onInspectSample={onInspectSample} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </>
        )}
      </Box>

      <Box>
        <Typography variant="subtitle2">Missing channel values</Typography>
        {sampling.missingChannels.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            Every channel has a value on every sample.
          </Typography>
        ) : (
          <>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>
              {completeChannels.toLocaleString()} of {sampling.channelCount.toLocaleString()} channels
              are complete. The first missing sample opens on the recording.
            </Typography>
            <TableContainer sx={{ maxHeight: 240 }}>
              <Table size="small" stickyHeader>
                <TableHead>
                  <TableRow>
                    <TableCell>Channel</TableCell>
                    <TableCell align="right">Missing</TableCell>
                    <TableCell>First missing</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {sampling.missingChannels.map((channel) => (
                    <TableRow key={channel.channelId} hover>
                      <TableCell>{coverageLabel(channel)}</TableCell>
                      <TableCell align="right">{channel.missingCount.toLocaleString()}</TableCell>
                      <TableCell>
                        <SequenceLink sequence={channel.firstSequence} onInspectSample={onInspectSample} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </>
        )}
      </Box>

      <Box>
        <Typography variant="subtitle2">Gaps</Typography>
        <Typography variant="caption" color="text.secondary">
          Each gap opens the sample before it and the sample after it.
          {sampling.missingTimestampCount == null
            ? " Missing timestamps are not counted until a cadence is confirmed or declared."
            : ` ${sampling.missingTimestampCount.toLocaleString()} timestamps are missing at the current cadence.`}
        </Typography>
        {sampling.gapCount === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            No gaps in the sample spacing.
          </Typography>
        ) : (
          <TableContainer sx={{ maxHeight: 280, mt: 0.5 }}>
            <Table size="small" stickyHeader>
              <TableHead>
                <TableRow>
                  <TableCell>Before</TableCell>
                  <TableCell>After</TableCell>
                  <TableCell>Interval</TableCell>
                  <TableCell align="right">Missing timestamps</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {sampling.gaps.map((gap) => (
                  <TableRow key={`${gap.beforeSequence}:${gap.afterSequence}`} hover>
                    <TableCell>
                      <SequenceLink sequence={gap.beforeSequence} onInspectSample={onInspectSample} />
                    </TableCell>
                    <TableCell>
                      <SequenceLink sequence={gap.afterSequence} onInspectSample={onInspectSample} />
                    </TableCell>
                    <TableCell>{gapIntervalLabel(gap)}</TableCell>
                    <TableCell align="right">
                      {gap.missingTimestamps == null ? "—" : gap.missingTimestamps.toLocaleString()}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Box>
    </Stack>
  );
}

function cadenceSentence(sampling: SamplingHealth): string {
  if (sampling.cadenceSource === "declared" && sampling.cadenceNanoseconds) {
    return `Declared cadence: ${formatDuration(sampling.cadenceNanoseconds)}. Missing timestamps use this interval.`;
  }
  if (sampling.cadenceSource === "confirmed" && sampling.cadenceNanoseconds) {
    return `Confirmed cadence: ${formatDuration(sampling.cadenceNanoseconds)}, from the most common interval. Missing timestamps use this interval.`;
  }
  return "Intervals do not share one cadence, so missing timestamps are not counted. Declare an expected interval to count them.";
}

function gapIntervalLabel(gap: SamplingGap): string {
  if (gap.intervalNanoseconds.startsWith("-")) {
    return `Clock moved backward ${formatDuration(gap.intervalNanoseconds.slice(1))}`;
  }
  return formatDuration(gap.intervalNanoseconds);
}

function SequenceList({
  sequences,
  onInspectSample,
}: {
  sequences: number[];
  onInspectSample: (sequence: number) => void;
}) {
  return (
    <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
      {sequences.map((sequence) => (
        <SequenceLink key={sequence} sequence={sequence} onInspectSample={onInspectSample} />
      ))}
    </Box>
  );
}

function Extreme({
  value,
  sequence,
  onInspectSample,
}: {
  value: number | null;
  sequence: number | null;
  onInspectSample: (sequence: number) => void;
}) {
  if (value == null) return <span>—</span>;
  return (
    <span>
      {formatMeasure(value)}
      {" at "}
      <SequenceLink sequence={sequence} onInspectSample={onInspectSample} />
    </span>
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

function coverageLabel(channel: ChannelCoverage): string {
  return channel.unit ? `${channel.name} (${channel.unit})` : channel.name;
}

function formatMeasure(value: number): string {
  return value.toLocaleString(undefined, { maximumFractionDigits: 3 });
}

function formatOptional(value: number | null): string {
  return value == null ? "—" : formatMeasure(value);
}

function cadenceToNanoseconds(input: string, unit: CadenceUnit): string | null {
  const value = Number(input);
  if (!Number.isFinite(value) || value <= 0) return null;
  const scale = unit === "ms" ? 1_000_000 : unit === "min" ? 60_000_000_000 : 1_000_000_000;
  const nanoseconds = Math.round(value * scale);
  if (!Number.isSafeInteger(nanoseconds) || nanoseconds <= 0) return null;
  return String(nanoseconds);
}

function durationBetween(start: string, end: string): string {
  try {
    return (BigInt(end) - BigInt(start)).toString();
  } catch {
    return "0";
  }
}

function formatDuration(nanoseconds: string | null): string {
  if (nanoseconds == null) return "—";
  let value: bigint;
  try {
    value = BigInt(nanoseconds);
  } catch {
    return nanoseconds;
  }

  const sign = value < 0n ? "-" : "";
  const magnitude = value < 0n ? -value : value;
  const seconds = magnitude / 1_000_000_000n;
  const remainder = magnitude % 1_000_000_000n;

  if (seconds === 0n) {
    const milliseconds = remainder / 1_000_000n;
    if (milliseconds === 0n) return `${sign}${remainder.toString()} ns`;
    return `${sign}${milliseconds.toString()} ms`;
  }

  if (seconds < 60n) {
    if (remainder === 0n) return `${sign}${seconds.toString()} s`;
    const milliseconds = (remainder / 1_000_000n).toString().padStart(3, "0").replace(/0+$/, "");
    return `${sign}${seconds.toString()}.${milliseconds} s`;
  }

  const minutes = seconds / 60n;
  const leftoverSeconds = seconds % 60n;
  if (minutes < 60n) {
    return leftoverSeconds === 0n
      ? `${sign}${minutes.toString()} min`
      : `${sign}${minutes.toString()} min ${leftoverSeconds.toString()} s`;
  }

  const hours = minutes / 60n;
  const leftoverMinutes = minutes % 60n;
  return leftoverMinutes === 0n
    ? `${sign}${hours.toString()} h`
    : `${sign}${hours.toString()} h ${leftoverMinutes.toString()} min`;
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

function renderOtherSummary(channel: ChannelWindowSummary): string {
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

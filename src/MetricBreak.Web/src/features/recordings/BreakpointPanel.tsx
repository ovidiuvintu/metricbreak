import {
  Alert, Box, Button, CircularProgress, Divider, IconButton,
  Stack, Switch, Tooltip, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import NavigateBeforeIcon from "@mui/icons-material/NavigateBefore";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";
import {
  isRecordingGap,
  type Breakpoint,
  type BreakpointCoverage,
  type BreakpointCoverageRelation,
  type BreakpointOperator,
  type CompositeBreakpoint,
} from "./breakpointApi";
import type { ChannelMetadata } from "./channelMetadata";

interface BreakpointPanelProps {
  breakpoints: Breakpoint[];
  compositeBreakpoints: CompositeBreakpoint[];
  channels: ChannelMetadata[];
  loading: boolean;
  busy: boolean;
  error: string | null;
  onAdd: () => void;
  onAddComposite: () => void;
  onSetEnabled: (breakpointId: string, isEnabled: boolean) => void;
  onDelete: (breakpointId: string) => void;
  onRunTo?: (breakpointId: string, direction: "forward" | "backward") => void;
  focusedBreakpointId?: string | null;
  coverage?: BreakpointCoverage[];
}

const relationLabel: Record<BreakpointCoverageRelation, string> = {
  above: "above",
  atOrAbove: "at or above",
  below: "below",
  atOrBelow: "at or below",
  matching: "matching",
};

function formatDuration(nanoseconds: number): string {
  const seconds = Math.max(0, Math.round(nanoseconds / 1_000_000_000));
  if (seconds < 60) return `${seconds.toLocaleString()}s`;
  const minutes = Math.floor(seconds / 60);
  const remainder = seconds % 60;
  if (minutes < 60) return remainder === 0 ? `${minutes}m` : `${minutes}m ${remainder}s`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins === 0 ? `${hours}h` : `${hours}h ${mins}m`;
}

function formatSpan(samples: number, nanoseconds: number): string {
  if (samples <= 0) return "0s";
  if (nanoseconds <= 0) {
    return samples === 1 ? "1 sample" : `${samples.toLocaleString()} samples`;
  }
  const seconds = Math.round(nanoseconds / 1_000_000_000);
  if (Math.abs(seconds - samples) > 1) {
    return `${samples.toLocaleString()} samples (${formatDuration(nanoseconds)})`;
  }
  return formatDuration(nanoseconds);
}

function formatCoverage(coverage: BreakpointCoverage): string {
  const side = relationLabel[coverage.relation] ?? "matching";
  const parts: string[] = [];
  if (coverage.crossings != null) {
    parts.push(
      `${coverage.crossings.toLocaleString()} ${coverage.crossings === 1 ? "crossing" : "crossings"}`,
    );
  }
  parts.push(`${formatSpan(coverage.matchingSamples, coverage.matchingNanoseconds)} ${side}`);
  if (
    coverage.longestRunSamples > 0
    && coverage.longestRunSamples !== coverage.matchingSamples
  ) {
    parts.push(
      `longest ${formatSpan(coverage.longestRunSamples, coverage.longestRunNanoseconds)}`,
    );
  }
  return parts.join(" · ");
}

function describeCoverage(coverage: BreakpointCoverage): string {
  const samples = coverage.matchingSamples.toLocaleString();
  const longest = coverage.longestRunSamples.toLocaleString();
  const run = coverage.longestRunSamples === coverage.matchingSamples
    ? "one run"
    : `longest run ${longest} samples`;
  if (coverage.relation === "matching") {
    return `${samples} samples match this rule, ${run}.`;
  }
  const side = relationLabel[coverage.relation] ?? "matching";
  return `${samples} samples ${side} the threshold, ${run}.`;
}

const symbols: Record<BreakpointOperator, string> = {
  GreaterThan: ">", GreaterThanOrEqual: ">=", LessThan: "<",
  LessThanOrEqual: "<=", Equal: "==", NotEqual: "!=", Changed: "changed", RisingEdge: "rises above", FallingEdge: "falls below",
  MissingLongerThan: "missing >",
};

function CoverageCaption({ coverage }: { coverage: BreakpointCoverage | undefined }) {
  if (!coverage) return null;
  return (
    <Tooltip title={describeCoverage(coverage)}>
      <Typography
        variant="caption"
        color="text.secondary"
        sx={{ display: "block", overflowWrap: "anywhere" }}
      >
        {formatCoverage(coverage)}
      </Typography>
    </Tooltip>
  );
}

export function BreakpointPanel({
  breakpoints, compositeBreakpoints, channels, loading, busy, error,
  onAdd, onAddComposite, onSetEnabled, onDelete, onRunTo, focusedBreakpointId,
  coverage = [],
}: BreakpointPanelProps) {
  const names = new Map(channels.map(c => [c.channelId, c.name]));
  const scopeName = (channelId: string) =>
    isRecordingGap(channelId) ? "Recording" : (names.get(channelId) ?? channelId);
  const format = (c: { channelId: string; operator: BreakpointOperator; threshold: number; value?: string }) =>
    c.operator === "MissingLongerThan"
      ? `${scopeName(c.channelId)} missing > ${c.threshold}s`
      : `${names.get(c.channelId) ?? c.channelId} ${symbols[c.operator]} ${c.operator === "Changed" ? "" : c.value ?? c.threshold.toLocaleString()}`;
  const total = breakpoints.length + compositeBreakpoints.length;
  const coverageById = new Map(coverage.map(item => [item.breakpointId, item]));

  const actions = (id: string, enabled: boolean, label: string) => (
  <>
      {onRunTo && (
        <>
          <Tooltip title="Previous occurrence">
            <span>
              <IconButton
                size="small"
                disabled={busy}
                aria-label={`Previous hit for ${label}`}
                onClick={() => onRunTo(id, "backward")}
              >
                <NavigateBeforeIcon fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title="Next occurrence">
            <span>
              <IconButton
                size="small"
                disabled={busy}
                aria-label={`Next hit for ${label}`}
                onClick={() => onRunTo(id, "forward")}
              >
                <NavigateNextIcon fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
        </>
      )}
      <Tooltip title={enabled ? "Disable breakpoint" : "Enable breakpoint"}>
        <span>
          <Switch size="small" checked={enabled} disabled={busy}
            onChange={(_, checked) => onSetEnabled(id, checked)}
            slotProps={{
              input: { "aria-label": `Toggle ${label}` },
            }} />
        </span>
      </Tooltip>
      <Tooltip title="Delete breakpoint">
        <span>
          <IconButton size="small" color="error" disabled={busy}
            onClick={() => onDelete(id)} aria-label={`Delete ${label}`}>
            <DeleteOutlineIcon fontSize="small" />
          </IconButton>
        </span>
      </Tooltip>
    </>
  );

  return (
    <Stack spacing={1.5} sx={{ width: "100%", minWidth: 0 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1, flexWrap: "wrap" }}>
        <Typography variant="subtitle2">Breakpoints ({total})</Typography>
        <Stack direction="row" spacing={1}>
          <Button size="small" startIcon={<AddIcon />} onClick={onAdd}
            disabled={loading || busy || channels.length === 0}>Add</Button>
          <Button size="small" variant="outlined" startIcon={<AddIcon />}
            onClick={onAddComposite} disabled={loading || busy || channels.length === 0}>
            Add Composite
          </Button>
        </Stack>
      </Box>
      {onRunTo && total > 0 && (
        <Typography variant="caption" color="text.secondary">
          Use ◀ ▶ on each breakpoint to jump to previous or next occurrence (like Find Next).
        </Typography>
      )}
      {error && <Alert severity="error">{error}</Alert>}
      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}><CircularProgress size={24} /></Box>
      ) : total === 0 ? (
        <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
          No breakpoints configured. Use Add breakpoint in the toolbar, or Add here.
        </Typography>
      ) : (
        <Stack divider={<Divider flexItem />} spacing={0}>
          {breakpoints.map(b => (
            <Box
              key={b.breakpointId}
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 0.5,
                py: 1,
                opacity: b.isEnabled ? 1 : 0.6,
                borderRadius: 1,
                px: focusedBreakpointId === b.breakpointId ? 0.5 : 0,
                bgcolor: focusedBreakpointId === b.breakpointId ? "action.selected" : "transparent",
              }}
            >
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Tooltip title={format(b)}>
                  <Typography variant="body2" sx={{ fontWeight: 500 }} noWrap>
                    {b.operator === "MissingLongerThan" ? scopeName(b.channelId) : (names.get(b.channelId) ?? b.channelId)}
                  </Typography>
                </Tooltip>
                <Typography variant="caption" color="text.secondary">
                  {b.operator === "MissingLongerThan"
                    ? `missing > ${b.threshold}s`
                    : `${symbols[b.operator]} ${b.operator === "Changed" ? "" : b.value ?? b.threshold.toLocaleString()}`}
                </Typography>
                <CoverageCaption coverage={coverageById.get(b.breakpointId)} />
              </Box>
              {actions(b.breakpointId, b.isEnabled, format(b))}
            </Box>
          ))}
          {compositeBreakpoints.map(b => {
            const expression = b.conditions.map(format).join(b.logicalOperator === "And" ? " AND " : " OR ");
            return (
              <Box
                key={b.breakpointId}
                sx={{
                  display: "flex",
                  alignItems: "center",
                  gap: 0.5,
                  py: 1,
                  opacity: b.isEnabled ? 1 : 0.6,
                  borderRadius: 1,
                  px: focusedBreakpointId === b.breakpointId ? 0.5 : 0,
                  bgcolor: focusedBreakpointId === b.breakpointId ? "action.selected" : "transparent",
                }}
              >
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Typography
                    variant="caption"
                    sx={{ fontWeight: 700 }}
                    color="primary"
                  >
                    COMPOSITE · {b.logicalOperator.toUpperCase()}
                  </Typography>
                  <Tooltip title={expression}>
                    <Typography variant="body2" sx={{ overflowWrap: "anywhere" }}>{expression}</Typography>
                  </Tooltip>
                  <CoverageCaption coverage={coverageById.get(b.breakpointId)} />
                </Box>
                {actions(b.breakpointId, b.isEnabled, expression)}
              </Box>
            );
          })}
        </Stack>
      )}
    </Stack>
  );
}

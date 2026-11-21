import {
  Alert, Box, Button, CircularProgress, Divider, IconButton,
  Stack, Switch, Tooltip, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import NavigateBeforeIcon from "@mui/icons-material/NavigateBefore";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";
import type { Breakpoint, CompositeBreakpoint, ComparisonOperator } from "./breakpointApi";
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
}

const symbols: Record<ComparisonOperator, string> = {
  GreaterThan: ">", GreaterThanOrEqual: ">=", LessThan: "<",
  LessThanOrEqual: "<=", Equal: "==", NotEqual: "!=", Changed: "changed", RisingEdge: "rises above", FallingEdge: "falls below",
};

export function BreakpointPanel({
  breakpoints, compositeBreakpoints, channels, loading, busy, error,
  onAdd, onAddComposite, onSetEnabled, onDelete, onRunTo, focusedBreakpointId,
}: BreakpointPanelProps) {
  const names = new Map(channels.map(c => [c.channelId, c.name]));
  const format = (c: { channelId: string; operator: ComparisonOperator; threshold: number; value?: string }) =>
    `${names.get(c.channelId) ?? c.channelId} ${symbols[c.operator]} ${c.operator === "Changed" ? "" : c.value ?? c.threshold.toLocaleString()}`;
  const total = breakpoints.length + compositeBreakpoints.length;

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
          No breakpoints configured. Right-click the chart on an interesting sample to add one, or use Add here.
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
                  <Typography variant="body2" sx={{ fontWeight: 500 }} noWrap>{names.get(b.channelId) ?? b.channelId}</Typography>
                </Tooltip>
                <Typography variant="caption" color="text.secondary">{symbols[b.operator]} {b.operator === "Changed" ? "" : b.value ?? b.threshold.toLocaleString()}</Typography>
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

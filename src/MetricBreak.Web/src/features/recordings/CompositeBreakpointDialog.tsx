import { useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent,
  DialogTitle, FormControl, IconButton, InputLabel, MenuItem,
  Select, Stack, TextField, ToggleButton, ToggleButtonGroup, Typography,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { getSeriesColor } from "../../theme/chartTheme";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import type {
  BreakpointCondition, ComparisonOperator, CreateCompositeBreakpointRequest,
  LogicalOperator,
} from "./breakpointApi";
import {
  formatChannelExtent,
  type ChannelExtent,
  type ChannelMetadata,
} from "./channelMetadata";

type DraftCondition = { key: number; channelId: string; operator: ComparisonOperator; threshold: string; value: string };

interface CompositeBreakpointDialogProps {
  open: boolean;
  channels: ChannelMetadata[];
  channelExtents?: Record<string, ChannelExtent>;
  seriesColors?: Record<string, string>;
  busy: boolean;
  error: string | null;
  onClose: () => void;
  onCreate: (request: CreateCompositeBreakpointRequest) => Promise<void> | void;
}

function ChannelOptionLabel({
  name,
  color,
  range,
}: {
  name: string;
  color: string;
  range?: string | null;
}) {
  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 1, minWidth: 0, width: "100%" }}>
      <Box
        aria-hidden
        sx={{
          width: 8,
          height: 8,
          borderRadius: "50%",
          flexShrink: 0,
          bgcolor: color,
        }}
      />
      <Box component="span" sx={{ minWidth: 0, flex: 1, overflow: "hidden", textOverflow: "ellipsis" }}>
        {name}
      </Box>
      {range && (
        <Box
          component="span"
          sx={{
            flexShrink: 0,
            color: "text.secondary",
            fontSize: "0.75rem",
            fontVariantNumeric: "tabular-nums",
          }}
        >
          {range}
        </Box>
      )}
    </Box>
  );
}

const operators: { value: ComparisonOperator; label: string }[] = [
  { value: "GreaterThan", label: ">" },
  { value: "GreaterThanOrEqual", label: ">=" },
  { value: "LessThan", label: "<" },
  { value: "LessThanOrEqual", label: "<=" },
  { value: "Equal", label: "==" },
  { value: "NotEqual", label: "!=" },
  { value: "Changed", label: "Changed" },
  { value: "RisingEdge", label: "Rising Edge" },
  { value: "FallingEdge", label: "Falling Edge" },
];

export function CompositeBreakpointDialog({
  open, channels, channelExtents = {}, seriesColors = {}, busy, error, onClose, onCreate,
}: CompositeBreakpointDialogProps) {
  const theme = useTheme();
  const seriesColorById = useMemo(() => {
    const colors: Record<string, string> = {};
    channels.forEach((channel, index) => {
      colors[channel.channelId] =
        seriesColors[channel.channelId] ?? getSeriesColor(theme.palette.mode, index);
    });
    return colors;
  }, [channels, seriesColors, theme.palette.mode]);
  const [logicalOperator, setLogicalOperator] = useState<LogicalOperator>("And");
  const [conditions, setConditions] = useState<DraftCondition[]>([]);
  const [nextKey, setNextKey] = useState(2);

  useEffect(() => {
    if (open) {
      setLogicalOperator("And");
      setConditions([
        { key: 0, channelId: channels[0]?.channelId ?? "", operator: (channels[0]?.valueType ?? "Numeric") === "Numeric" ? "GreaterThan" : "Changed", threshold: "", value: "" },
        { key: 1, channelId: channels[0]?.channelId ?? "", operator: (channels[0]?.valueType ?? "Numeric") === "Numeric" ? "LessThan" : "Changed", threshold: "", value: "" },
      ]);
      setNextKey(2);
    }
  }, [open]);

  const update = (key: number, change: Partial<DraftCondition>) =>
    setConditions(current => current.map(c => c.key === key ? { ...c, ...change } : c));

  const valid = conditions.length >= 2 && conditions.every(c =>
    channels.some(channel => channel.channelId === c.channelId) &&
    ((channels.find(ch => ch.channelId === c.channelId)?.valueType ?? "Numeric") !== "Numeric" || c.operator === "Changed" || c.threshold.trim() !== "") && Number.isFinite(Number(c.threshold || "0")) &&
    (!["Equal", "NotEqual"].includes(c.operator) || (channels.find(ch => ch.channelId === c.channelId)?.valueType ?? "Numeric") === "Numeric" ||
      ((channels.find(ch => ch.channelId === c.channelId)?.valueType === "Boolean") ? ["true", "false"].includes(c.value) : c.value.length > 0)));

  const submit = async () => {
    if (!valid || busy) return;
    const parsed: BreakpointCondition[] = conditions.map(c => ({
      channelId: c.channelId,
      operator: c.operator,
      threshold: Number(c.threshold || "0"),
      ...(["Equal", "NotEqual"].includes(c.operator) && (channels.find(ch => ch.channelId === c.channelId)?.valueType ?? "Numeric") !== "Numeric" ? { value: c.value } : {}),
    }));
    await onCreate({ logicalOperator, conditions: parsed });
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Create composite breakpoint</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Pause replay when all conditions match (AND), or when any condition matches (OR).
          </Typography>
          <ToggleButtonGroup exclusive size="small" fullWidth value={logicalOperator}
            onChange={(_, value: LogicalOperator | null) => { if (value) setLogicalOperator(value); }}>
            <ToggleButton value="And">All conditions (AND)</ToggleButton>
            <ToggleButton value="Or">Any condition (OR)</ToggleButton>
          </ToggleButtonGroup>
          {conditions.map((c, index) => (
            <Box key={c.key} sx={{ p: 1.5, border: 1, borderColor: "divider", borderRadius: 1 }}>
              <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", mb: 1 }}>
                <Typography variant="subtitle2">Condition {index + 1}</Typography>
                <IconButton size="small" aria-label={`Remove condition ${index + 1}`}
                  disabled={busy || conditions.length <= 2}
                  onClick={() => setConditions(current => current.filter(item => item.key !== c.key))}>
                  <DeleteOutlineIcon fontSize="small" />
                </IconButton>
              </Stack>
              <Stack spacing={1.5}>
                <FormControl fullWidth size="small">
                  <InputLabel id={`channel-${c.key}`}>Channel</InputLabel>
                  <Select labelId={`channel-${c.key}`} label="Channel" value={c.channelId}
                    onChange={event => update(c.key, { channelId: event.target.value, operator: (channels.find(ch => ch.channelId === event.target.value)?.valueType ?? "Numeric") === "Numeric" ? c.operator : "Changed" })} disabled={busy}
                    renderValue={(selected) => {
                      const channel = channels.find((item) => item.channelId === selected);
                      if (!channel) return "";
                      return (
                        <ChannelOptionLabel
                          name={channel.name}
                          color={seriesColorById[channel.channelId]}
                          range={formatChannelExtent(channelExtents[channel.channelId])}
                        />
                      );
                    }}>
                    {channels.map(channel => (
                      <MenuItem key={channel.channelId} value={channel.channelId} sx={{ width: "100%" }}>
                        <ChannelOptionLabel
                          name={channel.name}
                          color={seriesColorById[channel.channelId]}
                          range={formatChannelExtent(channelExtents[channel.channelId])}
                        />
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <Stack direction="row" spacing={1}>
                  <FormControl size="small" sx={{ minWidth: 110 }}>
                    <InputLabel id={`operator-${c.key}`}>Operator</InputLabel>
                    <Select labelId={`operator-${c.key}`} label="Operator" value={c.operator}
                      onChange={event => update(c.key, { operator: event.target.value as ComparisonOperator })} disabled={busy}>
                      {operators.filter(op => {
                        const type = channels.find(ch => ch.channelId === c.channelId)?.valueType ?? "Numeric";
                        return type === "Numeric" || (type === "Boolean" && ["Equal", "NotEqual", "Changed", "RisingEdge", "FallingEdge"].includes(op.value)) || (type === "Categorical" && ["Equal", "NotEqual", "Changed"].includes(op.value));
                      }).map(op => <MenuItem key={op.value} value={op.value}>{op.label}</MenuItem>)}
                    </Select>
                  </FormControl>
                  {["Equal", "NotEqual"].includes(c.operator) && (channels.find(ch => ch.channelId === c.channelId)?.valueType ?? "Numeric") !== "Numeric" &&
                    ((channels.find(ch => ch.channelId === c.channelId)?.valueType === "Boolean") ?
                      <FormControl size="small" fullWidth><InputLabel>Value</InputLabel>
                        <Select label="Value" value={c.value} onChange={event => update(c.key, { value: event.target.value })}>
                          <MenuItem value="true">true</MenuItem><MenuItem value="false">false</MenuItem>
                        </Select></FormControl> :
                      <TextField size="small" fullWidth label="Value" value={c.value} onChange={event => update(c.key, { value: event.target.value })} />)}
                  {(channels.find(ch => ch.channelId === c.channelId)?.valueType ?? "Numeric") === "Numeric" && c.operator !== "Changed" && <TextField fullWidth size="small" label="Threshold" type="number"
                    value={c.threshold} disabled={busy}
                    onChange={event => update(c.key, { threshold: event.target.value })}
                    error={c.threshold.trim() !== "" && !Number.isFinite(Number(c.threshold))} />}
                </Stack>
              </Stack>
            </Box>
          ))}
          <Button startIcon={<AddIcon />} variant="outlined" disabled={busy || channels.length === 0}
            onClick={() => {
              setConditions(current => [...current, {
                key: nextKey, channelId: channels[0]?.channelId ?? "",
                operator: "GreaterThan", threshold: "", value: "",
              }]);
              setNextKey(current => current + 1);
            }}>
            Add condition
          </Button>
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Cancel</Button>
        <Button variant="contained" onClick={() => void submit()} disabled={!valid || busy}>
          {busy ? "Creating..." : "Create breakpoint"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

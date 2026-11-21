
import { useState } from "react";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
} from "@mui/material";

import type { ChannelMetadata } from "./channelMetadata";
import type {
  ComparisonOperator,
  CreateBreakpointRequest,
} from "./breakpointApi";

interface BreakpointDialogProps {
  open: boolean;
  channels: ChannelMetadata[];
  busy: boolean;
  onClose: () => void;
  onCreate: (request: CreateBreakpointRequest) => Promise<void>;
}

const operators: {
  value: ComparisonOperator;
  label: string;
}[] = [
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

export function BreakpointDialog({
  open,
  channels,
  busy,
  onClose,
  onCreate,
}: BreakpointDialogProps) {
  const [channelId, setChannelId] = useState("");
  const [operator, setOperator] =
    useState<ComparisonOperator>("GreaterThan");
  const [threshold, setThreshold] = useState("");

  const selectedType = channels.find(c => c.channelId === channelId)?.valueType ?? "Numeric";
  const allowedOperators = operators.filter(o => selectedType === "Numeric" ||
    (selectedType === "Boolean" && ["Changed", "RisingEdge", "FallingEdge"].includes(o.value)) ||
    (selectedType === "Categorical" && o.value === "Changed"));
  const needsThreshold = selectedType === "Numeric" && operator !== "Changed";
  const numericThreshold = needsThreshold ? Number(threshold) : 0;

  const valid =
    channelId !== "" &&
    channels.some((channel) => channel.channelId === channelId) &&
    (!needsThreshold || threshold.trim() !== "") &&
    Number.isFinite(numericThreshold);

  const handleCreate = async () => {
    if (!valid || busy) return;

    await onCreate({
      channelId,
      operator,
      threshold: numericThreshold,
    });
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose}
      fullWidth maxWidth="sm">
      <DialogTitle>Add Breakpoint</DialogTitle>

      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <FormControl fullWidth size="small">
            <InputLabel>Channel</InputLabel>
            <Select
              label="Channel"
              value={channelId}
              onChange={(event) => {
                setChannelId(event.target.value);
                const nextType = channels.find(c => c.channelId === event.target.value)?.valueType ?? "Numeric";
                if (nextType !== "Numeric") setOperator("Changed");
              }}
            >
              {channels.map((channel) => (
                <MenuItem
                  key={channel.channelId}
                  value={channel.channelId}
                >
                  {channel.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth size="small">
            <InputLabel>Operator</InputLabel>
            <Select
              label="Operator"
              value={operator}
              onChange={(event) =>
                setOperator(event.target.value as ComparisonOperator)
              }
            >
              {allowedOperators.map((item) => (
                <MenuItem key={item.value} value={item.value}>
                  {item.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {needsThreshold && <TextField
            label="Threshold"
            type="number"
            size="small"
            value={threshold}
            onChange={(event) => setThreshold(event.target.value)}
            fullWidth
          />}
        </Stack>
      </DialogContent>

      <DialogActions>
        <Button onClick={onClose} disabled={busy}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleCreate}
          disabled={!valid || busy}
        >
          Create Breakpoint
        </Button>
      </DialogActions>
    </Dialog>
  );
}

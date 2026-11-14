
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  IconButton,
  Stack,
  Switch,
  Tooltip,
  Typography,
} from "@mui/material";

import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import type { Breakpoint, ComparisonOperator } from "./breakpointApi";
import type { ChannelMetadata } from "./channelMetadata";

interface BreakpointPanelProps {
  breakpoints: Breakpoint[];
  channels: ChannelMetadata[];
  loading: boolean;
  busy: boolean;
  error: string | null;
  onAdd: () => void;
  onSetEnabled: (
    breakpointId: string,
    isEnabled: boolean,
  ) => void;
  onDelete: (breakpointId: string) => void;
}

const operatorSymbols: Record<ComparisonOperator, string> = {
  GreaterThan: ">",
  GreaterThanOrEqual: ">=",
  LessThan: "<",
  LessThanOrEqual: "<=",
  Equal: "==",
  NotEqual: "!=",
};

export function BreakpointPanel({
  breakpoints,
  channels,
  loading,
  busy,
  error,
  onAdd,
  onSetEnabled,
  onDelete,
}: BreakpointPanelProps) {
  const channelNames = new Map(
    channels.map((channel) => [
      channel.channelId,
      channel.name,
    ]),
  );

  return (
    <Stack spacing={1.5} sx={{ width: "100%", minWidth: 0 }}>
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 1,
        }}
      >
        <Typography variant="subtitle2">
          Breakpoints ({breakpoints.length})
        </Typography>

        <Button
          size="small"
          startIcon={<AddIcon />}
          onClick={onAdd}
          disabled={loading || busy || channels.length === 0}
        >
          Add
        </Button>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
          <CircularProgress size={24} />
        </Box>
      ) : breakpoints.length === 0 ? (
        <Typography
          variant="body2"
          color="text.secondary"
          sx={{ py: 2 }}
        >
          No breakpoints configured. Add a breakpoint to pause
          replay when a channel meets a condition.
        </Typography>
      ) : (
        <Stack divider={<Divider flexItem />} spacing={0}>
          {breakpoints.map((breakpoint) => {
            const channelName =
              channelNames.get(breakpoint.channelId) ??
              breakpoint.channelId;

            const symbol =
              operatorSymbols[breakpoint.operator] ??
              breakpoint.operator;

            return (
              <Box
                key={breakpoint.breakpointId}
                sx={{
                  display: "flex",
                  alignItems: "center",
                  gap: 0.5,
                  py: 1,
                  opacity: breakpoint.isEnabled ? 1 : 0.6,
                }}
              >
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Tooltip title={channelName}>
                    <Typography
                      variant="body2"
                      fontWeight={500}
                      noWrap
                    >
                      {channelName}
                    </Typography>
                  </Tooltip>

                  <Typography
                    variant="caption"
                    color="text.secondary"
                  >
                    {symbol} {breakpoint.threshold.toLocaleString()}
                  </Typography>
                </Box>

                <Tooltip
                  title={
                    breakpoint.isEnabled
                      ? "Disable breakpoint"
                      : "Enable breakpoint"
                  }
                >
                  <span>
                    <Switch
                      size="small"
                      checked={breakpoint.isEnabled}
                      disabled={busy}
                      onChange={(_, checked) =>
                        onSetEnabled(breakpoint.breakpointId, checked)
                      }
                      inputProps={{
                        "aria-label": `Toggle breakpoint for ${channelName}`,
                      }}
                    />
                  </span>
                </Tooltip>

                <Tooltip title="Delete breakpoint">
                  <span>
                    <IconButton
                      size="small"
                      color="error"
                      disabled={busy}
                      onClick={() =>
                        onDelete(breakpoint.breakpointId)
                      }
                      aria-label={`Delete breakpoint for ${channelName}`}
                    >
                      <DeleteOutlineIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
              </Box>
            );
          })}
        </Stack>
      )}
    </Stack>
  );
}

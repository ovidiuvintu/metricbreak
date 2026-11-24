
import { useMemo, useState } from "react";

import {
  Box,
  Button,
  Checkbox,
  IconButton,
  InputAdornment,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";

import SearchIcon from "@mui/icons-material/Search";
import VisibilityIcon from "@mui/icons-material/Visibility";
import VisibilityOffIcon from "@mui/icons-material/VisibilityOff";

import type { ChannelMetadata } from "./channelMetadata";
import type { AxisPins } from "./chartAxisAssignment";
import { assignChartSeriesColors, getSeriesColor } from "../../theme/chartTheme";

interface ChannelSelectionPanelProps {
  channels: ChannelMetadata[];
  selectedChannelIds: string[];
  axisPins?: AxisPins;
  effectiveRightAxisChannelIds?: string[];
  onSelectionChange: (ids: string[]) => void;
  onAxisPinChange?: (channelId: string, pin: "left" | "right" | "auto") => void;
  onClearAllAxisPins?: () => void;
  /** Axis pin controls. Hidden in the CSV setup wizard. */
  showAxisControls?: boolean;
}

export function ChannelSelectionPanel({
  channels,
  selectedChannelIds,
  axisPins = {},
  effectiveRightAxisChannelIds = [],
  onSelectionChange,
  onAxisPinChange,
  onClearAllAxisPins,
  showAxisControls = true,
}: ChannelSelectionPanelProps) {
  const theme = useTheme();
  const [search, setSearch] = useState("");

  const seriesColorById = useMemo(() => {
    const right = new Set(effectiveRightAxisChannelIds);
    const selected = new Set(selectedChannelIds);
    const inputs = channels
      .map((channel, channelIndex) => ({ channel, channelIndex }))
      .filter(({ channel }) => selected.has(channel.channelId))
      .map(({ channel, channelIndex }) => ({
        channelId: channel.channelId,
        channelName: channel.name,
        channelIndex,
        yAxisIndex: (right.has(channel.channelId) ? 1 : 0) as 0 | 1,
      }));
    const assigned = assignChartSeriesColors(
      theme.palette.mode,
      inputs,
      right.size > 0,
    );
    const colors: Record<string, string> = {};
    channels.forEach((channel, index) => {
      colors[channel.channelId] =
        assigned.get(channel.channelId) ?? getSeriesColor(theme.palette.mode, index);
    });
    return colors;
  }, [channels, selectedChannelIds, effectiveRightAxisChannelIds, theme.palette.mode]);

  const filteredChannels = useMemo(
    () =>
      channels.filter((channel) =>
        channel.name.toLowerCase().includes(search.toLowerCase()),
      ),
    [channels, search],
  );

  const toggleChannel = (channelId: string) => {
    onSelectionChange(
      selectedChannelIds.includes(channelId)
        ? selectedChannelIds.filter((id) => id !== channelId)
        : [...selectedChannelIds, channelId],
    );
  };

  const allSelected =
    channels.length > 0 &&
    selectedChannelIds.length === channels.length;

  const rightAxisSet = useMemo(
    () => new Set(effectiveRightAxisChannelIds),
    [effectiveRightAxisChannelIds],
  );

  const hasPins = Object.keys(axisPins).length > 0;

  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        height: "100%",
        minHeight: 0,
        gap: 1,
      }}
    >
      <TextField
        size="small"
        fullWidth
        placeholder="Search channels"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" />
              </InputAdornment>
            ),
          },
        }}
      />

      <Box sx={{ display: "flex", flexDirection: "column", gap: 0.75, px: 0.5 }}>
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 1,
          }}
        >
          <Typography
            variant="caption"
            sx={{ color: "text.primary", fontWeight: 600, fontSize: "0.75rem" }}
          >
            {showAxisControls
              ? `${selectedChannelIds.length} of ${channels.length} on chart`
              : `${selectedChannelIds.length} of ${channels.length} selected`}
          </Typography>

          {showAxisControls ? (
          <Tooltip title={allSelected ? "Hide all" : "Show all"}>
            <IconButton
              size="small"
              onClick={() =>
                onSelectionChange(
                  allSelected
                    ? []
                    : channels.map((channel) => channel.channelId),
                )
              }
            >
              {allSelected ? (
                <VisibilityOffIcon fontSize="small" />
              ) : (
                <VisibilityIcon fontSize="small" />
              )}
            </IconButton>
          </Tooltip>
          ) : (
            <Box sx={{ display: "flex", gap: 0.5 }}>
              <Button
                size="small"
                disabled={allSelected || channels.length === 0}
                onClick={() =>
                  onSelectionChange(channels.map((channel) => channel.channelId))
                }
              >
                Select all
              </Button>
              <Button
                size="small"
                disabled={selectedChannelIds.length === 0}
                onClick={() => onSelectionChange([])}
              >
                Clear
              </Button>
            </Box>
          )}
        </Box>

        {showAxisControls && (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 1,
            bgcolor: "action.hover",
            borderRadius: 0.75,
            px: 1,
            py: 0.5,
          }}
        >
          <Typography
            variant="caption"
            component="div"
            sx={{
              fontSize: "0.72rem",
              lineHeight: 1.45,
              color: "text.secondary",
            }}
          >
            <Box component="span" sx={{ fontWeight: 700, color: "text.primary" }}>
              A
            </Box>
            {" = auto axis from scale"}
            <br />
            <Box component="span" sx={{ fontWeight: 700, color: "text.primary" }}>
              L
            </Box>
            {" / "}
            <Box component="span" sx={{ fontWeight: 700, color: "text.primary" }}>
              R
            </Box>
            {" = pin left (solid) or right (dashed)"}
          </Typography>
          {onClearAllAxisPins && (
            <Button
              size="small"
              variant="text"
              disabled={!hasPins}
              onClick={onClearAllAxisPins}
              sx={{
                flexShrink: 0,
                p: 0,
                minWidth: 0,
                whiteSpace: "nowrap",
                visibility: hasPins ? "visible" : "hidden",
              }}
            >
              Reset all to auto
            </Button>
          )}
        </Box>
        )}
      </Box>

      <Box
        sx={{
          flex: 1,
          minHeight: 0,
          overflowY: "auto",
          scrollbarGutter: "stable",
          overflowAnchor: "none",
        }}
      >
        {filteredChannels.map((channel) => {
          const checked = selectedChannelIds.includes(
            channel.channelId,
          );

          return (
            <Box
              key={channel.channelId}
              onClick={() => toggleChannel(channel.channelId)}
              sx={{
                display: "flex",
                alignItems: "center",
                minHeight: 36,
                borderRadius: 1,
                cursor: "pointer",
                "&:hover": {
                  bgcolor: "action.hover",
                },
              }}
            >
              <Checkbox
                size="small"
                checked={checked}
                tabIndex={-1}
                onClick={(event) => event.stopPropagation()}
                onChange={() => toggleChannel(channel.channelId)}
                slotProps={{
                  input: { "aria-label": channel.name },
                }}
              />

              <Box
                aria-hidden
                sx={{
                  width: 8,
                  height: 8,
                  borderRadius: "50%",
                  bgcolor: seriesColorById[channel.channelId],
                  flexShrink: 0,
                  mr: 1,
                }}
              />

              <Box sx={{ minWidth: 0, flex: 1 }}>
                <Typography
                  variant="body2"
                  noWrap
                  title={channel.name}
                  color={checked ? "text.primary" : "text.secondary"}
                >
                  {channel.name}
                </Typography>

                {channel.unit && (
                  <Typography
                    variant="caption"
                    color="text.secondary"
                  >
                    {channel.unit}
                  </Typography>
                )}
              </Box>

              {showAxisControls && checked && onAxisPinChange && (
                <ToggleButtonGroup
                  onClick={(event) => event.stopPropagation()}
                  size="small"
                  exclusive
                  value={axisPins[channel.channelId] ?? "auto"}
                  onChange={(_, value: "left" | "right" | "auto" | null) => {
                    if (value) onAxisPinChange(channel.channelId, value);
                  }}
                  aria-label={`Y-axis for ${channel.name}`}
                  sx={{
                    flexShrink: 0,
                    ml: 0.5,
                    "& .MuiToggleButton-root": {
                      px: 0.75,
                      py: 0.15,
                      fontSize: "0.65rem",
                      fontWeight: 700,
                      lineHeight: 1.2,
                      minWidth: 26,
                    },
                  }}
                >
                  <Tooltip
                    title={
                      rightAxisSet.has(channel.channelId)
                        ? "Auto (currently right)"
                        : "Auto (currently left)"
                    }
                  >
                    <ToggleButton value="auto" aria-label="Auto Y-axis">
                      A
                    </ToggleButton>
                  </Tooltip>
                  <Tooltip title="Pin left Y-axis (solid)">
                    <ToggleButton value="left" aria-label="Left Y-axis">
                      L
                    </ToggleButton>
                  </Tooltip>
                  <Tooltip title="Pin right Y-axis (dashed)">
                    <ToggleButton value="right" aria-label="Right Y-axis">
                      R
                    </ToggleButton>
                  </Tooltip>
                </ToggleButtonGroup>
              )}
            </Box>
          );
        })}
      </Box>
    </Box>
  );
}

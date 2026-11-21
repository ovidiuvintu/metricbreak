
import { useMemo, useState } from "react";

import {
  Box,
  Checkbox,
  IconButton,
  InputAdornment,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";

import SearchIcon from "@mui/icons-material/Search";
import VisibilityIcon from "@mui/icons-material/Visibility";
import VisibilityOffIcon from "@mui/icons-material/VisibilityOff";

import type { ChannelMetadata } from "./channelMetadata";

interface ChannelSelectionPanelProps {
  channels: ChannelMetadata[];
  selectedChannelIds: string[];
  rightAxisChannelIds?: string[];
  onSelectionChange: (ids: string[]) => void;
  onChartAxisChange?: (channelId: string, side: "left" | "right") => void;
}

export function ChannelSelectionPanel({
  channels,
  selectedChannelIds,
  rightAxisChannelIds = [],
  onSelectionChange,
  onChartAxisChange,
}: ChannelSelectionPanelProps) {
  const [search, setSearch] = useState("");

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
    () => new Set(rightAxisChannelIds),
    [rightAxisChannelIds],
  );

  const dualAxisActive = selectedChannelIds.some((id) => rightAxisSet.has(id));

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
            {selectedChannelIds.length} of {channels.length} on chart
          </Typography>

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
        </Box>

        {dualAxisActive && (
          <Typography
            variant="caption"
            component="div"
            sx={{
              fontSize: "0.72rem",
              lineHeight: 1.45,
              color: "text.secondary",
              bgcolor: "action.hover",
              borderRadius: 0.75,
              px: 1,
              py: 0.5,
            }}
          >
            <Box component="span" sx={{ fontWeight: 700, color: "text.primary" }}>
              L
            </Box>
            {" = left axis, solid"}
            <br />
            <Box component="span" sx={{ fontWeight: 700, color: "text.primary" }}>
              R
            </Box>
            {" = right axis, dashed"}
          </Typography>
        )}
      </Box>

      <Box
        sx={{
          flex: 1,
          minHeight: 0,
          overflowY: "auto",
        }}
      >
        {filteredChannels.map((channel) => {
          const checked = selectedChannelIds.includes(
            channel.channelId,
          );

          return (
            <Box
              key={channel.channelId}
              sx={{
                display: "flex",
                alignItems: "center",
                minHeight: 36,
                borderRadius: 1,
                "&:hover": {
                  bgcolor: "action.hover",
                },
              }}
            >
              <Checkbox
                size="small"
                checked={checked}
                onChange={() => toggleChannel(channel.channelId)}
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

              {checked && onChartAxisChange && (
                <ToggleButtonGroup
                  size="small"
                  exclusive
                  value={rightAxisSet.has(channel.channelId) ? "right" : "left"}
                  onChange={(_, value: "left" | "right" | null) => {
                    if (value) onChartAxisChange(channel.channelId, value);
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
                  <Tooltip title="Left Y-axis (solid)">
                    <ToggleButton value="left" aria-label="Left Y-axis">
                      L
                    </ToggleButton>
                  </Tooltip>
                  <Tooltip title="Right Y-axis (dashed)">
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


import { useMemo, useState } from "react";

import {
  Box,
  Checkbox,
  IconButton,
  InputAdornment,
  TextField,
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
  onSelectionChange: (ids: string[]) => void;
}

export function ChannelSelectionPanel({
  channels,
  selectedChannelIds,
  onSelectionChange,
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

      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          px: 0.5,
        }}
      >
        <Typography variant="caption" color="text.secondary">
          {selectedChannelIds.length} of {channels.length} visible
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
            </Box>
          );
        })}
      </Box>
    </Box>
  );
}

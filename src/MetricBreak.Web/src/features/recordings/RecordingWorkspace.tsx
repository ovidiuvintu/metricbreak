
import { useState, type ReactNode } from "react";

import {
  Box,
  Divider,
  IconButton,
  Paper,
  Tooltip,
  Typography,
} from "@mui/material";

import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";

interface RecordingWorkspaceProps {
  toolbar: ReactNode;
  chart: ReactNode;
  channels: ReactNode;
  replayControls?: ReactNode;
  details?: ReactNode;
}

export function RecordingWorkspace({
  toolbar,
  chart,
  channels,
  replayControls,
  details,
}: RecordingWorkspaceProps) {
  const [channelsCollapsed, setChannelsCollapsed] =
    useState(false);

  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        flex: 1,
        width: "100%",
        height: "100%",
        minHeight: 0,
        minWidth: 0,
        gap: 1,
      }}
    >
      {/* Compact toolbar */}
      <Box
        sx={{
          flexShrink: 0,
          minHeight: 40,
          display: "flex",
          alignItems: "center",
        }}
      >
        {toolbar}
      </Box>

      {/* Main visualization workspace */}
      <Paper
        variant="outlined"
        sx={{
          display: "flex",
          flex: 1,
          minHeight: 0,
          minWidth: 0,
          overflow: "hidden",
          borderRadius: 1,
        }}
      >
        {/* Chart and replay controls */}
        <Box
          sx={{
            display: "flex",
            flexDirection: "column",
            flex: 1,
            minWidth: 0,
            minHeight: 0,
            overflow: "hidden",
          }}
        >
          <Box
            sx={{
              flex: 1,
              minWidth: 0,
              minHeight: 0,
              overflow: "hidden",
              p: 1,
            }}
          >
            {chart}
          </Box>

          {replayControls && (
            <>
              <Divider />

              <Box
                sx={{
                  flexShrink: 0,
                  px: 2,
                  py: 1,
                }}
              >
                {replayControls}
              </Box>
            </>
          )}
        </Box>

        {/* Collapsible channels panel */}
        <Box
          component="aside"
          sx={{
            display: "flex",
            flexDirection: "column",
            width: channelsCollapsed ? 48 : 280,
            flexShrink: 0,
            minWidth: 0,
            minHeight: 0,
            overflow: "hidden",
            borderLeft: 1,
            borderColor: "divider",
            bgcolor: "background.paper",
            transition: (theme) =>
              theme.transitions.create("width", {
                duration:
                  theme.transitions.duration.shorter,
              }),
          }}
        >
          <Box
            sx={{
              display: "flex",
              alignItems: "center",
              justifyContent: channelsCollapsed
                ? "center"
                : "space-between",
              px: 1,
              py: 0.5,
              minHeight: 44,
              flexShrink: 0,
            }}
          >
            {!channelsCollapsed && (
              <Typography
                variant="subtitle2"
                fontWeight={600}
              >
                Channels
              </Typography>
            )}

            <Tooltip
              title={
                channelsCollapsed
                  ? "Expand channels"
                  : "Collapse channels"
              }
            >
              <IconButton
                size="small"
                onClick={() =>
                  setChannelsCollapsed((value) => !value)
                }
                aria-label={
                  channelsCollapsed
                    ? "Expand channels"
                    : "Collapse channels"
                }
              >
                {channelsCollapsed ? (
                  <ChevronLeftIcon fontSize="small" />
                ) : (
                  <ChevronRightIcon fontSize="small" />
                )}
              </IconButton>
            </Tooltip>
          </Box>

          {!channelsCollapsed && (
            <>
              <Divider />

              <Box
                sx={{
                  flex: 1,
                  minHeight: 0,
                  overflowY: "auto",
                  overflowX: "hidden",
                  p: 1,
                }}
              >
                {channels}
              </Box>
            </>
          )}
        </Box>
      </Paper>

      {/* Optional compact details/status bar */}
      {details && (
        <Box
          sx={{
            flexShrink: 0,
            minHeight: 28,
          }}
        >
          {details}
        </Box>
      )}
    </Box>
  );
}

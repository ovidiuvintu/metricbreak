
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
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

interface RecordingWorkspaceProps {
  toolbar: ReactNode;
  chart: ReactNode;
  channels: ReactNode;
  replayControls?: ReactNode;
  currentValues?: ReactNode;
  details?: ReactNode;
}

export function RecordingWorkspace({
  toolbar,
  chart,
  channels,
  replayControls,
  currentValues,
  details,
}: RecordingWorkspaceProps) {
  const [channelsCollapsed, setChannelsCollapsed] =
    useState(false);

  const [valuesCollapsed, setValuesCollapsed] =
    useState(true);

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
      {/* Toolbar */}
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

      {/* Main workspace */}
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
        {/* Chart, replay controls and values */}
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
          {/* Chart */}
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

          {/* Replay controls */}
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

          {/* Current Values */}
          {currentValues && (
            <>
              <Divider />

              <Box
                sx={{
                  flexShrink: 0,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  px: 2,
                  minHeight: 40,
                  bgcolor: "background.paper",
                }}
              >
                <Typography
                  variant="subtitle2"
                  fontWeight={600}
                >
                  Current Values
                </Typography>

                <Tooltip
                  title={
                    valuesCollapsed
                      ? "Expand current values"
                      : "Collapse current values"
                  }
                >
                  <IconButton
                    size="small"
                    aria-label={
                      valuesCollapsed
                        ? "Expand current values"
                        : "Collapse current values"
                    }
                    onClick={() =>
                      setValuesCollapsed((value) => !value)
                    }
                  >
                    {valuesCollapsed ? (
                      <ExpandLessIcon fontSize="small" />
                    ) : (
                      <ExpandMoreIcon fontSize="small" />
                    )}
                  </IconButton>
                </Tooltip>
              </Box>

              {!valuesCollapsed && (
                <Box
                  sx={{
                    height: 220,
                    minHeight: 100,
                    maxHeight: "60%",
                    resize: "vertical",
                    overflow: "auto",
                    borderTop: 1,
                    borderColor: "divider",
                    p: 1,
                  }}
                >
                  {currentValues}
                </Box>
              )}
            </>
          )}
        </Box>

        {/* Inspector sidebar */}
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
                Inspector
              </Typography>
            )}

            <Tooltip
              title={
                channelsCollapsed
                  ? "Expand inspector"
                  : "Collapse inspector"
              }
            >
              <IconButton
                size="small"
                onClick={() =>
                  setChannelsCollapsed((value) => !value)
                }
                aria-label={
                  channelsCollapsed
                    ? "Expand inspector"
                    : "Collapse inspector"
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

      {/* Status bar */}
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

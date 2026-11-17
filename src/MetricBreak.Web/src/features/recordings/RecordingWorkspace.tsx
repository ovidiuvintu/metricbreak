
import { useRef, useState, type ReactNode, type PointerEvent, type KeyboardEvent } from "react";

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

  const DEFAULT_INSPECTOR_WIDTH = 280;
  const MIN_INSPECTOR_WIDTH = 280;
  const MIN_CHART_WIDTH = 400;
  const SPLITTER_WIDTH = 8;
  const workspaceRef = useRef<HTMLDivElement | null>(null);
  const dragPointerIdRef = useRef<number | null>(null);
  const [inspectorWidth, setInspectorWidth] = useState(DEFAULT_INSPECTOR_WIDTH);
  const [isDragging, setIsDragging] = useState(false);

  const resizeInspector = (clientX: number) => {
    const bounds = workspaceRef.current?.getBoundingClientRect();
    if (!bounds) return;
    const available = bounds.width - SPLITTER_WIDTH;
    // On narrow viewports, keep both panes usable without exceeding the container.
    const maximum = Math.max(0, available - MIN_CHART_WIDTH);
    const minimum = Math.min(MIN_INSPECTOR_WIDTH, maximum);
    const requested = bounds.right - clientX;
    setInspectorWidth(Math.max(minimum, Math.min(maximum, requested)));
  };

  const handleSplitterPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0 || channelsCollapsed) return;
    event.preventDefault();
    dragPointerIdRef.current = event.pointerId;
    event.currentTarget.setPointerCapture(event.pointerId);
    setIsDragging(true);
  };

  const handleSplitterPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    if (dragPointerIdRef.current !== event.pointerId) return;
    resizeInspector(event.clientX);
  };

  const stopDragging = (event: PointerEvent<HTMLDivElement>) => {
    if (dragPointerIdRef.current !== event.pointerId) return;
    dragPointerIdRef.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    setIsDragging(false);
  };

  const handleSplitterKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (channelsCollapsed) return;
    const step = event.shiftKey ? 50 : 10;
    if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
      event.preventDefault();
      const bounds = workspaceRef.current?.getBoundingClientRect();
      if (!bounds) return;
      const maximum = Math.max(0, bounds.width - SPLITTER_WIDTH - MIN_CHART_WIDTH);
      const minimum = Math.min(MIN_INSPECTOR_WIDTH, maximum);
      const delta = event.key === "ArrowLeft" ? step : -step;
      setInspectorWidth((width) => Math.max(minimum, Math.min(maximum, width + delta)));
    } else if (event.key === "Home") {
      event.preventDefault();
      setInspectorWidth(DEFAULT_INSPECTOR_WIDTH);
    }
  };

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
        ref={workspaceRef}
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

        {/* Resizable boundary between chart and inspector */}
        {!channelsCollapsed && (
          <Box
            role="separator"
            aria-label="Resize chart and inspector"
            aria-orientation="vertical"
            tabIndex={0}
            onPointerDown={handleSplitterPointerDown}
            onPointerMove={handleSplitterPointerMove}
            onPointerUp={stopDragging}
            onPointerCancel={stopDragging}
            onLostPointerCapture={() => {
              dragPointerIdRef.current = null;
              setIsDragging(false);
            }}
            onKeyDown={handleSplitterKeyDown}
            onDoubleClick={() => setInspectorWidth(DEFAULT_INSPECTOR_WIDTH)}
            sx={{
              width: SPLITTER_WIDTH,
              flexShrink: 0,
              cursor: "col-resize",
              touchAction: "none",
              bgcolor: isDragging ? "action.selected" : "background.paper",
              borderLeft: 1,
              borderRight: 1,
              borderColor: "divider",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              "&:hover, &:focus-visible": { bgcolor: "action.hover" },
              "&::after": {
                content: '""',
                height: 40,
                width: 3,
                borderRadius: 2,
                bgcolor: "text.disabled",
              },
            }}
          />
        )}

        {/* Inspector sidebar */}
        <Box
          component="aside"
          sx={{
            display: "flex",
            flexDirection: "column",
            width: channelsCollapsed ? 48 : inspectorWidth,
            flexShrink: 0,
            minWidth: 0,
            minHeight: 0,
            overflow: "hidden",
            borderLeft: channelsCollapsed ? 1 : 0,
            borderColor: "divider",
            bgcolor: "background.paper",
            transition: isDragging ? "none" : undefined,
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

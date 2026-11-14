
import {
  Box,
  IconButton,
  Tooltip,
  Typography,
} from "@mui/material";

import SkipPreviousIcon from "@mui/icons-material/SkipPrevious";
import NavigateBeforeIcon from "@mui/icons-material/NavigateBefore";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";

interface ReplayControlsProps {
  currentSequence: number;
  sampleCount: number;
  busy: boolean;
  onStepForward: () => void;
  onStepBackward: () => void;
  onReset: () => void;
  onContinue: () => void;
}

export function ReplayControls({
  currentSequence,
  sampleCount,
  busy,
  onStepForward,
  onStepBackward,
  onReset,
  onContinue,
}: ReplayControlsProps) {
  const atStart = currentSequence <= 0;
  const atEnd =
    sampleCount === 0 ||
    currentSequence >= sampleCount - 1;

  return (
    <Box
      sx={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 2,
        minHeight: 40,
      }}
    >
      <Box sx={{ display: "flex", gap: 0.5 }}>
        <Tooltip title="Reset to beginning">
          <span>
            <IconButton
              size="small"
              disabled={busy || atStart}
              onClick={onReset}
              aria-label="Reset replay"
            >
              <SkipPreviousIcon />
            </IconButton>
          </span>
        </Tooltip>

        <Tooltip title="Step backward">
          <span>
            <IconButton
              size="small"
              disabled={busy || atStart}
              onClick={onStepBackward}
              aria-label="Step backward"
            >
              <NavigateBeforeIcon />
            </IconButton>
          </span>
        </Tooltip>

        <Tooltip title="Step forward">
          <span>
            <IconButton
              size="small"
              disabled={busy || atEnd}
              onClick={onStepForward}
              aria-label="Step forward"
            >
              <NavigateNextIcon />
            </IconButton>
          </span>
        </Tooltip>

        <Tooltip title="Continue to next breakpoint">
          <span>
            <IconButton
              size="small"
              disabled={busy || atEnd}
              onClick={onContinue}
              aria-label="Continue replay"
            >
                <PlayArrowIcon />
            </IconButton>
          </span>
        </Tooltip>
      </Box>

      <Typography
        variant="caption"
        color="text.secondary"
        sx={{ fontVariantNumeric: "tabular-nums" }}
      >
        Sequence {currentSequence.toLocaleString()} /{" "}
        {Math.max(0, sampleCount - 1).toLocaleString()}
      </Typography>
    </Box>
  );
}

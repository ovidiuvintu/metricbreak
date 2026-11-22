import { useEffect, useMemo, useState, type RefObject } from "react";
import { createPortal } from "react-dom";
import { Box, LinearProgress, Tooltip, Typography } from "@mui/material";

import {
  breakpointApi,
  isRecordingGap,
  type Breakpoint,
  type BreakpointHitOccurrence,
  type CompositeBreakpoint,
} from "./breakpointApi";
import type { ChannelMetadata } from "./channelMetadata";

const HIT_COLORS = [
  "#FF6B8A",
  "#4EA8FF",
  "#49D6C5",
  "#E5C95C",
  "#C792EA",
  "#F97316",
  "#78D47C",
  "#94A3B8",
];

function colorForBreakpoint(colorIndex: number): string {
  return HIT_COLORS[colorIndex % HIT_COLORS.length];
}

interface BreakpointHitTimelineProps {
  sessionId: string;
  pageStart: number;
  pageEnd: number;
  sampleCount: number;
  currentSequence: number | null;
  breakpoints: Breakpoint[];
  compositeBreakpoints: CompositeBreakpoint[];
  channels: ChannelMetadata[];
  refreshKey?: number;
  /** X-axis line in the chart, so hit marks share its scale and sit on it. */
  plotFrame?: {
    left: number;
    width: number;
    axisTop: number;
    min: number;
    max: number;
  } | null;
  plotAnchorRef?: RefObject<HTMLElement | null>;
  onHitClick: (sampleSequence: number, breakpointId: string) => void;
}

export function BreakpointHitTimeline({
  sessionId,
  pageStart,
  pageEnd,
  sampleCount,
  currentSequence,
  breakpoints,
  compositeBreakpoints,
  channels,
  refreshKey = 0,
  plotFrame = null,
  plotAnchorRef,
  onHitClick,
}: BreakpointHitTimelineProps) {
  const [hits, setHits] = useState<BreakpointHitOccurrence[]>([]);
  const [truncated, setTruncated] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const channelNames = useMemo(
    () => new Map(channels.map((channel) => [channel.channelId, channel.name])),
    [channels],
  );

  const enabledBreakpoints = useMemo(
  () => [
      ...breakpoints.filter((item) => item.isEnabled),
      ...compositeBreakpoints.filter((item) => item.isEnabled),
    ],
    [breakpoints, compositeBreakpoints],
  );

  const breakpointLabels = useMemo(() => {
    const map = new Map<string, string>();
    for (const bp of breakpoints) {
      if (bp.operator === "MissingLongerThan") {
        const scope = isRecordingGap(bp.channelId)
          ? "Recording"
          : (channelNames.get(bp.channelId) ?? bp.channelId);
        map.set(bp.breakpointId, `${scope} missing > ${bp.threshold}s`);
        continue;
      }
      const name = channelNames.get(bp.channelId) ?? bp.channelId;
      map.set(bp.breakpointId, `${name} ${bp.operator}`);
    }
    for (const bp of compositeBreakpoints) {
      map.set(
        bp.breakpointId,
        `Composite (${bp.logicalOperator})`,
      );
    }
    return map;
  }, [breakpoints, compositeBreakpoints, channelNames]);

  const colorIndexById = useMemo(() => {
    const map = new Map<string, number>();
    enabledBreakpoints.forEach((bp, index) => {
      map.set(bp.breakpointId, index);
    });
    return map;
  }, [enabledBreakpoints]);

  useEffect(() => {
    if (enabledBreakpoints.length === 0) {
      setHits([]);
      setTruncated(false);
      setError(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    void (async () => {
      try {
        const response = await breakpointApi.listHits(
          sessionId,
          pageStart,
          pageEnd,
        );
        if (cancelled) return;
        setHits(response.hits);
        setTruncated(response.truncated);
      } catch (err) {
        if (!cancelled) {
          setHits([]);
          setTruncated(false);
          setError(err instanceof Error ? err.message : "Unable to load breakpoint hits.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [sessionId, pageStart, pageEnd, enabledBreakpoints.length, refreshKey]);

  if (enabledBreakpoints.length === 0) {
    return null;
  }

  const domainMin = plotFrame?.min ?? pageStart;
  const domainMax = plotFrame?.max ?? Math.max(pageStart + 1, pageEnd - 1);
  const span = Math.max(0.001, domainMax - domainMin);
  const percentFor = (sequence: number): number | null => {
    const percent = ((sequence - domainMin) / span) * 100;
    if (percent < -0.5 || percent > 100.5) return null;
    return Math.max(0, Math.min(100, percent));
  };
  const cursorPercent = currentSequence == null ? null : percentFor(currentSequence);
  const visibleHits = hits.filter((hit) => percentFor(hit.sampleSequence) != null);

  const anchor = plotAnchorRef?.current ?? null;
  const marks = (
    <Box
      role="list"
      aria-label="Breakpoint hit timeline"
      sx={{
        position: "absolute",
        left: plotFrame ? `${plotFrame.left}px` : 0,
        width: plotFrame ? `${plotFrame.width}px` : "100%",
        top: plotFrame ? `${plotFrame.axisTop}px` : undefined,
        height: 14,
        transform: plotFrame ? "translateY(-50%)" : undefined,
        zIndex: 8,
        pointerEvents: "none",
      }}
    >
      {cursorPercent != null && (
        <Box
          aria-hidden
          sx={{
            position: "absolute",
            top: 0,
            bottom: 0,
            left: `${cursorPercent}%`,
            width: "2px",
            transform: "translateX(-50%)",
            bgcolor: "warning.main",
            pointerEvents: "none",
            zIndex: 2,
          }}
        />
      )}
      {hits.map((hit) => {
        const percent = percentFor(hit.sampleSequence);
        if (percent == null) return null;
        const colorIndex = colorIndexById.get(hit.breakpointId) ?? 0;
        const label = breakpointLabels.get(hit.breakpointId) ?? hit.breakpointId;

        return (
          <Tooltip
            key={`${hit.breakpointId}-${hit.sampleSequence}`}
            title={`${label} · #${hit.sampleSequence.toLocaleString()} (click to seek)`}
          >
            <Box
              component="button"
              type="button"
              role="listitem"
              aria-label={`${label} at sample ${hit.sampleSequence}`}
              onPointerDown={(event) => event.stopPropagation()}
              onClick={() => onHitClick(hit.sampleSequence, hit.breakpointId)}
              sx={{
                position: "absolute",
                top: 0,
                bottom: 0,
                left: `${percent}%`,
                width: 6,
                transform: "translateX(-50%)",
                border: 0,
                borderRadius: 0.5,
                p: 0,
                cursor: "pointer",
                pointerEvents: "auto",
                bgcolor: colorForBreakpoint(colorIndex),
                opacity: 0.95,
                zIndex: 1,
                "&:hover": { opacity: 1, boxShadow: 2 },
              }}
            />
          </Tooltip>
        );
      })}
    </Box>
  );

  return (
    <Box sx={{ flexShrink: 0, pb: 0.25 }}>
      {loading && <LinearProgress sx={{ height: 2 }} />}
      {error && (
        <Typography variant="caption" color="error" sx={{ px: 0.5 }}>
          {error}
        </Typography>
      )}
      {plotFrame && anchor ? createPortal(marks, anchor) : null}
      <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 0.75, mt: 0.25, px: 0.5 }}>
        <Typography variant="caption" color="text.secondary">
          {loading
            ? "Scanning breakpoint hits…"
            : !error && visibleHits.length === 0
              ? hits.length === 0
                ? `No hits on samples ${(pageStart + 1).toLocaleString()}–${pageEnd.toLocaleString()}`
                : "No hits in the current zoom"
              : `Breakpoint hits on this page${truncated ? " (truncated)" : ""}`}
        </Typography>
        {enabledBreakpoints.slice(0, 6).map((bp) => {
          const colorIndex = colorIndexById.get(bp.breakpointId) ?? 0;
          return (
            <Box key={bp.breakpointId} sx={{ display: "flex", alignItems: "center", gap: 0.35 }}>
              <Box
                sx={{
                  width: 8,
                  height: 8,
                  borderRadius: 0.5,
                  bgcolor: colorForBreakpoint(colorIndex),
                }}
              />
              <Typography variant="caption" color="text.secondary" noWrap sx={{ maxWidth: 140 }}>
                {breakpointLabels.get(bp.breakpointId) ?? bp.breakpointId}
              </Typography>
            </Box>
          );
        })}
        {sampleCount > 0 && (
          <Typography variant="caption" color="text.disabled" sx={{ ml: "auto" }}>
            Amber line = replay cursor
          </Typography>
        )}
      </Box>
    </Box>
  );
}
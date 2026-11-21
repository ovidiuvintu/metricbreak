import { useEffect, useMemo, useState } from "react";
import { Box, LinearProgress, Tooltip, Typography } from "@mui/material";

import { breakpointApi, type Breakpoint, type BreakpointHitOccurrence, type CompositeBreakpoint } from "./breakpointApi";
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

  const span = Math.max(1, pageEnd - pageStart);
  const cursorPercent = currentSequence == null
    ? null
    : Math.max(0, Math.min(100, ((currentSequence - pageStart) / span) * 100));

  return (
    <Box sx={{ flexShrink: 0, px: 0.5, pb: 0.25 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1, mb: 0.25 }}>
        <Typography variant="caption" color="text.secondary">
          Breakpoint hits on this page
          {truncated ? " (truncated)" : ""}
        </Typography>
        {loading && (
          <Typography variant="caption" color="text.secondary">
            Scanning…
          </Typography>
        )}
      </Box>
      {loading && <LinearProgress sx={{ mb: 0.25, height: 2 }} />}
      {error && (
        <Typography variant="caption" color="error">
          {error}
        </Typography>
      )}
      <Box
        role="list"
        aria-label="Breakpoint hit timeline"
        sx={{
          position: "relative",
          height: 28,
          borderRadius: 0.5,
          border: 1,
          borderColor: "divider",
          bgcolor: "action.hover",
          overflow: "hidden",
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
              opacity: 0.9,
              pointerEvents: "none",
              zIndex: 2,
            }}
          />
        )}
        {hits.map((hit) => {
          const percent = Math.max(0, Math.min(100, ((hit.sampleSequence - pageStart) / span) * 100));
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
                onClick={() => onHitClick(hit.sampleSequence, hit.breakpointId)}
                sx={{
                  position: "absolute",
                  top: 2,
                  bottom: 2,
                  left: `${percent}%`,
                  width: 6,
                  transform: "translateX(-50%)",
                  border: 0,
                  borderRadius: 0.5,
                  p: 0,
                  cursor: "pointer",
                  bgcolor: colorForBreakpoint(colorIndex),
                  opacity: 0.92,
                  zIndex: 1,
                  "&:hover": { opacity: 1, boxShadow: 2 },
                }}
              />
            </Tooltip>
          );
        })}
        {!loading && hits.length === 0 && !error && (
          <Typography
            variant="caption"
            color="text.secondary"
            sx={{
              position: "absolute",
              inset: 0,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            No hits on samples {(pageStart + 1).toLocaleString()}–{pageEnd.toLocaleString()}
          </Typography>
        )}
      </Box>
      <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75, mt: 0.25 }}>
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
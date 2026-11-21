import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  type PointerEvent,
  useState,
} from "react";
import type { MouseEvent as ReactMouseEvent } from "react";

import ReactECharts from "echarts-for-react";
import type { EChartsType } from "echarts/core";

import {
  Alert,
  Button,
  ButtonGroup,
  Checkbox,
  ListItemText,
  Menu,
  MenuItem,
  Divider,
  Box,
  CircularProgress,
  Typography,
} from "@mui/material";

import { useTheme } from "@mui/material/styles";

import { recordingApi } from "./recordingApi";
import type { ChannelMetadata } from "./channelMetadata";
import type { SampleRangeItem } from "./sampleTypes";
import {
  assignChartSeriesColors,
  createChartTheme,
  getAxisTemperatureAccent,
  getSeriesColor,
} from "../../theme/chartTheme";

interface RecordingChartProps {
  sessionId: string;
  sampleCount: number;
  channels: ChannelMetadata[];
  selectedChannelIds: string[];
  currentSequence: number | null;
  currentTimestampNanoseconds?: number | string | null;
  timestampOrigin?: "Unix" | "Elapsed";
  onSeek: (sequence: number) => void;
  onScrubStart: () => void;
  onScrubEnd: (sequence: number) => void;
}

type HoverDetails = {
  sequence: number;
  timestamp: string;
  x: number;
  y: number;
  values: { name: string; value: string }[];
};

function formatTimestamp(
  nanoseconds: string,
  origin: "Unix" | "Elapsed" = "Unix",
): string {
  if (origin === "Elapsed") {
    return formatElapsed(nanoseconds);
  }

  try {
    // Divide before converting to Number to avoid losing nanosecond precision.
    const milliseconds = BigInt(nanoseconds) / 1_000_000n;
    const date = new Date(Number(milliseconds));
    return Number.isNaN(date.getTime()) ? nanoseconds : date.toISOString();
  } catch {
    return nanoseconds;
  }
}

function formatElapsed(nanoseconds: string): string {
  try {
    const value = BigInt(nanoseconds);
    const negative = value < 0n;
    const magnitude = negative ? -value : value;
    const seconds = magnitude / 1_000_000_000n;
    const fraction = (magnitude % 1_000_000_000n)
      .toString()
      .padStart(9, "0")
      .replace(/0+$/, "");
    const body = fraction.length === 0
      ? seconds.toString()
      : `${seconds}.${fraction}`;
    return `${negative ? "-" : ""}${body} s`;
  } catch {
    return nanoseconds;
  }
}

function formatSampleValue(sample: SampleRangeItem, channelId: string): string {
  const typed = sample.typedChannels?.[channelId];
  if (typed) {
    if (typed.valueType === "Missing" || typed.value === null) return "N/A";
    if (typed.valueType === "Boolean") return typed.value ? "True" : "False";
    if (typed.valueType === "Numeric") return typed.value.toLocaleString();
    return typed.value;
  }
  const numeric = sample.channels[channelId];
  return numeric == null ? "N/A" : numeric.toLocaleString();
}

// The numeric projection may contain a default zero for a typed Missing value.
// Always consult the typed representation before drawing a point.
function numericPlotValue(sample: SampleRangeItem, channelId: string): number | null {
  const typedSample = sample as SampleRangeItem & {
    typedChannels?: Record<string, { valueType: string; value: unknown } | undefined>;
  };
  if (typedSample.typedChannels?.[channelId]?.valueType === "Missing") return null;
  const value = sample.channels[channelId];
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

// Mark windows where all plotted channels lack data. Individual channel gaps
// remain visible as broken lines without falsely labeling valid data "No Data".
function findNoDataWindows(samples: SampleRangeItem[], channelIds: string[]) {
  const windows: { start: number; end: number }[] = [];
  if (!channelIds.length) return windows;
  let start: number | null = null;
  let previous: number | null = null;
  for (const sample of samples) {
    const missing = channelIds.every((id) => numericPlotValue(sample, id) === null);
    if (start !== null && (!missing || (previous !== null && sample.sequence !== previous + 1))) {
      windows.push({ start, end: previous! });
      start = null;
    }
    if (missing && start === null) start = sample.sequence;
    previous = sample.sequence;
  }
  if (start !== null && previous !== null) windows.push({ start, end: previous });
  return windows;
}

// ChannelMetadata does not currently expose a declared value type. Infer chartable
// numeric channels from the loaded samples, preferring typed values over the
// numeric projection (which can contain placeholder zeros for other types).
function isNumericChartChannel(samples: SampleRangeItem[], channelId: string): boolean {
  let sawNumeric = false;
  for (const sample of samples) {
    const typed = sample.typedChannels?.[channelId];
    if (typed) {
      if (typed.valueType === "Boolean" || typed.valueType === "Categorical") return false;
      if (typed.valueType === "Numeric" && typeof typed.value === "number" && Number.isFinite(typed.value)) {
        sawNumeric = true;
      }
      continue;
    }
    const projected = sample.channels[channelId];
    if (typeof projected === "number" && Number.isFinite(projected)) sawNumeric = true;
  }
  return sawNumeric;
}

const PAGE_SIZE = 5000;
const CHART_TOOLTIP_WIDTH = 340;

type PlottedSeriesMeta = {
  yAxisIndex: number;
  color: string;
  channelName: string;
  unit: string | null;
};

// Axis side: left = solid, right = dashed (always). Additional series on the
// same axis vary width and opacity so overlaps stay visible without breaking
// the solid/dashed axis rule.
function seriesLineStyleForAxis(
  yAxisIndex: number,
  indexOnAxis: number,
  baseWidth: number,
) {
  const tier = Math.min(indexOnAxis, 2);
  const widthScale = [1, 0.82, 0.68][tier];
  const opacity = [1, 0.82, 0.68][tier];

  return {
    type: (yAxisIndex === 1 ? "dashed" : "solid") as "solid" | "dashed",
    width: Math.max(0.75, baseWidth * widthScale),
    opacity,
  };
}

function formatNumericTick(value: number): string {
  const absolute = Math.abs(value);

  if (absolute >= 1_000_000_000) {
    return `${(value / 1_000_000_000).toFixed(1)}G`;
  }

  if (absolute >= 1_000_000) {
    return `${(value / 1_000_000).toFixed(1)}M`;
  }

  if (absolute >= 1_000) {
    return `${(value / 1_000).toFixed(1)}K`;
  }

  return value.toLocaleString();
}

function buildYAxisTitle(entries: PlottedSeriesMeta[]): string {
  if (entries.length === 0) return "";

  if (entries.length === 1) {
    const entry = entries[0];
    return entry.unit
      ? `${entry.channelName} (${entry.unit})`
      : entry.channelName;
  }

  const units = [
    ...new Set(
      entries
        .map((entry) => entry.unit?.trim())
        .filter((unit): unit is string => !!unit),
    ),
  ];

  if (units.length === 1) return units[0];
  if (units.length > 1) return units.join(" · ");
  return "";
}

function buildYAxisStyling(
  entries: PlottedSeriesMeta[],
  neutralColor: string,
  sharedAxisGroupAccent?: string,
) {
  const shared = entries.length > 1;
  const accentColor = shared
    ? (sharedAxisGroupAccent ?? neutralColor)
    : entries[0]?.color ?? neutralColor;

  return {
    name: buildYAxisTitle(entries),
    accentColor,
    axisLineOpacity: shared ? 0.35 : 0.7,
    labelOpacity: shared ? 1 : 1,
  };
}

function formatChannelSeriesLabel(
  channel: ChannelMetadata,
  onRightAxis: boolean,
  includeAxisSide: boolean,
): string {
  const base = channel.unit
    ? `${channel.name} (${channel.unit})`
    : channel.name;

  if (!includeAxisSide) return base;

  const side = onRightAxis ? "Right Y-axis" : "Left Y-axis";
  return `${base} — ${side}`;
}

function getLineWidth(sampleCount: number): number {
  if (sampleCount <= 100) return 2.5;
  if (sampleCount <= 500) return 2;
  if (sampleCount <= 2000) return 1.5;
  return 1;
}

export function RecordingChart({
  sessionId,
  sampleCount,
  channels,
  selectedChannelIds,
  currentSequence,
  currentTimestampNanoseconds,
  timestampOrigin = "Unix",
  onSeek,
  onScrubStart,
  onScrubEnd,
}: RecordingChartProps) {
  const theme = useTheme();

  const chartTheme = useMemo(
    () => createChartTheme(theme.palette.mode),
    [theme.palette.mode],
  );

  const chartRef = useRef<ReactECharts>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const [rightAxisChannelIds, setRightAxisChannelIds] = useState<string[]>([]);
  const [axisMenuAnchor, setAxisMenuAnchor] = useState<HTMLElement | null>(null);
  const rightAxisSet = useMemo(() => new Set(rightAxisChannelIds), [rightAxisChannelIds]);
  const hasRightAxis = selectedChannelIds.some((id) => rightAxisSet.has(id));
  const toggleRightAxis = (id: string) => {
    setRightAxisChannelIds((previous) => previous.includes(id)
      ? previous.filter((value) => value !== id)
      : [...previous, id]);
  };
  const [samples, setSamples] = useState<SampleRangeItem[]>([]);
  // Keep the current page and its immediate neighbors ready for instant navigation.
  // The cache is session-scoped and bounded to avoid retaining entire recordings.
  const pageCacheRef = useRef(new Map<string, SampleRangeItem[]>());
  const pendingPagesRef = useRef(new Map<string, Promise<SampleRangeItem[]>>());
  const cacheSessionRef = useRef(sessionId);
  if (cacheSessionRef.current !== sessionId) {
    cacheSessionRef.current = sessionId;
    pageCacheRef.current.clear();
    pendingPagesRef.current.clear();
  }
  const pageKey = (start: number) => `${sessionId}:${sampleCount}:${start}`;
  const requestPage = (start: number): Promise<SampleRangeItem[]> => {
    const key = pageKey(start);
    const cached = pageCacheRef.current.get(key);
    if (cached) return Promise.resolve(cached);
    const existing = pendingPagesRef.current.get(key);
    if (existing) return existing;
    const promise = recordingApi.getSamples(sessionId, start, Math.min(PAGE_SIZE, sampleCount - start))
      .then((result) => {
        // A previous session's request must never repopulate the current cache.
        if (cacheSessionRef.current === sessionId) {
          pageCacheRef.current.set(key, result.samples);
          // Only retain a small sliding window around the active page.
          if (pageCacheRef.current.size > 5) {
            const oldest = pageCacheRef.current.keys().next().value;
            if (oldest !== undefined) pageCacheRef.current.delete(oldest);
          }
        }
        return result.samples;
      })
      .finally(() => { pendingPagesRef.current.delete(key); });
    pendingPagesRef.current.set(key, promise);
    return promise;
  };
  const [pageIndex, setPageIndex] = useState(0);
  const pageCount = Math.ceil(sampleCount / PAGE_SIZE);
  const activePage = Math.min(pageIndex, Math.max(0, pageCount - 1));
  const pageStart = activePage * PAGE_SIZE;
  const pageEnd = Math.min(sampleCount, pageStart + PAGE_SIZE);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [hover, setHover] = useState<HoverDetails | null>(null);
  const [inspectedSample, setInspectedSample] = useState<HoverDetails | null>(null);
  const [cursorHovered, setCursorHovered] = useState(false);
  const [previewSequence, setPreviewSequence] = useState<number | null>(null);
  // Tag the label position with the page whose ECharts coordinate system
  // produced it. A position from the previous page must never be rendered.
  const [cursorLabelPosition, setCursorLabelPosition] = useState<{
    pageStart: number;
    x: number;
  } | null>(null);
  const cursorLabelX = cursorLabelPosition?.pageStart === pageStart
    ? cursorLabelPosition.x
    : null;
  const setCursorLabelX = (x: number | null) => {
    setCursorLabelPosition(x === null ? null : { pageStart, x });
  };
  const dragRef = useRef<{ sequence: number } | null>(null);
  // Drag visuals are updated imperatively, without a React/ECharts option rebuild
  // on every pointer event.
  const cursorHandleRef = useRef<HTMLDivElement>(null);
  const dragLineRef = useRef<HTMLDivElement>(null);
  const dragLineVisibleRef = useRef(false);
  const dragFrameRef = useRef<number | null>(null);
  const pendingDragXRef = useRef<number | null>(null);

  const renderDragPosition = (x: number, sequence: number) => {
    const handle = cursorHandleRef.current;
    const line = dragLineRef.current;
    if (handle) {
      handle.style.left = `${x}px`;
      handle.textContent = sequence.toLocaleString();
    }
    if (line) line.style.left = `${x}px`;
  };
  const suppressClickRef = useRef(false);
  const [tooltipsEnabled, setTooltipsEnabled] = useState(true);
  const tooltipsEnabledRef = useRef(true);
  const [chartMenu, setChartMenu] = useState<{
    clientX: number;
    clientY: number;
    sequence: number;
    timestamp: string;
    x: number;
    y: number;
  } | null>(null);

  // Only the chart plot opens the menu. Merely opening it never seeks.
  const handlePlotContextMenu = (event: ReactMouseEvent<HTMLDivElement>) => {
    const chart = chartRef.current?.getEchartsInstance();
    if (!chart || chart.isDisposed()) return;
    const bounds = event.currentTarget.getBoundingClientRect();
    const x = event.clientX - bounds.left;
    const y = event.clientY - bounds.top;
    if (!chart.containPixel({ gridIndex: 0 }, [x, y])) return;
    const coordinates = chart.convertFromPixel({ gridIndex: 0 }, [x, y]) as number[];
    if (!Number.isFinite(coordinates?.[0]) || samples.length === 0) return;
    event.preventDefault();
    event.stopPropagation();
    const nearest = samples.reduce((best, item) =>
      Math.abs(item.sequence - coordinates[0]) < Math.abs(best.sequence - coordinates[0])
        ? item : best,
      samples[0],
    );
    setHover(null);
    setInspectedSample(null);
    setChartMenu({
      clientX: event.clientX,
      clientY: event.clientY,
      sequence: nearest.sequence,
      timestamp: formatTimestamp(nearest.timestampNanoseconds, timestampOrigin),
      x,
      y,
    });
  };

  const inspectMenuSample = () => {
    if (!chartMenu) return;
    const sample = samples.find((item) => item.sequence === chartMenu.sequence);
    if (!sample) return;
    const selected = new Set(selectedChannelIds);
    setInspectedSample({
      sequence: sample.sequence,
      timestamp: formatTimestamp(sample.timestampNanoseconds, timestampOrigin),
      x: chartMenu.x,
      y: chartMenu.y,
      values: channels.filter((channel) => selected.has(channel.channelId)).map((channel) => ({
        name: formatChannelSeriesLabel(
          channel,
          rightAxisSet.has(channel.channelId),
          hasRightAxis,
        ),
        value: formatSampleValue(sample, channel.channelId),
      })),
    });
    setChartMenu(null);
  };



  const displayedSample = inspectedSample ?? (tooltipsEnabled ? hover : null);

  const toggleTooltips = () => {
    const next = !tooltipsEnabledRef.current;
    tooltipsEnabledRef.current = next;
    setTooltipsEnabled(next);
    if (!next) setHover(null);
  };
  const handleResetZoom = () => {
    const chart = chartRef.current?.getEchartsInstance();
    if (!chart || chart.isDisposed()) return;

    chart.dispatchAction({
      type: "dataZoom",
      dataZoomIndex: 0,
      start: 0,
      end: 100,
    });
  };

  const listenerCleanupRef = useRef<(() => void) | null>(null);

  // Keep the latest callback and sample range accessible
  // without re-registering chart listeners on each render.
  const onSeekRef = useRef(onSeek);
  const onScrubStartRef = useRef(onScrubStart);
  const onScrubEndRef = useRef(onScrubEnd);
  const samplesRef = useRef(samples);
  const currentSequenceRef = useRef(currentSequence);
  const channelsRef = useRef(channels);
  const selectedIdsRef = useRef(selectedChannelIds);
  const rightAxisSetRef = useRef(rightAxisSet);
  const hasRightAxisRef = useRef(hasRightAxis);

  onSeekRef.current = onSeek;
  onScrubStartRef.current = onScrubStart;
  onScrubEndRef.current = onScrubEnd;
  samplesRef.current = samples;
  currentSequenceRef.current = currentSequence;
  channelsRef.current = channels;
  selectedIdsRef.current = selectedChannelIds;
  rightAxisSetRef.current = rightAxisSet;
  hasRightAxisRef.current = hasRightAxis;

  // Track the last replay position independently of manual chart paging.
  // A page change must never be interpreted as a replay cursor movement.
  // Manual page browsing is independent of the replay cursor.
  const manualPageRef = useRef(false);
  const lastReplayPositionRef = useRef<{
    sessionId: string;
    sequence: number | null;
  } | null>(null);

  useEffect(() => {
    const previous = lastReplayPositionRef.current;
    const sessionChanged = previous?.sessionId !== sessionId;
    const sequenceChanged = previous?.sequence !== currentSequence;

    lastReplayPositionRef.current = { sessionId, sequence: currentSequence };

    if (sessionChanged) {
      manualPageRef.current = false;
      dragRef.current = null;
      setPreviewSequence(null);
      setSamples([]);
      setHover(null);
    } else if (sequenceChanged && !dragRef.current && previewSequence !== null) {
      setPreviewSequence(null);
    }

    // Manual page browsing must remain independent of ordinary replay updates.
    // Resume following only when replay crosses an actual page boundary.
    // This avoids snapping back after clicking Previous/Next while still
    // allowing forward and reverse playback to transition between pages.
    const previousReplayPage = previous?.sequence == null
      ? null
      : Math.floor(previous.sequence / PAGE_SIZE);
    const currentReplayPage = currentSequence == null
      ? null
      : Math.floor(currentSequence / PAGE_SIZE);
    const crossedPageBoundary = !sessionChanged && sequenceChanged &&
      previousReplayPage !== null && currentReplayPage !== null &&
      previousReplayPage !== currentReplayPage;

    if (crossedPageBoundary) manualPageRef.current = false;

    if (dragRef.current || manualPageRef.current) return;

    // Manual Next/Previous page navigation does not change either value.
    if (!sessionChanged && !sequenceChanged) return;

    const targetPage =
      currentSequence === null || sampleCount <= 0
        ? 0
        : Math.floor(
            Math.max(0, Math.min(currentSequence, sampleCount - 1)) /
              PAGE_SIZE,
          );

    setPageIndex((previousPage) =>
      previousPage === targetPage ? previousPage : targetPage,
    );
  }, [sessionId, currentSequence, sampleCount, previewSequence]);

  // Layout effect applies prefetched data before the browser paints a new page.
  // Uncached pages still show the existing loading indicator.
  useLayoutEffect(() => {
    let cancelled = false;
    setHover(null);
    setInspectedSample(null);
    setCursorLabelX(null);

    if (sampleCount <= 0 || pageStart >= sampleCount) {
      setSamples([]);
      setLoading(false);
      setError("");
      return;
    }

    const cached = pageCacheRef.current.get(pageKey(pageStart));
    if (cached) {
      setSamples(cached);
      setLoading(false);
      setError("");
    } else {
      setSamples([]);
      setLoading(true);
      setError("");
      void requestPage(pageStart).then((loaded) => {
        if (!cancelled) {
          setSamples(loaded);
          setLoading(false);
        }
      }).catch((caught) => {
        if (!cancelled) {
          setError(caught instanceof Error ? caught.message : "Unable to load recording samples.");
          setLoading(false);
        }
      });
    }

    // Prefetch both directions. This is a data-only optimization: replay
    // position, breakpoint behavior, and manual page selection are unchanged.
    for (const neighbor of [pageStart - PAGE_SIZE, pageStart + PAGE_SIZE]) {
      if (neighbor >= 0 && neighbor < sampleCount) {
        void requestPage(neighbor).catch(() => { /* Retry on actual navigation. */ });
      }
    }
    return () => { cancelled = true; };
  }, [sessionId, pageStart, sampleCount]);

  useEffect(() => {
    const container = containerRef.current;

    if (!container) return;

    let frameId: number | null = null;

    const observer = new ResizeObserver(() => {
      if (frameId !== null) {
        cancelAnimationFrame(frameId);
      }

      frameId = requestAnimationFrame(() => {
        frameId = null;

        const chart = chartRef.current?.getEchartsInstance();

        if (chart && !chart.isDisposed()) {
          chart.resize();
        }
      });
    });

    observer.observe(container);

    return () => {
      observer.disconnect();

      if (frameId !== null) {
        cancelAnimationFrame(frameId);
      }
    };
  }, []);

  // Browsing another chart page must not seek or invalidate playback.
  // Keep the selected page visible until the user explicitly scrubs/seeks.
  const navigateToPage = (targetPage: number) => {
    if (loading || targetPage < 0 || targetPage >= pageCount || targetPage === activePage) return;
    manualPageRef.current = true;
    // Clear the old page's handle before the new chart finishes loading.
    setCursorLabelX(null);
    setPageIndex(targetPage);
  };

  const lineWidth = getLineWidth(samples.length);

  const option = useMemo(() => {
    const selectedIds = new Set(selectedChannelIds);

    const plotCandidates = channels
      .map((channel, channelIndex) => ({
        channel,
        channelIndex,
      }))
      .filter(({ channel }) =>
        selectedIds.has(channel.channelId) && isNumericChartChannel(samples, channel.channelId),
      );

    const indexOnYAxis = new Map<string, number>();
    let leftAxisSeriesCount = 0;
    let rightAxisSeriesCount = 0;
    for (const { channel } of plotCandidates) {
      const yAxisIndex = rightAxisSet.has(channel.channelId) ? 1 : 0;
      const indexOnAxis =
        yAxisIndex === 1 ? rightAxisSeriesCount++ : leftAxisSeriesCount++;
      indexOnYAxis.set(channel.channelId, indexOnAxis);
    }

    const seriesColorByChannelId = assignChartSeriesColors(
      theme.palette.mode,
      plotCandidates.map(({ channel, channelIndex }) => ({
        channelId: channel.channelId,
        channelName: channel.name,
        channelIndex,
        yAxisIndex: (rightAxisSet.has(channel.channelId) ? 1 : 0) as 0 | 1,
      })),
      hasRightAxis,
    );

    const builtSeries = plotCandidates.map(({ channel, channelIndex }) => {
        const yAxisIndex = rightAxisSet.has(channel.channelId) ? 1 : 0;
        const indexOnAxis = indexOnYAxis.get(channel.channelId) ?? 0;
        const seriesColor =
          seriesColorByChannelId.get(channel.channelId) ??
          getSeriesColor(theme.palette.mode, channelIndex);
        const axisLineStyle = seriesLineStyleForAxis(
          yAxisIndex,
          indexOnAxis,
          lineWidth,
        );
        const legendName = formatChannelSeriesLabel(channel, yAxisIndex === 1, false);

        const config = {
          id: channel.channelId,
          yAxisIndex,
          name: legendName,

          type: "line" as const,
          showSymbol: false,
          connectNulls: false,
          smooth: false,
          silent: false,
          cursor: "default" as const,

          itemStyle: {
            color: seriesColor,
          },

          lineStyle: {
            color: seriesColor,
            width: axisLineStyle.width,
            type: axisLineStyle.type,
            opacity: axisLineStyle.opacity,
          },

          emphasis: {
            focus: "none" as const,
            lineStyle: {
              width: Math.max(2, axisLineStyle.width + 0.75),
              color: seriesColor,
              type: axisLineStyle.type,
              opacity: 1,
            },
            itemStyle: {
              color: seriesColor,
            },
          },

          data: samples.map((sample) => [
            sample.sequence,
            numericPlotValue(sample, channel.channelId),
          ]),
        };

        const meta: PlottedSeriesMeta = {
          yAxisIndex,
          color: seriesColor,
          channelName: channel.name,
          unit: channel.unit,
        };

        return { config, meta };
      });

    const series = builtSeries.map((item) => item.config);
    const seriesMetaByYAxis = (axisIndex: number) =>
      builtSeries
        .filter((item) => item.meta.yAxisIndex === axisIndex)
        .map((item) => item.meta);

    const leftAxisStyle = buildYAxisStyling(
      seriesMetaByYAxis(0),
      chartTheme.axisTextColor,
      hasRightAxis ? getAxisTemperatureAccent(theme.palette.mode, "cool") : undefined,
    );
    const rightAxisStyle = buildYAxisStyling(
      seriesMetaByYAxis(1),
      chartTheme.axisTextColor,
      hasRightAxis ? getAxisTemperatureAccent(theme.palette.mode, "warm") : undefined,
    );

    const leftAxisName = leftAxisStyle.name;
    const rightAxisName = hasRightAxis ? rightAxisStyle.name : "";
    const hasYAxisNames = Boolean(leftAxisName || rightAxisName);

    const makeYAxis = (
      style: ReturnType<typeof buildYAxisStyling>,
      position?: "right",
    ) => ({
      type: "value" as const,
      scale: true,
      position,
      show: position === "right" ? hasRightAxis : true,
      name: style.name,
      // Horizontal titles at the axis top avoid vertical clipping from rotation.
      nameLocation: "end" as const,
      nameGap: 10,
      nameRotate: 0,
      nameTextStyle: {
        color: style.accentColor,
        fontSize: 11,
        fontWeight: 600,
        align: position === "right" ? "right" : "left",
      },
      axisLine: {
        show: true,
        lineStyle: {
          color: style.accentColor,
          opacity: style.axisLineOpacity,
        },
      },
      axisTick: {
        lineStyle: {
          color: style.accentColor,
          opacity: style.axisLineOpacity,
        },
      },
      axisLabel: {
        color: style.accentColor,
        opacity: style.labelOpacity,
        formatter: formatNumericTick,
      },
      splitLine: {
        show: position !== "right",
        lineStyle: {
          color: chartTheme.gridLineColor,
        },
      },
    });

    // The cursor position comes from backend replay state.
    // Attach it to the first visible telemetry series.
    const cursorSequence = previewSequence ?? currentSequence;
    const replayCursor =
      cursorSequence === null ||
      cursorSequence < pageStart ||
      cursorSequence >= pageEnd ||
      series.length === 0
        ? undefined
        : {
            silent: true,
            symbol: "none",
            animation: false,
            label: { show: false },
            lineStyle: {
              color: "#F59E0B",
              width: cursorHovered || previewSequence !== null ? 4 : 2.5,
              type: "solid" as const,
              opacity: 1,
            },
            data: [{ xAxis: cursorSequence }],
          };

    const noDataWindows = findNoDataWindows(samples, series.map((item) => item.id));
    const noDataArea = noDataWindows.length === 0 ? undefined : {
      silent: true,
      animation: false,
      itemStyle: {
        color: theme.palette.mode === "dark" ? "rgba(148,163,184,0.10)" : "rgba(100,116,139,0.09)",
      },
      label: {
        show: true,
        position: "insideTop" as const,
        color: chartTheme.axisTextColor,
        fontSize: 10,
        formatter: "No Data",
      },
      data: noDataWindows.map(({ start, end }) => [
        { xAxis: Math.max(pageStart, start - 0.5) },
        { xAxis: Math.min(pageEnd - 1, end + 0.5) },
      ]),
    };

    const chartSeries = series.map((item, index) =>
      index === 0
        ? { ...item, ...(replayCursor ? { markLine: replayCursor } : {}),
            ...(noDataArea ? { markArea: noDataArea } : {}) }
        : item,
    );

    return {
      animation: false,

      backgroundColor: chartTheme.backgroundColor,
      color: chartTheme.color,
      textStyle: chartTheme.textStyle,

      tooltip: {
        show: false,
        trigger: "none",
      },

      legend: {
        show: true,
        data: series.map((item) => ({
          name: item.name,
          itemStyle: {
            color: item.lineStyle.color,
            borderColor: item.lineStyle.color,
          },
          lineStyle: {
            color: item.lineStyle.color,
            type: item.lineStyle.type,
            width: 2.5,
          },
        })),
        ...(hasRightAxis
          ? {
              tooltip: {
                show: true,
                confine: true,
                formatter: (param: { name?: string }) => {
                  const label = param.name ?? "";
                  const match = series.find((item) => item.name === label);
                  if (!match) return label;
                  const side =
                    match.yAxisIndex === 1 ? "Right Y-axis" : "Left Y-axis";
                  return `${label}<br/>${side}`;
                },
              },
            }
          : {}),
        type: "scroll" as const,
        orient: "horizontal" as const,

        top: 4,
        left: "center",
        right: 120,

        itemWidth: 32,
        itemHeight: 3,
        itemGap: 10,

        textStyle: {
          color: chartTheme.axisTextColor,
          fontSize: 11,
        },

        pageTextStyle: {
          color: chartTheme.axisTextColor,
        },

        selectedMode: false,
      },

      grid: {
        left: 8,
        right: hasRightAxis ? 12 : 8,
        top: hasYAxisNames ? 52 : 42,
        bottom: 40,
        containLabel: true,
      },

      xAxis: {
        type: "value" as const,
        name: "Sequence",

        min: pageStart,
        max: Math.max(pageStart + 1, pageEnd - 1),

        nameLocation: "middle" as const,
        nameGap: 22,
        minInterval: 1,

        nameTextStyle: {
          color: chartTheme.axisTextColor,
        },

        axisLine: {
          lineStyle: {
            color: chartTheme.axisTextColor,
          },
        },

        axisTick: {
          lineStyle: {
            color: chartTheme.axisTextColor,
          },
        },

        axisLabel: {
          color: chartTheme.axisTextColor,
          hideOverlap: true,
          formatter: (value: number) =>
            Math.round(value).toLocaleString(),
        },

        splitLine: {
          show: true,
          lineStyle: {
            color: chartTheme.gridLineColor,
          },
        },

        axisPointer: {
          show: true,
          snap: false,
          lineStyle: {
            color: chartTheme.cursorColor,
            type: "dashed" as const,
          },
        },
      },

      yAxis: [
        makeYAxis(leftAxisStyle),
        makeYAxis(rightAxisStyle, "right"),
      ],

      dataZoom: [
        {
          type: "inside" as const,
          xAxisIndex: 0,
          zoomOnMouseWheel: true,
          // Reserve left-button dragging for replay scrubbing, not ECharts panning.
          moveOnMouseMove: false,
          moveOnMouseWheel: false,
        },
      ],

      series: chartSeries,
    };
  }, [
      channels,
      samples,
      selectedChannelIds,
      rightAxisSet,
      hasRightAxis,
      chartTheme,
      currentSequence,
      cursorHovered,
      previewSequence,
      lineWidth,
      pageStart,
      pageEnd,
      theme.palette.mode,
  ]);

  // Follow replay within a zoomed viewport without changing the zoom width.
  // Only a replay-position change can move the viewport: manual zoom/pan stays
  // untouched while paused. Chart pages retain their existing navigation rules.
  const lastChartReplayRef = useRef<{ sessionId: string; pageStart: number; sequence: number | null } | null>(null);

  // Keep the HTML handle aligned with the actual visible plot, not the canvas.
  // ECharts can convert off-screen data coordinates into pixels outside the grid.
  useEffect(() => {
    const chart = chartRef.current?.getEchartsInstance();
    if (!chart || chart.isDisposed()) return;

    let disposed = false;
    let frame: number | null = null;
    const previous = lastChartReplayRef.current;
    const replayMoved = previous?.sessionId === sessionId &&
      previous.pageStart === pageStart && previous.sequence !== currentSequence;
    lastChartReplayRef.current = { sessionId, pageStart, sequence: currentSequence };
    let followPending = replayMoved && previewSequence === null;

    const update = () => {
      if (disposed || chart.isDisposed() || dragRef.current) return;
      const sequence = previewSequence ?? currentSequence;
      if (sequence === null || sequence < pageStart || sequence >= pageEnd ||
          samples.length === 0 || samples[0].sequence < pageStart ||
          samples[0].sequence >= pageEnd) {
        setCursorLabelX(null);
        return;
      }
      try {
        // During page transitions, ECharts may still expose the previous axes.
        const axis = (chart.getOption().xAxis as Array<{ min?: number; max?: number }> | undefined)?.[0];
        if (Number(axis?.min) !== pageStart ||
            Number(axis?.max) !== Math.max(pageStart + 1, pageEnd - 1)) {
          setCursorLabelX(null);
          return;
        }

        const zoom = (chart.getOption().dataZoom as Array<{
          start?: number; end?: number;
        }> | undefined)?.[0];
        const startPercent = Math.max(0, Math.min(100, Number(zoom?.start ?? 0)));
        const endPercent = Math.max(startPercent, Math.min(100, Number(zoom?.end ?? 100)));
        const fullMin = pageStart;
        const fullMax = Math.max(pageStart + 1, pageEnd - 1);
        const fullWidth = fullMax - fullMin;
        const visibleMin = fullMin + fullWidth * startPercent / 100;
        const visibleMax = fullMin + fullWidth * endPercent / 100;
        const visibleWidth = visibleMax - visibleMin;

        if (followPending) {
          followPending = false;
          // A forward cursor leaving the right edge returns near 80% of the
          // viewport; reverse playback follows from the left at 20%.
          // Preserve the exact zoom width, including near page boundaries.
          if (visibleWidth > 0 && visibleWidth < fullWidth - 0.001 &&
              (sequence < visibleMin || sequence > visibleMax)) {
            const forward = previous?.sequence == null || sequence >= previous.sequence;
            const anchor = forward ? 0.8 : 0.2;
            const shiftedMin = Math.max(fullMin, Math.min(fullMax - visibleWidth,
              sequence - visibleWidth * anchor));
            const newStart = (shiftedMin - fullMin) / fullWidth * 100;
            const newEnd = (shiftedMin + visibleWidth - fullMin) / fullWidth * 100;
            chart.dispatchAction({
              type: "dataZoom", dataZoomIndex: 0,
              start: newStart, end: newEnd,
            });
            // datazoom/finished will update the handle after layout.
            return;
          }
        }

        // Never render the cursor outside the zoomed plot. In particular,
        // checking pixel <= chart.getWidth() is insufficient here.
        if (sequence < visibleMin - 0.001 || sequence > visibleMax + 0.001) {
          setCursorLabelX(null);
          return;
        }
        const pixel = chart.convertToPixel({ xAxisIndex: 0 }, sequence) as number;
        const leftPixel = chart.convertToPixel({ xAxisIndex: 0 }, visibleMin) as number;
        const rightPixel = chart.convertToPixel({ xAxisIndex: 0 }, visibleMax) as number;
        if (!Number.isFinite(pixel) || !Number.isFinite(leftPixel) ||
            !Number.isFinite(rightPixel) ||
            pixel < Math.min(leftPixel, rightPixel) - 1 ||
            pixel > Math.max(leftPixel, rightPixel) + 1) {
          setCursorLabelX(null);
          return;
        }
        // Keep the draggable label fully inside the chart at either edge.
        const labelX = Math.max(30, Math.min(chart.getWidth() - 30, pixel));
        if (cursorHandleRef.current) cursorHandleRef.current.style.left = `${labelX}px`;
        setCursorLabelX(labelX);
      } catch {
        setCursorLabelX(null);
      }
    };
    const scheduleUpdate = () => {
      if (disposed || frame !== null) return;
      frame = requestAnimationFrame(() => {
        frame = null;
        update();
      });
    };
    scheduleUpdate();
    chart.on("finished", scheduleUpdate);
    chart.on("datazoom", scheduleUpdate);
    const observer = new ResizeObserver(scheduleUpdate);
    observer.observe(chart.getDom());
    return () => {
      disposed = true;
      if (frame !== null) cancelAnimationFrame(frame);
      chart.off("finished", scheduleUpdate);
      chart.off("datazoom", scheduleUpdate);
      observer.disconnect();
    };
  }, [sessionId, currentSequence, previewSequence, pageStart, pageEnd, samples, option]);

  const handleLabelPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0 || dragRef.current) return;
    const chart = chartRef.current?.getEchartsInstance();
    const sequence = previewSequence ?? currentSequence;
    if (!chart || chart.isDisposed() || sequence === null) return;
    event.preventDefault();
    event.stopPropagation();
    event.currentTarget.setPointerCapture(event.pointerId);
    dragRef.current = { sequence };
    suppressClickRef.current = true;
    setHover(null);
    setInspectedSample(null);

    // Hide the authoritative ECharts markLine once while dragging. A lightweight
    // DOM line takes its place; ECharts is not updated again until release.
    const firstSeriesId = (option.series[0] as { id?: string } | undefined)?.id;
    if (firstSeriesId) {
      chart.setOption({ series: [{ id: firstSeriesId, markLine: { data: [] } }] });
    }
    const line = dragLineRef.current;
    if (line) {
      line.style.display = "block";
      dragLineVisibleRef.current = true;
    }
    onScrubStartRef.current();
  };

  const handleLabelPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    if (!dragRef.current) return;
    const chart = chartRef.current?.getEchartsInstance();
    const loaded = samplesRef.current;
    if (!chart || chart.isDisposed() || !loaded.length) return;
    const rect = chart.getDom().getBoundingClientRect();
    const x = event.clientX - rect.left;
    let rawSequence: number;
    try {
      rawSequence = chart.convertFromPixel({ xAxisIndex: 0 }, x) as number;
    } catch {
      return;
    }
    if (!Number.isFinite(rawSequence)) return;
    const sequence = Math.max(loaded[0].sequence,
      Math.min(loaded[loaded.length - 1].sequence, Math.round(rawSequence)));
    dragRef.current.sequence = sequence;

    // Pixel position follows the pointer, clamped to the loaded sample range.
    // Snap only the recorded sequence; commit the seek on pointer release.
    let snappedX: number;
    try {
      snappedX = chart.convertToPixel({ xAxisIndex: 0 }, sequence) as number;
    } catch {
      return;
    }
    if (!Number.isFinite(snappedX)) return;
    pendingDragXRef.current = snappedX;
    if (dragFrameRef.current === null) {
      dragFrameRef.current = requestAnimationFrame(() => {
        dragFrameRef.current = null;
        if (dragRef.current && pendingDragXRef.current !== null) {
          renderDragPosition(pendingDragXRef.current, dragRef.current.sequence);
        }
      });
    }
  };

  const finishLabelDrag = (event: PointerEvent<HTMLDivElement>, cancelled: boolean) => {
    if (!dragRef.current) return;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    if (dragFrameRef.current !== null) cancelAnimationFrame(dragFrameRef.current);
    dragFrameRef.current = null;
    pendingDragXRef.current = null;
    const sequence = cancelled ? currentSequence : dragRef.current.sequence;
    dragRef.current = null;
    dragLineVisibleRef.current = false;
    if (dragLineRef.current) dragLineRef.current.style.display = "none";
    // React now performs one authoritative chart update, rather than one per move.
    setPreviewSequence(sequence === currentSequence ? null : sequence);
    manualPageRef.current = false;
    suppressClickRef.current = false;
    // Restore the visible markLine immediately, including a no-op drag.
    const restoredChart = chartRef.current?.getEchartsInstance();
    const restoredSeriesId = (option.series[0] as { id?: string } | undefined)?.id;
    if (restoredChart && !restoredChart.isDisposed() && restoredSeriesId && sequence !== null) {
      restoredChart.setOption({ series: [{ id: restoredSeriesId, markLine: {
        data: [{ xAxis: sequence }], silent: true, symbol: "none",
        animation: false, label: { show: false },
        lineStyle: { color: "#F59E0B", width: 2.5, type: "solid" },
      } }] });
    }
    if (cancelled) {
      // Restore the original cursor after cancellation without seeking.
      const chart = chartRef.current?.getEchartsInstance();
      const firstSeriesId = (option.series[0] as { id?: string } | undefined)?.id;
      if (chart && !chart.isDisposed() && firstSeriesId && currentSequence !== null) {
        chart.setOption({ series: [{ id: firstSeriesId, markLine: {
          data: [{ xAxis: currentSequence }],
          silent: true, symbol: "none", animation: false,
          label: { show: false },
          lineStyle: { color: "#F59E0B", width: 2.5, type: "solid" },
        } }] });
      }
      if (cursorHandleRef.current && cursorLabelX !== null) {
        renderDragPosition(cursorLabelX, currentSequence ?? 0);
      }
      return;
    }
    if (sequence !== null) onScrubEndRef.current(sequence);
  };

  const handleLabelPointerUp = (event: PointerEvent<HTMLDivElement>) =>
    finishLabelDrag(event, false);
  const handleLabelPointerCancel = (event: PointerEvent<HTMLDivElement>) =>
    finishLabelDrag(event, true);

  useEffect(() => () => {
    if (dragFrameRef.current !== null) cancelAnimationFrame(dragFrameRef.current);
  }, []);

  // Detach our ZRender listeners when the chart unmounts.
  useEffect(() => () => listenerCleanupRef.current?.(), []);

  // Register click-to-seek when ECharts is initialized.
  // This also handles clicks on empty areas of the plot.
  const handleChartReady = useCallback(
    (chart: EChartsType) => {
      console.debug("Replay chart ready");

      chart.on("finished", () => {
        const series = chart.getOption().series as Array<{
          id?: string;
          markLine?: {
            data?: Array<{ xAxis?: number }>;
          };
        }>;

        console.debug(
          "[MetricBreak Chart] Active cursor markers",
          series.flatMap(s =>
            (s.markLine?.data ?? []).map(marker => ({
              series: s.id,
              sequence: marker.xAxis
            }))
          )
        );
      });

      const renderer = chart.getZr();
             const sequenceAt = (x: number, y: number): number | null => {
         const pixel: [number, number] = [x, y];
         if (!chart.containPixel({ gridIndex: 0 }, pixel)) return null;
         const coords = chart.convertFromPixel({ gridIndex: 0 }, pixel) as number[];
         const loaded = samplesRef.current;
         if (!loaded.length || !Number.isFinite(coords[0])) return null;
         // Sequence is authoritative even when timestamps are identical.
         return Math.max(loaded[0].sequence,
           Math.min(loaded[loaded.length - 1].sequence, Math.round(coords[0])));
       };

       const handleClick = (event: { offsetX: number; offsetY: number }) => {
         setInspectedSample(null);
         if (suppressClickRef.current) {
           suppressClickRef.current = false;
           return;
         }
         const sequence = sequenceAt(event.offsetX, event.offsetY);
         if (sequence !== null && sequence !== currentSequenceRef.current) {
           onSeekRef.current(sequence);
         }
       };

const handleMove = (event: { offsetX: number; offsetY: number }) => {
        if (!tooltipsEnabledRef.current) return;
        const pixel: [number, number] = [event.offsetX, event.offsetY];
        if (!chart.containPixel({ gridIndex: 0 }, pixel)) {
          setHover(null);
          return;
        }
        const coordinates = chart.convertFromPixel(
          { gridIndex: 0 }, pixel,
        ) as number[];
        const rawSequence = coordinates[0];
        const loaded = samplesRef.current;
        if (!Number.isFinite(rawSequence) || loaded.length === 0) {
          setHover(null);
          return;
        }

        // Find the nearest loaded sample, without assuming contiguous sequences.
        let closest = loaded[0];
        let distance = Math.abs(closest.sequence - rawSequence);
        for (let i = 1; i < loaded.length; i++) {
          const candidate = loaded[i];
          const nextDistance = Math.abs(candidate.sequence - rawSequence);
          if (nextDistance < distance) {
            closest = candidate;
            distance = nextDistance;
          }
        }
        const selected = new Set(selectedIdsRef.current);
        const values = channelsRef.current
          .filter((channel) => selected.has(channel.channelId))
          .map((channel) => ({
            name: formatChannelSeriesLabel(
              channel,
              rightAxisSetRef.current.has(channel.channelId),
              hasRightAxisRef.current,
            ),
            value: formatSampleValue(closest, channel.channelId),
          }));
        setHover({
          sequence: closest.sequence,
          timestamp: formatTimestamp(closest.timestampNanoseconds, timestampOrigin),
          x: event.offsetX,
          y: event.offsetY,
          values,
        });
      };

      const handleLeave = () => {
        setHover(null);
        setCursorHovered(false);
      };

      listenerCleanupRef.current?.();
      renderer.on("click", handleClick);
      renderer.on("mousemove", handleMove);
      renderer.on("globalout", handleLeave);
      listenerCleanupRef.current = () => {
        renderer.off("click", handleClick);
        renderer.off("mousemove", handleMove);
        renderer.off("globalout", handleLeave);
      };

      // Remove only our listener when this chart is
      // disposed. Avoid renderer.off("click"), which
      // would remove other registered listeners.
      chart.on("finished", () => {
        // Intentionally no option updates here.
      });
    },
    [],
  );

  console.debug("CHART DIAGNOSTICS", {
    loadedSamples: samples.length,
    selectedChannels: selectedChannelIds,
    currentSequence,
    seriesCount: option.series.length,
    chartWidth: containerRef.current?.clientWidth,
    chartHeight: containerRef.current?.clientHeight,
  });

  return (
    <Box
      ref={containerRef}
      sx={{
        display: "flex",
        flexDirection: "column",
        width: "100%",
        height: "100%",
        minWidth: 0,
        minHeight: 0,
        overflow: "hidden",
        position: "relative",
      }}
    >
      {error && (
        <Alert
          severity="error"
          sx={{
            flexShrink: 0,
            mb: 1,
          }}
        >
          {error}
        </Alert>
      )}

      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 0.5,
          px: 0.5,
          py: 0.25,
          flexShrink: 0,
          flexWrap: "nowrap",
          overflowX: "auto",
        }}
      >
        {sampleCount > 0 && (
          <Typography
            variant="body2"
            noWrap
            sx={{
              fontWeight: 400,
              color: "warning.main",
              fontVariantNumeric: "tabular-nums",
              flexShrink: 0,
              mr: "auto",
            }}
            title="Actual replay position (independent of chart page)"
          >
            Current sample: {currentSequence?.toLocaleString() ?? "—"}
            {currentTimestampNanoseconds != null && (() => {
              if (timestampOrigin === "Elapsed") {
                return ` · ${formatElapsed(String(currentTimestampNanoseconds))}`;
              }
              try {
                const ms = Number(BigInt(currentTimestampNanoseconds) / 1_000_000n);
                const date = new Date(ms);
                return Number.isNaN(date.getTime()) ? "" : ` · ${date.toISOString().slice(11, 23)} UTC`;
              } catch { return ""; }
            })()}
          </Typography>
        )}
        <Button
          size="small"
          variant="outlined"
          onClick={(event) => setAxisMenuAnchor(event.currentTarget)}
          disabled={selectedChannelIds.length === 0}
          aria-label="Configure Y axes"
          sx={{ bgcolor: "background.paper", flexShrink: 0, whiteSpace: "nowrap" }}
        >
          Y Axes{hasRightAxis ? ": 2" : ": 1"}
        </Button>
        <Menu
          anchorEl={axisMenuAnchor}
          open={Boolean(axisMenuAnchor)}
          onClose={() => setAxisMenuAnchor(null)}
          slotProps={{ list: { 'aria-label': 'Channels plotted on right Y axis' } }}
        >
          <MenuItem disabled sx={{ opacity: 1, fontSize: "0.75rem" }}>
            Checked → right Y-axis (dashed). Unchecked → left (solid).
          </MenuItem>
          {channels.filter((channel) => selectedChannelIds.includes(channel.channelId) && isNumericChartChannel(samples, channel.channelId)).map((channel) => (
            <MenuItem key={channel.channelId} onClick={() => toggleRightAxis(channel.channelId)}>
              <Checkbox size="small" checked={rightAxisSet.has(channel.channelId)} tabIndex={-1} disableRipple />
              <ListItemText
                primary={channel.name}
                secondary={
                  rightAxisSet.has(channel.channelId)
                    ? `Right axis${channel.unit ? ` · ${channel.unit}` : ""}`
                    : `Left axis${channel.unit ? ` · ${channel.unit}` : ""}`
                }
              />
            </MenuItem>
          ))}
        </Menu>
        <Button
          size="small"
          variant="outlined"
          onClick={handleResetZoom}
          disabled={loading || samples.length === 0}
          sx={{ bgcolor: "background.paper", flexShrink: 0, whiteSpace: "nowrap" }}
        >
          Reset Zoom (100%)
        </Button>
        <Button
          size="small"
          variant="outlined"
          color={tooltipsEnabled ? "primary" : "inherit"}
          aria-pressed={tooltipsEnabled}
          onClick={toggleTooltips}
          sx={{ minWidth: 112, bgcolor: "background.paper", flexShrink: 0, whiteSpace: "nowrap" }}
        >
          Tooltips: {tooltipsEnabled ? "On" : "Off"}
        </Button>
      </Box>

      {hasRightAxis && sampleCount > 0 && (
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{
            flexShrink: 0,
            px: 0.5,
            fontSize: "0.65rem",
            letterSpacing: 0.2,
            opacity: 0.85,
          }}
        >
          Left Y-axis: solid · Right Y-axis: dashed
        </Typography>
      )}

      <Box
        sx={{
          flex: 1,
          minHeight: 0,
          minWidth: 0,
          position: "relative",
        }}
      >
        {loading ? (
          <Box
            sx={{
              height: "100%",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            <CircularProgress />
          </Box>
        ) : samples.length > 0 ? (
          <>
          <Box
            sx={{
              height: "100%",
              width: "100%",
              "& canvas": {
                cursor: "default !important",
              },
            }}
            onContextMenu={handlePlotContextMenu}
          >
          <ReactECharts
            key={`${sessionId}:${pageStart}`}
            ref={chartRef}
            option={option}
            notMerge={false}
            replaceMerge={["series"]}
            onChartReady={handleChartReady}
            style={{
              height: "100%",
              width: "100%",
              cursor: "default",
            }}
          />
          <Box
            ref={dragLineRef}
            aria-hidden="true"
            sx={{
              display: "none",
              position: "absolute",
              top: 42,
              bottom: 48,
              width: "4px",
              transform: "translateX(-50%)",
              backgroundColor: "#F59E0B",
              pointerEvents: "none",
              zIndex: 11,
            }}
          />
          {cursorLabelX !== null &&
            (previewSequence ?? currentSequence) !== null &&
            (previewSequence ?? currentSequence)! >= pageStart &&
            (previewSequence ?? currentSequence)! < pageEnd && (
            <Box
              ref={cursorHandleRef}
              onPointerDown={handleLabelPointerDown}
              onPointerMove={handleLabelPointerMove}
              onPointerUp={handleLabelPointerUp}
              onPointerCancel={handleLabelPointerCancel}
              sx={{
                position: "absolute",
                // Place the handle between the legend and the plot (grid starts at 42px).
                top: 22,
                left: cursorLabelX,
                transform: "translateX(-50%)",
                zIndex: 12,
                px: 1,
                py: 0.25,
                borderRadius: 1,
                bgcolor: "#F59E0B",
                color: "#17202B",
                fontSize: 11,
                fontWeight: 700,
                cursor: dragRef.current ? "grabbing" : "grab",
                userSelect: "none",
                touchAction: "none",
                whiteSpace: "nowrap",
              }}
            >
              {(previewSequence ?? currentSequence)?.toLocaleString()}
            </Box>
          )}
          </Box>
          {displayedSample && (
            <Box
              tabIndex={inspectedSample ? 0 : undefined}
              role={inspectedSample ? "region" : undefined}
              aria-label={inspectedSample ? "Inspect sample values" : undefined}
              onWheel={inspectedSample ? (event) => event.stopPropagation() : undefined}
              onClick={inspectedSample ? (event) => event.stopPropagation() : undefined}
              onMouseDown={inspectedSample ? (event) => event.stopPropagation() : undefined}
              sx={{
                position: "absolute",
                zIndex: 10,
                // Hover previews pass through to the chart; explicitly inspected
                // samples must receive clicks, focus and wheel scrolling.
                pointerEvents: inspectedSample ? "auto" : "none",
                overscrollBehavior: "contain",
                scrollbarGutter: "stable",
                left: Math.max(
                  8,
                  Math.min(
                    displayedSample.x + 14,
                    (containerRef.current?.clientWidth ?? 450) -
                      (CHART_TOOLTIP_WIDTH + 16),
                  ),
                ),
                top: Math.max(8, Math.min(displayedSample.y + 14, (containerRef.current?.clientHeight ?? 400) - 180)),
                width: CHART_TOOLTIP_WIDTH,
                maxWidth: "min(92%, 420px)",
                maxHeight: "70%",
                overflowY: "auto",
                p: 1.5,
                border: 1,
                borderColor: "divider",
                borderRadius: 1,
                bgcolor: "background.paper",
                boxShadow: 4,
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1 }}>
                <Typography variant="caption" sx={{ fontWeight: 700 }}>
                  Sequence {displayedSample.sequence.toLocaleString()}
                </Typography>
                {inspectedSample && (
                  <Button size="small" sx={{ minWidth: 0, p: 0.25, pointerEvents: "auto" }} onClick={() => setInspectedSample(null)} aria-label="Close sample inspection">
                    Close
                  </Button>
                )}
              </Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
                {displayedSample.timestamp}
              </Typography>
              {displayedSample.values.map((entry) => (
                <Box
                  key={entry.name}
                  sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    gap: 2,
                    alignItems: "flex-start",
                  }}
                >
                  <Typography
                    variant="caption"
                    sx={{ flex: 1, minWidth: 0, overflowWrap: "anywhere" }}
                  >
                    {entry.name}
                  </Typography>
                  <Typography variant="caption" sx={{ fontWeight: 600, flexShrink: 0 }}>
                    {entry.value}
                  </Typography>
                </Box>
              ))}
              {inspectedSample && (
                <Button
                  size="small"
                  variant="outlined"
                  fullWidth
                  sx={{ mt: 1.5 }}
                  onClick={() => {
                    const sequence = inspectedSample.sequence;
                    setInspectedSample(null);
                    manualPageRef.current = false;
                    onSeekRef.current(sequence);
                  }}
                >
                  Move Replay Cursor Here
                </Button>
              )}
            </Box>
          )}
          </>
        ) : (
          <Box
            sx={{
              height: "100%",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            <Typography color="text.secondary">
              No samples available for visualization.
            </Typography>
          </Box>
        )}
      </Box>

      {sampleCount > 0 && (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 0.5,
            px: 0.5,
            py: 0.25,
            flexShrink: 0,
            borderTop: 1,
            borderColor: "divider",
          }}
        >
          <Typography variant="caption" color="text.secondary" noWrap>
            Samples {(pageStart + 1).toLocaleString()}–{pageEnd.toLocaleString()} of {sampleCount.toLocaleString()}
            {pageCount > 1 ? ` · Page ${activePage + 1} of ${pageCount}` : ""}
          </Typography>
          {pageCount > 1 && (
            <ButtonGroup size="small" variant="outlined" aria-label="Chart page navigation" sx={{ flexShrink: 0 }}>
              <Button disabled={loading || activePage === 0} onClick={() => navigateToPage(activePage - 1)}>
                Previous
              </Button>
              <Button
                disabled={loading || activePage >= pageCount - 1}
                onClick={() => navigateToPage(activePage + 1)}
              >
                Next
              </Button>
            </ButtonGroup>
          )}
        </Box>
      )}

      <Menu
        open={chartMenu !== null}
        onClose={() => setChartMenu(null)}
        anchorReference="anchorPosition"
        anchorPosition={chartMenu ? { top: chartMenu.clientY, left: chartMenu.clientX } : undefined}
        slotProps={{ list: { 'aria-label': 'Chart sample actions' } }}
      >
        <MenuItem disabled>
          <ListItemText primary={`Sample ${chartMenu?.sequence.toLocaleString() ?? ''}`} secondary={chartMenu?.timestamp ?? ''} />
        </MenuItem>
        <Divider />
        <MenuItem onClick={() => {
          if (chartMenu) {
            manualPageRef.current = false;
            onSeekRef.current(chartMenu.sequence);
          }
          setChartMenu(null);
        }}>Move replay cursor here</MenuItem>
        <MenuItem onClick={inspectMenuSample}>Inspect sample values</MenuItem>
        <Divider />
        <MenuItem onClick={() => {
          if (chartMenu) void navigator.clipboard.writeText(String(chartMenu.sequence));
          setChartMenu(null);
        }}>Copy sequence number</MenuItem>
        <MenuItem onClick={() => {
          if (chartMenu) void navigator.clipboard.writeText(chartMenu.timestamp);
          setChartMenu(null);
        }}>Copy timestamp</MenuItem>
      </Menu>
    </Box>
  );
}

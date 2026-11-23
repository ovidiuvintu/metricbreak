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
import ZoomInOutlinedIcon from "@mui/icons-material/ZoomInOutlined";
import ZoomOutOutlinedIcon from "@mui/icons-material/ZoomOutOutlined";
import CenterFocusStrongOutlinedIcon from "@mui/icons-material/CenterFocusStrongOutlined";

import { recordingApi } from "./recordingApi";
import {
  applyViewportPercents,
  readChartPlotFrame,
  readSequenceViewport,
  type ChartPlotFrame,
  resetViewport,
  sequenceInZoomedViewport,
  sequencesToViewportPercents,
  zoomViewportAtAnchor,
  zoomViewportByFactor,
  zoomViewportToSequence,
} from "./chartDataZoom";
import type {
  Breakpoint,
  CompositeBreakpoint,
} from "./breakpointApi";
import { BreakpointHitTimeline } from "./BreakpointHitTimeline";
import type { BreakpointDialogDraft } from "./BreakpointDialog";
import type { ChannelMetadata } from "./channelMetadata";
import type { SampleRangeItem } from "./sampleTypes";
import {
  AXIS_RANGE_MIN_SCALE,
  collectPlottedSeriesRange,
  isNumericChartChannel,
  numericPlotValue,
  axisHasDivergentRanges,
  resolveEffectiveRightAxisIds,
  suggestRightAxisChannelIds,
  type AxisPins,
  type PlottedSeriesRange,
} from "./chartAxisAssignment";
import {
  assignChartSeriesColors,
  createChartTheme,
  getAxisTemperatureAccent,
  getSeriesColor,
} from "../../theme/chartTheme";
import type {
  InvestigationBookmark,
  InvestigationUiState,
} from "../investigations/investigationTypes";
import {
  buildTimelineEventScatterSeries,
  pickTimelineEventHit,
  resolveActiveStage,
  TIMELINE_EVENTS_SERIES_ID,
  timelineEventMarkerY,
  timelineEventsOnPage,
  type ChartTimelineEntry,
  type TimelineEventHit,
} from "./chartTimelineMarks";

export type ChartViewportSnapshot = NonNullable<
  InvestigationUiState["chartViewport"]
>;

export type ChartViewportReader = {
  read: () => ChartViewportSnapshot | null;
};

type ChartMarkLine = {
  silent?: boolean;
  symbol?: string;
  animation?: boolean;
  label?: { show: boolean };
  data: Array<Record<string, unknown>>;
};

function nearestLoadedSampleSequence(
  loaded: SampleRangeItem[],
  rawSequence: number,
): number | null {
  if (!loaded.length || !Number.isFinite(rawSequence)) return null;

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
  return closest.sequence;
}

function buildFirstSeriesMarkLine(params: {
  cursorSequence: number | null;
  pageStart: number;
  pageEnd: number;
  seriesLength: number;
  cursorHovered: boolean;
  previewSequence: number | null;
  paletteMode: "light" | "dark";
  bookmarks: InvestigationBookmark[];
  visibleMin?: number;
  visibleMax?: number;
}): ChartMarkLine | undefined {
  const bookmarkColor = params.paletteMode === "dark" ? "#C792EA" : "#6D28D9";
  const data: Array<Record<string, unknown>> = [];
  const zoomClips =
    params.visibleMin !== undefined && params.visibleMax !== undefined;

  const cursorOnPage =
    params.cursorSequence !== null &&
    params.cursorSequence >= params.pageStart &&
    params.cursorSequence < params.pageEnd;

  const cursorInView =
    cursorOnPage &&
    (!zoomClips ||
      sequenceInZoomedViewport(
        params.cursorSequence!,
        params.visibleMin!,
        params.visibleMax!,
      ));

  if (cursorInView && params.seriesLength > 0) {
    data.push({
      xAxis: params.cursorSequence,
      lineStyle: {
        color: "#F59E0B",
        width: params.cursorHovered || params.previewSequence !== null ? 4 : 2.5,
        type: "solid",
        opacity: 1,
      },
    });
  }

  for (const bookmark of params.bookmarks) {
    if (
      bookmark.sampleSequence < params.pageStart ||
      bookmark.sampleSequence >= params.pageEnd
    ) {
      continue;
    }
    if (
      zoomClips &&
      !sequenceInZoomedViewport(
        bookmark.sampleSequence,
        params.visibleMin!,
        params.visibleMax!,
      )
    ) {
      continue;
    }

    const label =
      bookmark.title?.trim() ||
      `Sample ${bookmark.sampleSequence.toLocaleString()}`;

    data.push({
      xAxis: bookmark.sampleSequence,
      name: bookmark.bookmarkId,
      bookmarkSequence: bookmark.sampleSequence,
      silent: false,
      lineStyle: {
        color: bookmarkColor,
        width: 1.5,
        type: "dashed",
        opacity: 0.85,
      },
      label: { show: false },
      emphasis: {
        label: {
          show: true,
          formatter: label,
          color: bookmarkColor,
          fontSize: 10,
        },
        lineStyle: {
          width: 2.5,
          opacity: 1,
        },
      },
    });
  }

  if (data.length === 0) {
    return undefined;
  }

  return {
    silent: true,
    symbol: "none",
    animation: false,
    label: { show: false },
    data,
  };
}

function markLineViewportFromChart(
  chart: EChartsType | undefined,
  pageStart: number,
  pageEnd: number,
): Pick<
  Parameters<typeof buildFirstSeriesMarkLine>[0],
  "visibleMin" | "visibleMax"
> {
  if (!chart || chart.isDisposed()) return {};
  try {
    const viewport = readSequenceViewport(chart, pageStart, pageEnd);
    if (!viewport) return {};
    return { visibleMin: viewport.visibleMin, visibleMax: viewport.visibleMax };
  } catch {
    return {};
  }
}

interface RecordingChartProps {
  sessionId: string;
  sampleCount: number;
  channels: ChannelMetadata[];
  selectedChannelIds: string[];
  axisPins: AxisPins;
  onAxisPinChange: (channelId: string, pin: "left" | "right" | "auto") => void;
  onClearAxisPins?: () => void;
  onEffectiveRightAxisChange?: (rightAxisChannelIds: string[]) => void;
  currentSequence: number | null;
  /** Replay marker. Hidden until the user starts playback on this file. */
  showCursor?: boolean;
  currentTimestampNanoseconds?: number | string | null;
  timestampOrigin?: "Unix" | "Elapsed";
  onSeek: (sequence: number) => void;
  onScrubStart: () => void;
  onScrubEnd: (sequence: number) => void;
  onOpenBreakpointDialog?: (draft: BreakpointDialogDraft) => void;
  onQuickBookmarkSample?: (payload: {
    sequence: number;
    timestampNanoseconds?: number | string;
    title?: string;
    annotation?: string;
  }) => void;
  investigationBookmarks?: InvestigationBookmark[];
  timelineEvents?: ChartTimelineEntry[];
  timelineStages?: ChartTimelineEntry[];
  /** Bumped when the parent wants the chart viewport zoomed (not the replay cursor). */
  viewportFocusRequestId?: number;
  viewportFocusSequence?: number | null;
  restoreChartViewport?: ChartViewportSnapshot | null;
  restoreChartViewportRequestId?: number;
  registerChartViewportReader?: (reader: ChartViewportReader | null) => void;
  breakpoints?: Breakpoint[];
  compositeBreakpoints?: CompositeBreakpoint[];
  breakpointHitsRefreshKey?: number;
  onBreakpointTimelineHit?: (sampleSequence: number, breakpointId: string) => void;
  /** While playing, the loaded window slides with the cursor. Zoom stays available. */
  playbackState?: "stopped" | "playing" | "paused";
}

type HoverDetails = {
  sequence: number;
  timestamp: string;
  x: number;
  y: number;
  values: { name: string; value: string; color?: string }[];
  activeStage?: ChartTimelineEntry | null;
  lastEvent?: ChartTimelineEntry | null;
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

const PAGE_SIZE = 5000;
const CHART_TOOLTIP_WIDTH = 340;

type PendingChartViewport =
  | { kind: "reset" }
  | { kind: "restore"; sequence: number; visibleWidth: number; anchor: number };

function alignedPageStarts(windowStart: number, windowEnd: number): number[] {
  if (windowEnd <= windowStart) return [];
  const first = Math.floor(windowStart / PAGE_SIZE) * PAGE_SIZE;
  const last = Math.floor((windowEnd - 1) / PAGE_SIZE) * PAGE_SIZE;
  const starts: number[] = [];
  for (let start = first; start <= last; start += PAGE_SIZE) starts.push(start);
  return starts;
}

function pageStartForSequence(sequence: number, sampleCount: number): number {
  if (sampleCount <= 0) return 0;
  const last = Math.floor((sampleCount - 1) / PAGE_SIZE) * PAGE_SIZE;
  return Math.max(0, Math.min(last, Math.floor(sequence / PAGE_SIZE) * PAGE_SIZE));
}

// During playback the loaded page slides so the cursor stays in view, instead of
// waiting for a 5,000-sample boundary and jumping. Shift only near the edge.
const PLAYBACK_SLIDE_EDGE = 0.9;
const PLAYBACK_SLIDE_ANCHOR = 0.75;

function slidingWindowStart(
  windowStart: number,
  sequence: number,
  sampleCount: number,
  forward: boolean,
): number | null {
  if (sampleCount <= 0) return null;
  const windowSize = Math.min(PAGE_SIZE, sampleCount);
  const maxStart = Math.max(0, sampleCount - windowSize);
  const start = Math.max(0, Math.min(maxStart, windowStart));
  const end = Math.min(sampleCount, start + windowSize);
  const span = Math.max(1, end - start);
  const outside = sequence < start || sequence >= end;
  const fraction = (sequence - start) / span;
  const nearEdge = forward
    ? fraction >= PLAYBACK_SLIDE_EDGE
    : fraction <= 1 - PLAYBACK_SLIDE_EDGE;
  if (!outside && !nearEdge) return null;

  const anchor = forward ? PLAYBACK_SLIDE_ANCHOR : 1 - PLAYBACK_SLIDE_ANCHOR;
  const next = Math.round(sequence - anchor * windowSize);
  const clamped = Math.max(0, Math.min(maxStart, next));
  return clamped === start ? null : clamped;
}

type PlottedSeriesMeta = {
  yAxisIndex: number;
  color: string;
  channelName: string;
  unit: string | null;
};

// Same dash length for every right-axis series. A shared pixel pattern keeps
// the gaps identical even when several dashed traces share that axis.
const RIGHT_AXIS_DASH = [8, 5];

function seriesLineStyleForAxis(
  yAxisIndex: number,
  baseWidth: number,
) {
  const width = Math.max(1.35, baseWidth);
  return {
    type: (yAxisIndex === 1 ? RIGHT_AXIS_DASH : "solid") as "solid" | number[],
    width,
    opacity: 1,
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

/** Round up to a readable axis ceiling (1.2K, 1.5K, 2K, 5K, 10K, 12K, …). */
function niceCeilAxisMax(value: number): number {
  if (!Number.isFinite(value) || value <= 0) return value;

  const exponent = Math.floor(Math.log10(value));
  const base = 10 ** exponent;
  const niceMantissas = [1, 1.1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10, 12];

  for (const mantissa of niceMantissas) {
    if (mantissa * base >= value - 1e-9) return mantissa * base;
  }

  return 10 * base;
}

type BuiltChartSeries = {
  config: {
    yAxisIndex: number;
    data: [number, number | null][];
  };
};

function computeSeriesAxisExtent(
  builtSeries: BuiltChartSeries[],
  axisIndex: number,
): { dataMin: number; dataMax: number } | null {
  let dataMin = Number.POSITIVE_INFINITY;
  let dataMax = Number.NEGATIVE_INFINITY;

  for (const { config } of builtSeries) {
    if (config.yAxisIndex !== axisIndex) continue;
    for (const [, y] of config.data) {
      if (typeof y !== "number" || !Number.isFinite(y)) continue;
      dataMin = Math.min(dataMin, y);
      dataMax = Math.max(dataMax, y);
    }
  }

  if (!Number.isFinite(dataMax)) return null;
  if (!Number.isFinite(dataMin)) dataMin = 0;
  return { dataMin, dataMax };
}

/**
 * Extra headroom on the right axis when dual Y-axes are active keeps high-magnitude
 * series from visually lining up with unrelated left-axis gridlines.
 */
function computeRightAxisMax(
  extent: { dataMin: number; dataMax: number },
): number {
  const span = Math.max(
    extent.dataMax - extent.dataMin,
    Math.abs(extent.dataMax) * 0.08,
    AXIS_RANGE_MIN_SCALE,
  );
  const paddedMax = Math.max(
    extent.dataMax + span * 0.28,
    extent.dataMax * 1.35,
  );
  return niceCeilAxisMax(paddedMax);
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
  if (sampleCount <= 2000) return 1.75;
  return 1.5;
}

export function RecordingChart({
  sessionId,
  sampleCount,
  channels,
  selectedChannelIds,
  axisPins,
  onAxisPinChange,
  onClearAxisPins,
  onEffectiveRightAxisChange,
  currentSequence,
  showCursor = false,
  currentTimestampNanoseconds,
  timestampOrigin = "Unix",
  onSeek,
  onScrubStart,
  onScrubEnd,
  onOpenBreakpointDialog,
  onQuickBookmarkSample,
  investigationBookmarks = [],
  timelineEvents = [],
  timelineStages = [],
  viewportFocusRequestId = 0,
  viewportFocusSequence = null,
  restoreChartViewport = null,
  restoreChartViewportRequestId = 0,
  registerChartViewportReader,
  breakpoints = [],
  compositeBreakpoints = [],
  breakpointHitsRefreshKey = 0,
  onBreakpointTimelineHit,
  playbackState = "stopped",
}: RecordingChartProps) {
  const theme = useTheme();

  const chartTheme = useMemo(
    () => createChartTheme(theme.palette.mode),
    [theme.palette.mode],
  );

  const chartRef = useRef<ReactECharts>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [plotFrame, setPlotFrame] = useState<ChartPlotFrame | null>(null);
  const plotAreaRef = useRef<HTMLDivElement>(null);
  const zoomSelectRef = useRef<HTMLDivElement>(null);
  const boxZoomRef = useRef<{
    pointerId: number;
    originX: number;
    originY: number;
    active: boolean;
    dragging: boolean;
    applied: boolean;
  } | null>(null);

  const BOX_ZOOM_DRAG_THRESHOLD_PX = 8;

  const [axisMenuAnchor, setAxisMenuAnchor] = useState<HTMLElement | null>(null);
  const [samples, setSamples] = useState<SampleRangeItem[]>([]);
  const suggestedRight = useMemo(
    () => suggestRightAxisChannelIds(channels, selectedChannelIds, samples),
    [channels, selectedChannelIds, samples],
  );
  const effectiveRightIds = useMemo(
    () => resolveEffectiveRightAxisIds(
      suggestedRight,
      selectedChannelIds,
      axisPins,
    ),
    [suggestedRight, selectedChannelIds, axisPins],
  );
  const rightAxisSet = useMemo(
    () => new Set(effectiveRightIds),
    [effectiveRightIds],
  );
  const hasRightAxis = selectedChannelIds.some((id) => rightAxisSet.has(id));

  useEffect(() => {
    onEffectiveRightAxisChange?.(effectiveRightIds);
  }, [effectiveRightIds, onEffectiveRightAxisChange]);

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
  const stitchWindow = (start: number, end: number): SampleRangeItem[] | null => {
    if (end <= start) return [];
    const parts: SampleRangeItem[] = [];
    for (const aligned of alignedPageStarts(start, end)) {
      const page = pageCacheRef.current.get(pageKey(aligned));
      if (!page) return null;
      parts.push(...page);
    }
    const sliced = parts.filter(
      (sample) => sample.sequence >= start && sample.sequence < end,
    );
    return sliced.length > 0 ? sliced : null;
  };
  const [pageStart, setPageStart] = useState(0);
  const pageCount = Math.ceil(sampleCount / PAGE_SIZE);
  const maxPageStart = sampleCount <= 0 ? 0 : Math.floor((sampleCount - 1) / PAGE_SIZE) * PAGE_SIZE;
  const activePage = Math.min(pageCount - 1, Math.max(0, Math.floor(pageStart / PAGE_SIZE)));
  const pageEnd = Math.min(sampleCount, pageStart + PAGE_SIZE);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [hover, setHover] = useState<HoverDetails | null>(null);
  const [eventTip, setEventTip] = useState<TimelineEventHit | null>(null);
  const eventTipKeyRef = useRef<string | null>(null);
  const eventTipPosRef = useRef<{ x: number; y: number } | null>(null);
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
  const tooltipPanelRef = useRef<HTMLDivElement>(null);
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

  const suppressBrowserContextMenu = (event: ReactMouseEvent) => {
    event.preventDefault();
  };

  const openSampleContextMenu = (
    event: ReactMouseEvent,
    sequence: number,
    anchor: { x: number; y: number },
  ) => {
    event.preventDefault();
    event.stopPropagation();

    const sample = samples.find((item) => item.sequence === sequence);
    const timestamp = sample
      ? formatTimestamp(sample.timestampNanoseconds, timestampOrigin)
      : sequence === currentSequence && currentTimestampNanoseconds != null
        ? formatTimestamp(String(currentTimestampNanoseconds), timestampOrigin)
        : "";

    setHover(null);
    setInspectedSample(null);
    setChartMenu({
      clientX: event.clientX,
      clientY: event.clientY,
      sequence,
      timestamp,
      x: anchor.x,
      y: anchor.y,
    });
  };

  // Sample menu only inside the plot grid; elsewhere on the chart block the browser menu.
  // The cursor label uses the replay sequence itself, so it does not snap to a pixel.
  const handlePlotContextMenu = (event: ReactMouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.stopPropagation();

    const chart = chartRef.current?.getEchartsInstance();
    if (!chart || chart.isDisposed()) return;
    const bounds = event.currentTarget.getBoundingClientRect();
    const x = event.clientX - bounds.left;
    const y = event.clientY - bounds.top;
    if (!chart.containPixel({ gridIndex: 0 }, [x, y])) return;
    const coordinates = chart.convertFromPixel({ gridIndex: 0 }, [x, y]) as number[];
    if (!Number.isFinite(coordinates?.[0]) || samples.length === 0) return;
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
        color: seriesColorByChannelIdRef.current.get(channel.channelId),
      })),
      activeStage: resolveActiveStage(timelineStages, sample.sequence),
      lastEvent: resolveActiveStage(timelineEvents, sample.sequence),
    });
    setChartMenu(null);
  };



  const displayedSample =
    inspectedSample ?? (tooltipsEnabled && !eventTip ? hover : null);

  const clearEventTip = () => {
    if (eventTipKeyRef.current === null && eventTipPosRef.current === null) return;
    eventTipKeyRef.current = null;
    eventTipPosRef.current = null;
    setEventTip(null);
  };

  useEffect(() => {
    eventTipKeyRef.current = null;
    eventTipPosRef.current = null;
    setEventTip(null);
  }, [sessionId, pageStart]);

  const toggleTooltips = () => {
    const next = !tooltipsEnabledRef.current;
    tooltipsEnabledRef.current = next;
    setTooltipsEnabled(next);
    if (!next) {
      setHover(null);
      clearEventTip();
    }
  };
  const withChart = (action: (chart: EChartsType) => void) => {
    const chart = chartRef.current?.getEchartsInstance();
    if (!chart || chart.isDisposed()) return;
    action(chart);
  };

  const handleResetZoom = () => {
    withChart((chart) => resetViewport(chart));
  };

  const handleZoomIn = () => {
    withChart((chart) => {
      const { pageStart: ps, pageEnd: pe } = pageWindowRef.current;
      zoomViewportByFactor(chart, ps, pe, 0.8);
    });
  };

  const handleZoomOut = () => {
    withChart((chart) => {
      const { pageStart: ps, pageEnd: pe } = pageWindowRef.current;
      zoomViewportByFactor(chart, ps, pe, 1.25);
    });
  };

  const handleZoomToReplay = () => {
    const sequence = previewSequence ?? currentSequence;
    if (sequence == null) return;
    withChart((chart) => {
      const { pageStart: ps, pageEnd: pe } = pageWindowRef.current;
      zoomViewportToSequence(chart, ps, pe, sequence, 0.15);
    });
  };

  const handleBreakpointTimelineHit = (sequence: number, breakpointId: string) => {
    manualPageRef.current = false;
    onSeek(sequence);
    withChart((chart) => {
      const { pageStart: ps, pageEnd: pe } = pageWindowRef.current;
      zoomViewportToSequence(chart, ps, pe, sequence, 0.12);
    });
    onBreakpointTimelineHit?.(sequence, breakpointId);
  };

  const hideZoomSelection = () => {
    const band = zoomSelectRef.current;
    if (band) band.style.display = "none";
  };

  const updateZoomSelection = (clientX: number) => {
    const band = zoomSelectRef.current;
    const plot = plotAreaRef.current;
    const box = boxZoomRef.current;
    if (!band || !plot || !box) return;

    const rect = plot.getBoundingClientRect();
    const start = Math.max(0, Math.min(rect.width, box.originX - rect.left));
    const end = Math.max(0, Math.min(rect.width, clientX - rect.left));
    const left = Math.min(start, end);
    const width = Math.abs(end - start);

    band.style.display = width < 2 ? "none" : "block";
    band.style.left = `${left}px`;
    band.style.width = `${width}px`;
  };

  const seekAtClientPosition = (clientX: number, clientY: number) => {
    const chart = chartRef.current?.getEchartsInstance();
    const plot = plotAreaRef.current;
    if (!chart || chart.isDisposed() || !plot) return;

    const rect = plot.getBoundingClientRect();
    const localX = clientX - rect.left;
    const localY = clientY - rect.top;
    if (!chart.containPixel({ gridIndex: 0 }, [localX, localY])) return;

    const coordinates = chart.convertFromPixel(
      { gridIndex: 0 },
      [localX, localY],
    ) as number[];
    const loaded = samplesRef.current;
    if (!loaded.length || !Number.isFinite(coordinates[0])) return;

    const sequence = nearestLoadedSampleSequence(loaded, coordinates[0]);
    if (sequence == null) return;

    setInspectedSample(null);
    if (sequence !== currentSequenceRef.current) {
      manualPageRef.current = false;
      onSeekRef.current(sequence);
    }
  };

  const applyBoxZoom = (clientX: number) => {
    const plot = plotAreaRef.current;
    const box = boxZoomRef.current;
    const chart = chartRef.current?.getEchartsInstance();
    if (!plot || !box || !chart || chart.isDisposed()) return false;

    const rect = plot.getBoundingClientRect();
    const delta = Math.abs(clientX - box.originX);
    if (delta < BOX_ZOOM_DRAG_THRESHOLD_PX) return false;

    const x0 = Math.max(0, Math.min(rect.width, box.originX - rect.left));
    const x1 = Math.max(0, Math.min(rect.width, clientX - rect.left));
    const left = Math.min(x0, x1);
    const right = Math.max(x0, x1);

    const startCoord = chart.convertFromPixel(
      { gridIndex: 0 },
      [left, rect.height / 2],
    ) as number[];
    const endCoord = chart.convertFromPixel(
      { gridIndex: 0 },
      [right, rect.height / 2],
    ) as number[];

    const loaded = samplesRef.current;
    if (!loaded.length || !Number.isFinite(startCoord[0]) || !Number.isFinite(endCoord[0])) {
      return false;
    }

    const minBound = loaded[0].sequence;
    const maxBound = loaded[loaded.length - 1].sequence;
    const minSequence = Math.max(
      minBound,
      Math.min(maxBound, Math.round(Math.min(startCoord[0], endCoord[0]))),
    );
    const maxSequence = Math.max(
      minBound,
      Math.min(maxBound, Math.round(Math.max(startCoord[0], endCoord[0]))),
    );

    const { pageStart: ps, pageEnd: pe } = pageWindowRef.current;
    const percents = sequencesToViewportPercents(ps, pe, minSequence, maxSequence);
    applyViewportPercents(chart, percents.startPercent, percents.endPercent);
    suppressClickRef.current = true;
    return true;
  };

  const handlePlotPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0 || event.shiftKey || dragRef.current) return;
    const chart = chartRef.current?.getEchartsInstance();
    if (!chart || chart.isDisposed()) return;

    const plot = plotAreaRef.current;
    if (!plot) return;

    const rect = plot.getBoundingClientRect();
    const localX = event.clientX - rect.left;
    const localY = event.clientY - rect.top;
    if (!chart.containPixel({ gridIndex: 0 }, [localX, localY])) return;

    boxZoomRef.current = {
      pointerId: event.pointerId,
      originX: event.clientX,
      originY: event.clientY,
      active: true,
      dragging: false,
      applied: false,
    };
  };

  const handlePlotPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const box = boxZoomRef.current;
    if (!box?.active || box.pointerId !== event.pointerId) return;

    const deltaX = Math.abs(event.clientX - box.originX);
    if (!box.dragging && deltaX >= BOX_ZOOM_DRAG_THRESHOLD_PX) {
      box.dragging = true;
      plotAreaRef.current?.setPointerCapture(event.pointerId);
    }

    if (box.dragging) {
      updateZoomSelection(event.clientX);
    }
  };

  const finishPlotPointer = (event: PointerEvent<HTMLDivElement>, cancelled: boolean) => {
    const box = boxZoomRef.current;
    if (!box?.active || box.pointerId !== event.pointerId) return;

    if (plotAreaRef.current?.hasPointerCapture(event.pointerId)) {
      plotAreaRef.current.releasePointerCapture(event.pointerId);
    }

    if (!cancelled) {
      if (box.dragging) {
        box.applied = applyBoxZoom(event.clientX);
      } else {
        seekAtClientPosition(event.clientX, event.clientY);
        suppressClickRef.current = true;
      }
    }

    boxZoomRef.current = null;
    hideZoomSelection();
  };

  const handlePlotPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    finishPlotPointer(event, false);
  };

  const handlePlotPointerCancel = (event: PointerEvent<HTMLDivElement>) => {
    finishPlotPointer(event, true);
  };

  const handlePlotDoubleClick = (event: ReactMouseEvent<HTMLDivElement>) => {
    if (event.shiftKey) return;
    const chart = chartRef.current?.getEchartsInstance();
    if (!chart || chart.isDisposed()) return;

    const plot = plotAreaRef.current;
    if (!plot) return;

    const rect = plot.getBoundingClientRect();
    const localX = event.clientX - rect.left;
    const localY = event.clientY - rect.top;
    if (!chart.containPixel({ gridIndex: 0 }, [localX, localY])) return;

    event.preventDefault();
    event.stopPropagation();
    suppressClickRef.current = true;
    boxZoomRef.current = null;
    hideZoomSelection();
    handleResetZoom();
  };

  const listenerCleanupRef = useRef<(() => void) | null>(null);

  // Keep the latest callback and sample range accessible
  // without re-registering chart listeners on each render.
  const onSeekRef = useRef(onSeek);
  const investigationBookmarksRef = useRef(investigationBookmarks);
  investigationBookmarksRef.current = investigationBookmarks;
  const timelineStagesRef = useRef(timelineStages);
  timelineStagesRef.current = timelineStages;
  const timelineEventsRef = useRef(timelineEvents);
  timelineEventsRef.current = timelineEvents;
  const eventMarkerRef = useRef<{ y: number; yAxisIndex: 0 | 1 } | null>(null);
  const onScrubStartRef = useRef(onScrubStart);
  const onScrubEndRef = useRef(onScrubEnd);
  const samplesRef = useRef(samples);
  const currentSequenceRef = useRef(currentSequence);
  const channelsRef = useRef(channels);
  const selectedIdsRef = useRef(selectedChannelIds);
  const rightAxisSetRef = useRef(rightAxisSet);
  const hasRightAxisRef = useRef(hasRightAxis);
  const seriesColorByChannelIdRef = useRef<Map<string, string>>(new Map());
  const pageWindowRef = useRef({ pageStart, pageEnd });
  const sampleCountRef = useRef(sampleCount);
  const pendingChartViewportRef = useRef<PendingChartViewport | null>(null);
  const chartViewRef = useRef<{ pageStart: number; pageEnd: number; samples: SampleRangeItem[] }>({
    pageStart: 0,
    pageEnd: 0,
    samples: [],
  });
  const chartViewSessionRef = useRef(sessionId);
  const edgeScrollAtRef = useRef(0);
  const cursorUiRef = useRef({ cursorHovered, previewSequence, paletteMode: theme.palette.mode });

  pageWindowRef.current = { pageStart, pageEnd };
  cursorUiRef.current = { cursorHovered, previewSequence, paletteMode: theme.palette.mode };

  onSeekRef.current = onSeek;
  onScrubStartRef.current = onScrubStart;
  onScrubEndRef.current = onScrubEnd;
  samplesRef.current = samples;
  sampleCountRef.current = sampleCount;
  currentSequenceRef.current = currentSequence;
  channelsRef.current = channels;
  selectedIdsRef.current = selectedChannelIds;
  rightAxisSetRef.current = rightAxisSet;
  hasRightAxisRef.current = hasRightAxis;

  // Track the last replay position independently of manual chart paging.
  // A page change must never be interpreted as a replay cursor movement.
  // Manual page browsing is independent of the replay cursor.
  const manualPageRef = useRef(false);
  // A window left between page boundaries follows the cursor onto the real page
  // once the cursor leaves it. Playback may leave the window unaligned while
  // the cursor is still inside, and pausing keeps that view.
  if (
    playbackState !== "playing" &&
    sampleCount > 0 &&
    currentSequence !== null &&
    !manualPageRef.current &&
    dragRef.current === null &&
    pageStart % PAGE_SIZE !== 0 &&
    (currentSequence < pageStart || currentSequence >= pageEnd)
  ) {
    const aligned = pageStartForSequence(
      Math.max(0, Math.min(currentSequence, sampleCount - 1)),
      sampleCount,
    );
    if (aligned !== pageStart) {
      pendingChartViewportRef.current = { kind: "reset" };
      setPageStart(aligned);
    }
  }
  const activePageRef = useRef(activePage);
  activePageRef.current = activePage;
  const pageLoadGenerationRef = useRef(0);
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

    // Resume auto-follow only after the cursor reaches the page the user
    // navigated to (not on the first boundary cross while still behind it).
    if (crossedPageBoundary && currentReplayPage !== null &&
        currentReplayPage >= activePageRef.current) {
      manualPageRef.current = false;
    }

    if (dragRef.current || manualPageRef.current) return;

    // Manual Next/Previous page navigation does not change either value.
    if (!sessionChanged && !sequenceChanged) return;

    if (
      playbackState === "playing" &&
      !sessionChanged &&
      currentSequence !== null &&
      sampleCount > 0
    ) {
      const sequence = Math.max(0, Math.min(currentSequence, sampleCount - 1));
      const forward = previous?.sequence == null || sequence >= previous.sequence;
      const nextStart = slidingWindowStart(pageStart, sequence, sampleCount, forward);
      if (nextStart !== null) {
        pendingChartViewportRef.current = { kind: "reset" };
        setPageStart(nextStart);
      }
      return;
    }

    if (currentSequence === null || sampleCount <= 0) {
      pendingChartViewportRef.current = { kind: "reset" };
      setPageStart(0);
      return;
    }

    const sequence = Math.max(0, Math.min(currentSequence, sampleCount - 1));
    const aligned = pageStartForSequence(sequence, sampleCount);
    if (sessionChanged) {
      pendingChartViewportRef.current = { kind: "reset" };
      setPageStart((current) => (current === aligned ? current : aligned));
      return;
    }

    // Stay on the loaded window while the cursor is still inside it, including a
    // window that playback slid off a page boundary. Leaving that window opens
    // the fixed page that contains the cursor.
    const windowEnd = Math.min(sampleCount, pageStart + PAGE_SIZE);
    if (sequence >= pageStart && sequence < windowEnd) return;

    pendingChartViewportRef.current = { kind: "reset" };
    setPageStart(aligned);
  }, [sessionId, currentSequence, sampleCount, previewSequence, playbackState, pageStart, pageEnd]);

  // Layout effect applies prefetched data before the browser paints a new page.
  // Keep the current traces up until the overlapping window is ready.
  useLayoutEffect(() => {
    const loadId = ++pageLoadGenerationRef.current;
    if (!dragRef.current) {
      setHover(null);
      setInspectedSample(null);
      setCursorLabelX(null);
    }

    if (sampleCount <= 0 || pageStart >= sampleCount) {
      setSamples([]);
      setLoading(false);
      setError("");
      return;
    }

    if (selectedChannelIds.length === 0) {
      setSamples([]);
      setLoading(false);
      setError("");
      return;
    }

    const windowEnd = Math.min(sampleCount, pageStart + PAGE_SIZE);
    const applySamples = (loaded: SampleRangeItem[]) => {
      if (pageLoadGenerationRef.current !== loadId) return;
      setSamples(loaded);
      setLoading(false);
      setError("");
    };

    const publish = () => {
      const stitched = stitchWindow(pageStart, windowEnd);
      if (!stitched) return false;
      applySamples(stitched);
      return true;
    };

    if (!publish()) {
      if (samplesRef.current.length === 0) {
        setLoading(true);
        setError("");
      }
      void Promise.all(alignedPageStarts(pageStart, windowEnd).map((start) => requestPage(start)))
        .then(() => {
          if (!publish()) {
            if (pageLoadGenerationRef.current !== loadId) return;
            setError("Unable to load recording samples.");
            setLoading(false);
          }
        })
        .catch((caught) => {
          if (pageLoadGenerationRef.current !== loadId) return;
          setError(
            caught instanceof Error
              ? caught.message
              : "Unable to load recording samples.",
          );
          setLoading(false);
        });
    }

    const covered = alignedPageStarts(pageStart, windowEnd);
    const neighbors = [
      (covered[0] ?? pageStart) - PAGE_SIZE,
      (covered[covered.length - 1] ?? pageStart) + PAGE_SIZE,
    ];
    for (const neighbor of neighbors) {
      if (neighbor >= 0 && neighbor < sampleCount) {
        void requestPage(neighbor).catch(() => { /* Retry on actual navigation. */ });
      }
    }
  }, [sessionId, pageStart, sampleCount, selectedChannelIds.length]);

  useEffect(() => {
    if (selectedChannelIds.length === 0) {
      setHover(null);
      setInspectedSample(null);
      setCursorLabelX(null);
    }
  }, [selectedChannelIds.length]);

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
  const navigateToPage = (direction: -1 | 1) => {
    if (playbackState === "playing") return;
    const aligned = Math.floor(pageStart / PAGE_SIZE) * PAGE_SIZE;
    const next = Math.max(0, Math.min(maxPageStart, aligned + direction * PAGE_SIZE));
    if (next === pageStart) return;
    manualPageRef.current = true;
    pendingChartViewportRef.current = { kind: "reset" };
    setCursorLabelX(null);
    setPageStart(next);
  };

  const samplesMatchWindow =
    samples.length > 0 &&
    samples[0].sequence >= pageStart &&
    samples[samples.length - 1].sequence < pageEnd;
  if (chartViewSessionRef.current !== sessionId) {
    chartViewSessionRef.current = sessionId;
    chartViewRef.current = { pageStart: 0, pageEnd: 0, samples: [] };
  } else if (samplesMatchWindow) {
    chartViewRef.current = { pageStart, pageEnd, samples };
  }
  const plotPageStart = chartViewRef.current.pageStart;
  const plotPageEnd = chartViewRef.current.pageEnd;
  const plotSamples = chartViewRef.current.samples;

  const lineWidth = getLineWidth(plotSamples.length);

  const hasPlottedSeries = useMemo(() => {
    if (selectedChannelIds.length === 0 || plotSamples.length === 0) return false;
    return channels.some(
      (channel) =>
        selectedChannelIds.includes(channel.channelId) &&
        isNumericChartChannel(plotSamples, channel.channelId),
    );
  }, [channels, selectedChannelIds, plotSamples]);

  const visibleInvestigationBookmarkCount = useMemo(
    () =>
      investigationBookmarks.filter(
        (item) =>
          item.sampleSequence >= plotPageStart && item.sampleSequence < plotPageEnd,
      ).length,
    [investigationBookmarks, plotPageStart, plotPageEnd],
  );

  const showAxisRangeWarning = useMemo(() => {
    if (samples.length === 0) return false;
    if (Object.keys(axisPins).length === 0) return false;

    const plottedChannelIds = channels
      .filter(
        (channel) =>
          selectedChannelIds.includes(channel.channelId) &&
          isNumericChartChannel(samples, channel.channelId),
      )
      .map((channel) => channel.channelId);

    if (plottedChannelIds.length < 2) return false;

    const rangesForAxis = (useRight: boolean) =>
      plottedChannelIds
        .filter((channelId) => rightAxisSet.has(channelId) === useRight)
        .map((channelId) => collectPlottedSeriesRange(samples, channelId))
        .filter((range): range is PlottedSeriesRange => range !== null);

    return (
      axisHasDivergentRanges(rangesForAxis(false)) ||
      axisHasDivergentRanges(rangesForAxis(true))
    );
  }, [samples, channels, selectedChannelIds, rightAxisSet, axisPins]);

  const option = useMemo(() => {
    const pageStart = plotPageStart;
    const pageEnd = plotPageEnd;
    const samples = plotSamples;
    const selectedIds = new Set(selectedChannelIds);

    const plotCandidates = channels
      .map((channel, channelIndex) => ({
        channel,
        channelIndex,
      }))
      .filter(({ channel }) =>
        selectedIds.has(channel.channelId) && isNumericChartChannel(samples, channel.channelId),
      );

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
    seriesColorByChannelIdRef.current = seriesColorByChannelId;

    const builtSeries = plotCandidates.map(({ channel, channelIndex }) => {
        const yAxisIndex = rightAxisSet.has(channel.channelId) ? 1 : 0;
        const seriesColor =
          seriesColorByChannelId.get(channel.channelId) ??
          getSeriesColor(theme.palette.mode, channelIndex);
        const axisLineStyle = seriesLineStyleForAxis(
          yAxisIndex,
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

          tooltip: {
            show: false,
          },

          data: samples.map(
            (sample) =>
              [
                sample.sequence,
                numericPlotValue(sample, channel.channelId),
              ] as [number, number | null],
          ),
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

    const rightAxisExtent = hasRightAxis
      ? computeSeriesAxisExtent(builtSeries, 1)
      : null;
    const rightAxisMax = rightAxisExtent
      ? computeRightAxisMax(rightAxisExtent)
      : undefined;

    const makeYAxis = (
      style: ReturnType<typeof buildYAxisStyling>,
      position?: "right",
      axisMax?: number,
    ) => ({
      type: "value" as const,
      scale: true,
      position,
      show: position === "right" ? hasRightAxis : true,
      ...(axisMax !== undefined ? { max: axisMax } : {}),
      name: "",
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
        show: true,
        color: chartTheme.axisTextColor,
        opacity: style.labelOpacity,
        formatter: formatNumericTick,
        margin: 6,
      },
      splitLine: {
        show: position !== "right",
        lineStyle: {
          color: chartTheme.gridLineColor,
        },
      },
    });

    const cursorSequence = showCursor ? previewSequence ?? currentSequence : null;
    // Viewport clipping for mark lines is applied after mount via dataZoom sync;
    // do not read the ECharts instance here (disposed during page changes).
    const seriesMarkLine =
      series.length === 0
        ? undefined
        : buildFirstSeriesMarkLine({
            cursorSequence,
            pageStart,
            pageEnd,
            seriesLength: series.length,
            cursorHovered,
            previewSequence,
            paletteMode: theme.palette.mode,
            bookmarks: investigationBookmarks,
          });

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
        ? { ...item, ...(seriesMarkLine ? { markLine: seriesMarkLine } : {}),
            ...(noDataArea ? { markArea: noDataArea } : {}) }
        : item,
    );

    const primaryYAxisIndex: 0 | 1 =
      builtSeries.some((item) => item.meta.yAxisIndex === 0) ? 0 : 1;
    const primaryExtent = computeSeriesAxisExtent(
      builtSeries,
      primaryYAxisIndex,
    );
    const pageEvents = timelineEventsOnPage(
      timelineEvents,
      pageStart,
      pageEnd,
    );
    const markerY = primaryExtent
      ? timelineEventMarkerY(primaryExtent.dataMin, primaryExtent.dataMax)
      : null;
    eventMarkerRef.current =
      markerY === null
        ? null
        : { y: markerY, yAxisIndex: primaryYAxisIndex };
    const eventScatter = markerY === null
      ? null
      : buildTimelineEventScatterSeries({
          events: pageEvents,
          markerY,
          yAxisIndex: primaryYAxisIndex,
          darkMode: theme.palette.mode === "dark",
        });
    const allSeries = eventScatter
      ? [...chartSeries, eventScatter]
      : chartSeries;

    return {
      animation: false,

      backgroundColor: chartTheme.backgroundColor,
      color: chartTheme.color,
      textStyle: chartTheme.textStyle,

      // Event text is a latched panel. ECharts item tips flicker on these dots
      // because series lines and the hover panel steal the pointer.
      tooltip: {
        show: false,
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
                  const swatch =
                    `<span style="display:inline-block;width:8px;height:8px;margin-right:6px;border-radius:50%;background:${match.lineStyle.color};vertical-align:middle"></span>`;
                  return `${swatch}${label}<br/>${side}`;
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
        left: hasRightAxis ? 12 : 16,
        right: hasRightAxis ? 16 : 12,
        top: 42,
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

      yAxis: hasRightAxis
        ? [
            makeYAxis(leftAxisStyle),
            makeYAxis(rightAxisStyle, "right", rightAxisMax),
          ]
        : makeYAxis(leftAxisStyle),

      dataZoom: [
        {
          type: "inside" as const,
          xAxisIndex: 0,
          filterMode: "none" as const,
          zoomOnMouseWheel: false,
          // Wheel zoom is handled on the plot (anchored to pointer). Shift+drag pans.
          moveOnMouseMove: "shift",
          moveOnMouseWheel: false,
        },
      ],

      series: allSeries,
    };
  }, [
      channels,
      plotSamples,
      selectedChannelIds,
      rightAxisSet,
      hasRightAxis,
      chartTheme,
      currentSequence,
      showCursor,
      cursorHovered,
      previewSequence,
      lineWidth,
      plotPageStart,
      plotPageEnd,
      theme.palette.mode,
      investigationBookmarks,
      timelineEvents,
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
    // Playback leaves a manual zoom where the user put it. The sliding window
    // is what recenters the chart when the cursor reaches the edge.
    let followPending = replayMoved && previewSequence === null && playbackState !== "playing";

    const publishPlotFrame = () => {
      try {
        const frame = readChartPlotFrame(chart, pageStart, pageEnd);
        if (!frame) return;
        setPlotFrame((current) => {
          if (
            current &&
            Math.abs(current.left - frame.left) < 0.5 &&
            Math.abs(current.width - frame.width) < 0.5 &&
            Math.abs(current.axisTop - frame.axisTop) < 0.5 &&
            Math.abs(current.min - frame.min) < 0.05 &&
            Math.abs(current.max - frame.max) < 0.05
          ) {
            return current;
          }
          return frame;
        });
      } catch {
        // Chart can throw while it is disposing or replacing axes.
      }
    };

    const update = () => {
      if (disposed || chart.isDisposed()) return;
      const pending = pendingChartViewportRef.current;
      const samplesReady = samples.length > 0
        && samples[0].sequence >= pageStart
        && samples[samples.length - 1].sequence < pageEnd;
      if (pending && samplesReady) {
        try {
          const axis = (chart.getOption().xAxis as Array<{ min?: number; max?: number }> | undefined)?.[0];
          const axisReady = Number(axis?.min) === pageStart
            && Number(axis?.max) === Math.max(pageStart + 1, pageEnd - 1);
          if (axisReady) {
            pendingChartViewportRef.current = null;
            const viewport = readSequenceViewport(chart, pageStart, pageEnd);
            if (viewport) {
              if (pending.kind === "reset") {
                if (viewport.startPercent > 0.5 || viewport.endPercent < 99.5) {
                  chart.dispatchAction({
                    type: "dataZoom",
                    dataZoomIndex: 0,
                    start: 0,
                    end: 100,
                  });
                  return;
                }
              } else {
                const fullSpan = Math.max(0.001, viewport.fullMax - viewport.fullMin);
                const width = Math.min(Math.max(pending.visibleWidth, 1), fullSpan);
                const min = Math.max(
                  viewport.fullMin,
                  Math.min(viewport.fullMax - width, pending.sequence - width * pending.anchor),
                );
                chart.dispatchAction({
                  type: "dataZoom",
                  dataZoomIndex: 0,
                  start: ((min - viewport.fullMin) / fullSpan) * 100,
                  end: ((min + width - viewport.fullMin) / fullSpan) * 100,
                });
                return;
              }
            }
          }
        } catch {
          // Axis is still settling after the window change.
        }
      }
      if (dragRef.current) return;
      publishPlotFrame();
      const sequence = showCursor ? previewSequence ?? currentSequence : null;
      if (!showCursor) {
        setCursorLabelX(null);
        try {
          const seriesId = (option.series?.[0] as { id?: string } | undefined)?.id;
          if (seriesId && (option.series as unknown[]).length > 0) {
            const markLine = buildFirstSeriesMarkLine({
              cursorSequence: null,
              pageStart,
              pageEnd,
              seriesLength: (option.series as unknown[]).length,
              cursorHovered,
              previewSequence,
              paletteMode: theme.palette.mode,
              bookmarks: investigationBookmarks,
            });
            chart.setOption(
              { series: [{ id: seriesId, markLine: markLine ?? { data: [] } }] },
              { lazyUpdate: true },
            );
          }
        } catch {
          // Chart can throw while it is disposing.
        }
        return;
      }
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

        const viewport = readSequenceViewport(chart, pageStart, pageEnd);
        if (!viewport) {
          setCursorLabelX(null);
          return;
        }
        const {
          visibleMin,
          visibleMax,
          visibleWidth,
          fullMin,
          fullMax,
        } = viewport;
        const fullWidth = Math.max(0.001, fullMax - fullMin);

        if (!dragRef.current) {
          const seriesId = (option.series?.[0] as { id?: string } | undefined)?.id;
          if (seriesId && (option.series as unknown[]).length > 0) {
            const markLine = buildFirstSeriesMarkLine({
              cursorSequence: sequence,
              pageStart,
              pageEnd,
              seriesLength: (option.series as unknown[]).length,
              cursorHovered,
              previewSequence,
              paletteMode: theme.palette.mode,
              bookmarks: investigationBookmarks,
              visibleMin,
              visibleMax,
            });
            chart.setOption(
              {
                series: [{
                  id: seriesId,
                  markLine: markLine ?? { data: [] },
                }],
              },
              { lazyUpdate: true },
            );
          }
        }

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
  }, [sessionId, currentSequence, showCursor, previewSequence, pageStart, pageEnd, samples, option]);

  useEffect(() => {
    if (viewportFocusRequestId <= 0 || viewportFocusSequence == null) return;
    withChart((chart) => {
      const { pageStart: ps, pageEnd: pe } = pageWindowRef.current;
      zoomViewportToSequence(chart, ps, pe, viewportFocusSequence, 0.12);
    });
  }, [viewportFocusRequestId, viewportFocusSequence]);

  const pendingViewportRestoreRef = useRef<ChartViewportSnapshot | null>(null);

  useEffect(() => {
    if (restoreChartViewportRequestId <= 0 || !restoreChartViewport) return;
    pendingViewportRestoreRef.current = restoreChartViewport;
    pendingChartViewportRef.current = null;
    manualPageRef.current = true;
    setPageStart(restoreChartViewport.pageStart);
  }, [restoreChartViewportRequestId, restoreChartViewport]);

  useEffect(() => {
    const pending = pendingViewportRestoreRef.current;
    if (!pending || loading || samples.length === 0 || pageStart !== pending.pageStart) {
      return;
    }

    pendingViewportRestoreRef.current = null;
    withChart((chart) => {
      applyViewportPercents(
        chart,
        pending.dataZoomStartPercent,
        pending.dataZoomEndPercent,
      );
    });
    manualPageRef.current = false;
  }, [loading, samples, pageStart]);

  useEffect(() => {
    if (!registerChartViewportReader) return;

    registerChartViewportReader({
      read: () => {
        const chart = chartRef.current?.getEchartsInstance();
        if (!chart || chart.isDisposed()) return null;
        const { pageStart: ps, pageEnd: pe } = pageWindowRef.current;
        const viewport = readSequenceViewport(chart, ps, pe);
        if (!viewport) return null;
        return {
          pageStart: ps,
          dataZoomStartPercent: viewport.startPercent,
          dataZoomEndPercent: viewport.endPercent,
        };
      },
    });

    return () => {
      registerChartViewportReader(null);
    };
  }, [registerChartViewportReader]);

  useEffect(() => {
    const root = containerRef.current;
    if (!root) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      const active = document.activeElement;
      if (
        active !== root &&
        active !== document.body &&
        !root.contains(active)
      ) {
        return;
      }
      if (active === document.body) return;

      const key = event.key;
      const withMeta = event.ctrlKey || event.metaKey;

      if (!withMeta && (key === "=" || key === "+")) {
        event.preventDefault();
        handleZoomIn();
        return;
      }
      if (!withMeta && key === "-") {
        event.preventDefault();
        handleZoomOut();
        return;
      }
      if (!withMeta && key === "0") {
        event.preventDefault();
        handleResetZoom();
        return;
      }
      if (!withMeta && (key === "f" || key === "F")) {
        event.preventDefault();
        handleZoomToReplay();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  });

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

    const window = pageWindowRef.current;
    const count = sampleCountRef.current;
    let axisLeft = Number.NaN;
    let axisRight = Number.NaN;
    try {
      axisLeft = chart.convertToPixel({ xAxisIndex: 0 }, window.pageStart) as number;
      axisRight = chart.convertToPixel(
        { xAxisIndex: 0 },
        Math.max(window.pageStart, window.pageEnd - 1),
      ) as number;
    } catch {
      axisLeft = Number.NaN;
      axisRight = Number.NaN;
    }

    const holdOnEdge = (direction: -1 | 1, edgePixel: number) => {
      const now = performance.now();
      const maxStart = count <= 1 ? 0 : Math.floor((count - 1) / PAGE_SIZE) * PAGE_SIZE;
      const aligned = Math.floor(window.pageStart / PAGE_SIZE) * PAGE_SIZE;
      const nextStart = Math.max(0, Math.min(maxStart, aligned + direction * PAGE_SIZE));
      if (nextStart !== window.pageStart && now - edgeScrollAtRef.current >= 400) {
        edgeScrollAtRef.current = now;
        // Enter the next page at its first sample, or the previous page at its last.
        dragRef.current!.sequence = direction > 0
          ? nextStart
          : Math.min(count - 1, nextStart + PAGE_SIZE - 1);
        pendingChartViewportRef.current = { kind: "reset" };
        setPageStart(nextStart);
        return;
      }
      const sequence = nextStart !== window.pageStart
        ? dragRef.current!.sequence
        : direction > 0
          ? Math.min(count - 1, window.pageEnd - 1)
          : window.pageStart;
      dragRef.current!.sequence = Math.max(0, Math.min(count - 1, sequence));
      let pixel = edgePixel;
      try {
        const converted = chart.convertToPixel({ xAxisIndex: 0 }, dragRef.current!.sequence) as number;
        if (Number.isFinite(converted)) pixel = converted;
      } catch {
        // The axis is still the page being left.
      }
      pendingDragXRef.current = pixel;
      renderDragPosition(pixel, dragRef.current!.sequence);
    };

    if (Number.isFinite(axisRight) && x > axisRight + 6 && window.pageEnd < count) {
      holdOnEdge(1, axisRight);
      return;
    }
    if (Number.isFinite(axisLeft) && x < axisLeft - 6 && window.pageStart > 0) {
      holdOnEdge(-1, axisLeft);
      return;
    }

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
    const restoreMarkLine = (
      chart: EChartsType | undefined,
      seriesId: string | undefined,
      cursorSequence: number | null,
    ) => {
      if (!chart || chart.isDisposed() || !seriesId) return;
      const { pageStart: ps, pageEnd: pe } = pageWindowRef.current;
      const ui = cursorUiRef.current;
      const markLine = buildFirstSeriesMarkLine({
        cursorSequence,
        pageStart: ps,
        pageEnd: pe,
        seriesLength: 1,
        cursorHovered: ui.cursorHovered,
        previewSequence: ui.previewSequence,
        paletteMode: ui.paletteMode,
        bookmarks: investigationBookmarksRef.current,
        ...markLineViewportFromChart(chart, ps, pe),
      });
      chart.setOption({
        series: [{
          id: seriesId,
          markLine: markLine ?? { data: [] },
        }],
      });
    };

    if (restoredChart && !restoredChart.isDisposed() && restoredSeriesId && sequence !== null) {
      restoreMarkLine(restoredChart, restoredSeriesId, sequence);
    }
    if (cancelled) {
      // Restore the original cursor after cancellation without seeking.
      const chart = chartRef.current?.getEchartsInstance();
      const firstSeriesId = (option.series[0] as { id?: string } | undefined)?.id;
      restoreMarkLine(chart, firstSeriesId, currentSequence);
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

  useLayoutEffect(() => {
    if (!dragRef.current || pendingDragXRef.current == null) return;
    renderDragPosition(pendingDragXRef.current, dragRef.current.sequence);
  }, [pageStart, samples]);

  useEffect(() => () => {
    if (dragFrameRef.current !== null) cancelAnimationFrame(dragFrameRef.current);
  }, []);

  // Detach our ZRender listeners when the chart unmounts.
  useEffect(() => () => listenerCleanupRef.current?.(), []);

  // Register click-to-seek when ECharts is initialized.
  // This also handles clicks on empty areas of the plot.
  const handleChartReady = useCallback(
    (chart: EChartsType) => {
      const chartDom = chart.getDom();

      const handleWheel = (event: WheelEvent) => {
        if (chart.isDisposed()) return;
        if (tooltipPanelRef.current?.contains(event.target as Node)) {
          return;
        }

        const rect = chartDom.getBoundingClientRect();
        const localX = event.clientX - rect.left;
        const localY = event.clientY - rect.top;
        if (!chart.containPixel({ gridIndex: 0 }, [localX, localY])) return;

        event.preventDefault();
        event.stopPropagation();
        eventTipKeyRef.current = null;
        eventTipPosRef.current = null;
        setEventTip(null);

        const coordinates = chart.convertFromPixel(
          { gridIndex: 0 },
          [localX, localY],
        ) as number[];
        const loaded = samplesRef.current;
        if (!loaded.length || !Number.isFinite(coordinates[0])) return;

        const anchor = nearestLoadedSampleSequence(loaded, coordinates[0]);
        if (anchor == null) return;

        const factor = event.deltaY < 0 ? 0.85 : 1.18;
        const { pageStart: ps, pageEnd: pe } = pageWindowRef.current;
        zoomViewportAtAnchor(chart, ps, pe, anchor, factor);
        requestAnimationFrame(() => {
          if (chart.isDisposed() || dragRef.current) return;
          const seriesId = (
            chart.getOption().series as Array<{ id?: string }> | undefined
          )?.[0]?.id;
          if (!seriesId) return;
          const vp = readSequenceViewport(chart, ps, pe);
          if (!vp) return;
          const ui = cursorUiRef.current;
          const markLine = buildFirstSeriesMarkLine({
            cursorSequence: ui.previewSequence ?? currentSequenceRef.current,
            pageStart: ps,
            pageEnd: pe,
            seriesLength: 1,
            cursorHovered: ui.cursorHovered,
            previewSequence: ui.previewSequence,
            paletteMode: ui.paletteMode,
            bookmarks: investigationBookmarksRef.current,
            visibleMin: vp.visibleMin,
            visibleMax: vp.visibleMax,
          });
          chart.setOption(
            {
              series: [{
                id: seriesId,
                markLine: markLine ?? { data: [] },
              }],
            },
            { lazyUpdate: true },
          );
        });
      };

      chartDom.addEventListener("wheel", handleWheel, { passive: false, capture: true });

      const renderer = chart.getZr();
             const sequenceAt = (x: number, y: number): number | null => {
         const pixel: [number, number] = [x, y];
         if (!chart.containPixel({ gridIndex: 0 }, pixel)) return null;
         const coords = chart.convertFromPixel({ gridIndex: 0 }, pixel) as number[];
         const loaded = samplesRef.current;
         if (!loaded.length || !Number.isFinite(coords[0])) return null;
         return nearestLoadedSampleSequence(loaded, coords[0]);
       };

       const handleClick = (event: { offsetX: number; offsetY: number }) => {
         if (suppressClickRef.current) {
           suppressClickRef.current = false;
           return;
         }
         const domEvent = (event as { event?: MouseEvent }).event;
         if (domEvent?.shiftKey) return;
         const sequence = sequenceAt(event.offsetX, event.offsetY);
         if (sequence !== null && sequence !== currentSequenceRef.current) {
           setInspectedSample(null);
           manualPageRef.current = false;
           onSeekRef.current(sequence);
         }
       };

const publishEventTip = (next: TimelineEventHit | null) => {
        const key = next?.key ?? null;
        const previous = eventTipPosRef.current;
        const samePosition = next !== null && previous !== null
          && Math.abs(previous.x - next.x) < 0.5
          && Math.abs(previous.y - next.y) < 0.5;
        if (key === eventTipKeyRef.current && (next === null || samePosition)) return;
        eventTipKeyRef.current = key;
        eventTipPosRef.current = next ? { x: next.x, y: next.y } : null;
        setEventTip(next);
      };

      const updateEventTip = (x: number, y: number) => {
        const marker = eventMarkerRef.current;
        const events = timelineEventsRef.current;
        const { pageStart: tipPageStart, pageEnd: tipPageEnd } = pageWindowRef.current;
        if (!marker || events.length === 0) {
          publishEventTip(null);
          return;
        }

        const candidates = [];
        for (const item of events) {
          if (item.sampleSequence < tipPageStart || item.sampleSequence >= tipPageEnd) {
            continue;
          }
          let pixel: number[];
          try {
            pixel = chart.convertToPixel(
              { xAxisIndex: 0, yAxisIndex: marker.yAxisIndex },
              [item.sampleSequence, marker.y],
            ) as number[];
          } catch {
            continue;
          }
          if (!pixel || pixel.length < 2 || !Number.isFinite(pixel[0]) || !Number.isFinite(pixel[1])) {
            continue;
          }
          candidates.push({
            key: `${item.sampleSequence}:${item.name}`,
            name: item.name,
            description: item.description,
            sampleSequence: item.sampleSequence,
            x: pixel[0],
            y: pixel[1],
            distance: Math.hypot(pixel[0] - x, pixel[1] - y),
          });
        }

        publishEventTip(pickTimelineEventHit(candidates, eventTipKeyRef.current));
      };

const handleMove = (event: { offsetX: number; offsetY: number }) => {
        if (!tooltipsEnabledRef.current) return;
        const pixel: [number, number] = [event.offsetX, event.offsetY];
        if (!chart.containPixel({ gridIndex: 0 }, pixel)) {
          setHover(null);
          publishEventTip(null);
          return;
        }
        updateEventTip(event.offsetX, event.offsetY);
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
            color: seriesColorByChannelIdRef.current.get(channel.channelId),
          }));
        setHover({
          sequence: closest.sequence,
          timestamp: formatTimestamp(closest.timestampNanoseconds, timestampOrigin),
          x: event.offsetX,
          y: event.offsetY,
          values,
          activeStage: resolveActiveStage(
            timelineStagesRef.current,
            closest.sequence,
          ),
          lastEvent: resolveActiveStage(
            timelineEventsRef.current,
            closest.sequence,
          ),
        });
      };

      const handleLeave = () => {
        setHover(null);
        publishEventTip(null);
        setCursorHovered(false);
      };

      listenerCleanupRef.current?.();
      renderer.on("click", handleClick);
      renderer.on("mousemove", handleMove);
      renderer.on("globalout", handleLeave);
      listenerCleanupRef.current = () => {
        chartDom.removeEventListener("wheel", handleWheel, true);
        renderer.off("click", handleClick);
        renderer.off("mousemove", handleMove);
        renderer.off("globalout", handleLeave);
      };
    },
    [],
  );

  return (
    <Box
      ref={containerRef}
      tabIndex={0}
      onContextMenu={suppressBrowserContextMenu}
      sx={{
        display: "flex",
        flexDirection: "column",
        width: "100%",
        height: "100%",
        minWidth: 0,
        minHeight: 0,
        overflow: "hidden",
        position: "relative",
        outline: "none",
        "&:focus-visible": {
          boxShadow: `inset 0 0 0 2px ${theme.palette.primary.main}`,
        },
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
            onContextMenu={(event) => {
              if (currentSequence === null) return;
              openSampleContextMenu(event, currentSequence, {
                x: cursorLabelX ?? 24,
                y: 48,
              });
            }}
            sx={{
              fontWeight: 400,
              color: "warning.main",
              fontVariantNumeric: "tabular-nums",
              flexShrink: 0,
              mr: "auto",
              cursor: "context-menu",
            }}
            title="Replay cursor. Right-click for actions at this sample."
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
            Tap a channel to pin it to the opposite axis. Use A in the channel list for auto.
          </MenuItem>
          {Object.keys(axisPins).length > 0 && onClearAxisPins && (
            <MenuItem
              onClick={() => {
                onClearAxisPins();
                setAxisMenuAnchor(null);
              }}
            >
              Reset all axes to auto
            </MenuItem>
          )}
          {channels.filter((channel) => selectedChannelIds.includes(channel.channelId) && isNumericChartChannel(samples, channel.channelId)).map((channel) => {
            const onRight = rightAxisSet.has(channel.channelId);
            const pinned = axisPins[channel.channelId];
            return (
            <MenuItem
              key={channel.channelId}
              onClick={() => onAxisPinChange(channel.channelId, onRight ? "left" : "right")}
            >
              <Checkbox size="small" checked={onRight} tabIndex={-1} disableRipple />
              <ListItemText
                primary={channel.name}
                secondary={
                  `${onRight ? "Right" : "Left"} axis${pinned ? " (pinned)" : " (auto)"}${channel.unit ? ` · ${channel.unit}` : ""}`
                }
              />
            </MenuItem>
            );
          })}
        </Menu>
        <Button
          size="small"
          variant="outlined"
          onClick={handleZoomOut}
          disabled={loading || samples.length === 0}
          title="Zoom out (−)"
          aria-label="Zoom out"
          sx={{ bgcolor: "background.paper", minWidth: 0, px: 1, flexShrink: 0 }}
        >
          <ZoomOutOutlinedIcon fontSize="small" />
        </Button>
        <Button
          size="small"
          variant="outlined"
          onClick={handleZoomIn}
          disabled={loading || samples.length === 0}
          title="Zoom in (+)"
          aria-label="Zoom in"
          sx={{ bgcolor: "background.paper", minWidth: 0, px: 1, flexShrink: 0 }}
        >
          <ZoomInOutlinedIcon fontSize="small" />
        </Button>
        <Button
          size="small"
          variant="outlined"
          onClick={handleZoomToReplay}
          disabled={loading || samples.length === 0 || currentSequence == null}
          title="Zoom viewport to replay cursor (F)"
          aria-label="Zoom to replay cursor"
          sx={{ bgcolor: "background.paper", minWidth: 0, px: 1, flexShrink: 0 }}
        >
          <CenterFocusStrongOutlinedIcon fontSize="small" />
        </Button>
        <Button
          size="small"
          variant="outlined"
          onClick={handleResetZoom}
          disabled={loading || samples.length === 0}
          title="Reset zoom (0)"
          sx={{ bgcolor: "background.paper", flexShrink: 0, whiteSpace: "nowrap" }}
        >
          Reset zoom
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
        {loading && plotSamples.length === 0 && selectedChannelIds.length > 0 ? (
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
        ) : hasPlottedSeries && plotSamples.length > 0 ? (
          <>
          <Box
            ref={plotAreaRef}
            data-chart-plot="true"
            sx={{
              height: "100%",
              width: "100%",
              position: "relative",
              "& canvas": {
                cursor: "default",
              },
            }}
            onContextMenu={handlePlotContextMenu}
            onDoubleClick={handlePlotDoubleClick}
            onPointerDown={handlePlotPointerDown}
            onPointerMove={handlePlotPointerMove}
            onPointerUp={handlePlotPointerUp}
            onPointerCancel={handlePlotPointerCancel}
            onPointerDownCapture={() => containerRef.current?.focus({ preventScroll: true })}
          >
          <Box
            ref={zoomSelectRef}
            aria-hidden
            sx={{
              display: "none",
              position: "absolute",
              top: 36,
              bottom: 44,
              border: 1,
              borderColor: "primary.main",
              bgcolor: (muiTheme) =>
                muiTheme.palette.mode === "dark"
                  ? "rgba(78,168,255,0.16)"
                  : "rgba(29,78,216,0.14)",
              pointerEvents: "none",
              zIndex: 9,
            }}
          />
          <ReactECharts
            key={sessionId}
            ref={chartRef}
            option={option}
            notMerge={false}
            replaceMerge={["series", "yAxis", "xAxis", "grid"]}
            onChartReady={handleChartReady}
            onEvents={{
              click: (params: {
                componentType?: string;
                seriesId?: string;
                data?: {
                  bookmarkSequence?: number;
                  value?: [number, number];
                };
              }) => {
                if (params.seriesId === TIMELINE_EVENTS_SERIES_ID) {
                  const sequence = params.data?.value?.[0];
                  if (sequence == null || !Number.isFinite(sequence)) return;
                  manualPageRef.current = false;
                  onSeekRef.current(sequence);
                  return;
                }
                if (params.componentType !== "markLine") return;
                const sequence = params.data?.bookmarkSequence;
                if (sequence == null || !Number.isFinite(sequence)) return;
                manualPageRef.current = false;
                onSeekRef.current(sequence);
              },
            }}
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
          {showCursor && (dragRef.current !== null || (
            cursorLabelX !== null &&
            (previewSequence ?? currentSequence) !== null &&
            (previewSequence ?? currentSequence)! >= plotPageStart &&
            (previewSequence ?? currentSequence)! < plotPageEnd
          )) && (
            <Box
              ref={cursorHandleRef}
              onPointerDown={handleLabelPointerDown}
              onPointerMove={handleLabelPointerMove}
              onPointerUp={handleLabelPointerUp}
              onPointerCancel={handleLabelPointerCancel}
              onContextMenu={(event) => {
                const sequence = dragRef.current?.sequence ?? previewSequence ?? currentSequence;
                if (sequence === null) return;
                openSampleContextMenu(event, sequence, {
                  x: cursorLabelX ?? event.nativeEvent.offsetX,
                  y: 48,
                });
              }}
              aria-label="Replay cursor. Right-click for actions at this sample."
              title="Drag to move the replay cursor. Right-click for actions at this sample."
              sx={{
                position: "absolute",
                // Place the handle between the legend and the plot (grid starts at 42px).
                top: 22,
                left: cursorLabelX ?? pendingDragXRef.current ?? 0,
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
              {(dragRef.current?.sequence ?? previewSequence ?? currentSequence)?.toLocaleString()}
            </Box>
          )}
          </Box>
          {eventTip && tooltipsEnabled && !inspectedSample && (
            <Box
              sx={{
                position: "absolute",
                zIndex: 11,
                pointerEvents: "none",
                width: 300,
                left: Math.max(
                  8,
                  Math.min(
                    eventTip.x - 150,
                    (plotAreaRef.current?.clientWidth ?? 450) - 308,
                  ),
                ),
                top: eventTip.y > 140 ? eventTip.y - 12 : eventTip.y + 16,
                transform: eventTip.y > 140 ? "translateY(-100%)" : "none",
              }}
            >
              <Box
                sx={{
                  border: 1,
                  borderColor: "divider",
                  borderRadius: 1,
                  bgcolor: "background.paper",
                  boxShadow: 4,
                  px: 1.25,
                  py: 1,
                  maxHeight: 220,
                  overflow: "hidden",
                }}
              >
                <Typography variant="caption" sx={{ display: "block", fontWeight: 700 }}>
                  {eventTip.name}
                </Typography>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ display: "block", mt: 0.25 }}
                >
                  Sample {eventTip.sampleSequence.toLocaleString()}
                </Typography>
                {eventTip.description?.trim() && (
                  <Typography
                    variant="caption"
                    sx={{ display: "block", mt: 0.75, overflowWrap: "anywhere" }}
                  >
                    {eventTip.description.trim()}
                  </Typography>
                )}
              </Box>
            </Box>
          )}
          {displayedSample && (
            <Box
              sx={{
                position: "absolute",
                zIndex: 10,
                pointerEvents: "none",
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
              }}
            >
              <Box
                ref={tooltipPanelRef}
                tabIndex={inspectedSample ? 0 : undefined}
                role={inspectedSample ? "region" : undefined}
                aria-label={inspectedSample ? "Inspect sample values" : undefined}
                onWheel={(event) => event.stopPropagation()}
                onClick={inspectedSample ? (event) => event.stopPropagation() : undefined}
                onMouseDown={inspectedSample ? (event) => event.stopPropagation() : undefined}
                sx={{
                  pointerEvents: inspectedSample ? "auto" : "none",
                  display: "flex",
                  flexDirection: "column",
                  maxHeight: "100%",
                  border: 1,
                  borderColor: "divider",
                  borderRadius: 1,
                  bgcolor: "background.paper",
                  boxShadow: 4,
                  overflow: "hidden",
                }}
              >
                <Box sx={{ flexShrink: 0, p: 1.5, pb: 0.5 }}>
                  <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1 }}>
                    <Typography variant="caption" sx={{ fontWeight: 700 }}>
                      Sequence {displayedSample.sequence.toLocaleString()}
                    </Typography>
                    {inspectedSample && (
                      <Button size="small" sx={{ minWidth: 0, p: 0.25 }} onClick={() => setInspectedSample(null)} aria-label="Close sample inspection">
                        Close
                      </Button>
                    )}
                  </Box>
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                    {displayedSample.timestamp}
                  </Typography>
                </Box>
                <Box
                  sx={{
                    flex: 1,
                    minHeight: 0,
                    overflowY: "auto",
                    overscrollBehavior: "contain",
                    scrollbarGutter: "stable",
                    px: 1.5,
                    pb: inspectedSample ? 0 : 1.5,
                  }}
                >
                  {displayedSample.activeStage && (
                    <Typography
                      variant="caption"
                      color="text.secondary"
                      sx={{ display: "block", mb: 0.5, fontWeight: 600 }}
                    >
                      Stage: {displayedSample.activeStage.name}
                      {displayedSample.activeStage.description?.trim()
                        ? ` — ${displayedSample.activeStage.description.trim()}`
                        : ""}
                    </Typography>
                  )}
                  {displayedSample.lastEvent && (
                    <Typography
                      variant="caption"
                      color="text.secondary"
                      sx={{ display: "block", mb: 1, fontWeight: 600 }}
                    >
                      Last event: {displayedSample.lastEvent.name}
                      {displayedSample.lastEvent.sampleSequence !== displayedSample.sequence
                        ? ` · sample ${displayedSample.lastEvent.sampleSequence.toLocaleString()}`
                        : ""}
                      {displayedSample.lastEvent.description?.trim()
                        ? ` — ${displayedSample.lastEvent.description.trim()}`
                        : ""}
                    </Typography>
                  )}
                  {showAxisRangeWarning && (
                    <Typography
                      variant="caption"
                      sx={{
                        display: "block",
                        mb: 1,
                        color: "warning.main",
                        opacity: 0.85,
                        fontSize: "0.68rem",
                        lineHeight: 1.35,
                      }}
                    >
                      Some series have significantly different value ranges. Consider moving a channel to the other Y-axis.
                    </Typography>
                  )}
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
                      <Box
                        sx={{
                          display: "flex",
                          alignItems: "flex-start",
                          gap: 0.75,
                          flex: 1,
                          minWidth: 0,
                        }}
                      >
                        {entry.color && (
                          <Box
                            aria-hidden
                            sx={{
                              width: 8,
                              height: 8,
                              mt: "0.28rem",
                              borderRadius: "50%",
                              bgcolor: entry.color,
                              flexShrink: 0,
                            }}
                          />
                        )}
                        <Typography
                          variant="caption"
                          sx={{ flex: 1, minWidth: 0, overflowWrap: "anywhere" }}
                        >
                          {entry.name}
                        </Typography>
                      </Box>
                      <Typography variant="caption" sx={{ fontWeight: 600, flexShrink: 0 }}>
                        {entry.value}
                      </Typography>
                    </Box>
                  ))}
                </Box>
                {inspectedSample && (
                <Box sx={{ flexShrink: 0, p: 1.5, pt: 0.5 }}>
              {onOpenBreakpointDialog && (
                <Button
                  size="small"
                  variant="text"
                  fullWidth
                  sx={{ mt: 0.5 }}
                  onClick={() => {
                    const sample = samples.find((item) => item.sequence === inspectedSample.sequence);
                    const plotted = channels.filter((channel) =>
                      selectedChannelIds.includes(channel.channelId));
                    const firstNumeric = plotted.find((channel) =>
                      sample && numericPlotValue(sample, channel.channelId) !== null);
                    const numericValue = firstNumeric && sample
                      ? numericPlotValue(sample, firstNumeric.channelId)
                      : null;
                    onOpenBreakpointDialog({
                      channelId: firstNumeric?.channelId,
                      operator: "GreaterThan",
                      threshold: numericValue ?? undefined,
                      caption: `Sample ${inspectedSample.sequence.toLocaleString()} · ${inspectedSample.timestamp}`,
                    });
                  }}
                >
                  Add breakpoint at this sample…
                </Button>
              )}
              {onQuickBookmarkSample && (
                <Button
                  size="small"
                  variant="outlined"
                  fullWidth
                  sx={{ mt: 1 }}
                  onClick={() => {
                    onQuickBookmarkSample({ sequence: inspectedSample.sequence });
                    setInspectedSample(null);
                  }}
                >
                  Bookmark this sample
                </Button>
              )}
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
                </Box>
                )}
              </Box>
            </Box>
          )}
          </>
        ) : sampleCount > 0 ? (
          <Box
            sx={{
              height: "100%",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              px: 3,
              textAlign: "center",
            }}
          >
            <Box>
              <Typography color="text.secondary" sx={{ fontWeight: 600, mb: 0.75 }}>
                {selectedChannelIds.length === 0
                  ? "No channels on chart"
                  : "No plottable series on this page"}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {selectedChannelIds.length === 0
                  ? "Turn on channels in the Inspector to plot series. Replay position and the timeline below still follow playback."
                  : "Selected channels have no numeric values on this page. Try another page or channel."}
              </Typography>
            </Box>
          </Box>
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
        <BreakpointHitTimeline
          sessionId={sessionId}
          pageStart={plotSamples.length > 0 ? plotPageStart : pageStart}
          pageEnd={plotSamples.length > 0 ? plotPageEnd : pageEnd}
          sampleCount={sampleCount}
          currentSequence={currentSequence}
          breakpoints={breakpoints}
          compositeBreakpoints={compositeBreakpoints}
          channels={channels}
          refreshKey={breakpointHitsRefreshKey}
          plotFrame={plotFrame}
          plotAnchorRef={plotAreaRef}
          onHitClick={handleBreakpointTimelineHit}
        />
      )}

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
            Samples {((plotSamples.length > 0 ? plotPageStart : pageStart) + 1).toLocaleString()}–{(plotSamples.length > 0 ? plotPageEnd : pageEnd).toLocaleString()} of {sampleCount.toLocaleString()}
            {pageCount > 1 ? ` · Page ${Math.floor((plotSamples.length > 0 ? plotPageStart : pageStart) / PAGE_SIZE) + 1} of ${pageCount}` : ""}
            {visibleInvestigationBookmarkCount > 0
              ? ` · ${visibleInvestigationBookmarkCount} bookmark${visibleInvestigationBookmarkCount === 1 ? "" : "s"} (dashed violet — click to seek)`
              : ""}
            {" · Click seek · Drag zoom · Shift+pan · Wheel at pointer · Dbl-click reset"}
          </Typography>
          {pageCount > 1 && (
            <ButtonGroup size="small" variant="outlined" aria-label="Chart page navigation" sx={{ flexShrink: 0 }}>
              <Button disabled={playbackState === "playing" || pageStart <= 0} onClick={() => navigateToPage(-1)}>
                Previous
              </Button>
              <Button
                disabled={playbackState === "playing" || pageStart >= maxPageStart}
                onClick={() => navigateToPage(1)}
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
        {chartMenu && onQuickBookmarkSample && (
          <MenuItem
            onClick={() => {
              onQuickBookmarkSample({ sequence: chartMenu.sequence });
              setChartMenu(null);
            }}
          >
            Bookmark this sample
          </MenuItem>
        )}
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

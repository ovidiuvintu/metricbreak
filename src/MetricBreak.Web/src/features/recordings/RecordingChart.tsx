
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";

import ReactECharts from "echarts-for-react";
import type { EChartsType } from "echarts/core";

import {
  Alert,
  Box,
  CircularProgress,
  Typography,
} from "@mui/material";

import { useTheme } from "@mui/material/styles";

import { recordingApi } from "./recordingApi";
import type { ChannelMetadata } from "./channelMetadata";
import type { SampleRangeItem } from "./sampleTypes";
import { createChartTheme } from "../../theme/chartTheme";

interface RecordingChartProps {
  sessionId: string;
  sampleCount: number;
  channels: ChannelMetadata[];
  selectedChannelIds: string[];
  currentSequence: number | null;
  onSeek: (sequence: number) => void;
}

type HoverDetails = {
  sequence: number;
  timestamp: string;
  x: number;
  y: number;
  values: { name: string; value: string }[];
};

function formatTimestamp(nanoseconds: string): string {
  try {
    // Divide before converting to Number to avoid losing nanosecond precision.
    const milliseconds = BigInt(nanoseconds) / 1_000_000n;
    const date = new Date(Number(milliseconds));
    return Number.isNaN(date.getTime()) ? nanoseconds : date.toISOString();
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

const MAX_SAMPLES = 5000;

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
  onSeek,
}: RecordingChartProps) {
  const theme = useTheme();

  const chartTheme = useMemo(
    () => createChartTheme(theme.palette.mode),
    [theme.palette.mode],
  );

  const chartRef = useRef<ReactECharts>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const [samples, setSamples] = useState<SampleRangeItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [hover, setHover] = useState<HoverDetails | null>(null);
  const listenerCleanupRef = useRef<(() => void) | null>(null);

  // Keep the latest callback and sample range accessible
  // without re-registering chart listeners on each render.
  const onSeekRef = useRef(onSeek);
  const samplesRef = useRef(samples);
  const currentSequenceRef = useRef(currentSequence);
  const channelsRef = useRef(channels);
  const selectedIdsRef = useRef(selectedChannelIds);

  onSeekRef.current = onSeek;
  samplesRef.current = samples;
  currentSequenceRef.current = currentSequence;
  channelsRef.current = channels;
  selectedIdsRef.current = selectedChannelIds;

  useEffect(() => {
    let cancelled = false;

    setSamples([]);
    setHover(null);

    if (sampleCount <= 0) {
      setLoading(false);
      setError("");
      return;
    }

    async function loadSamples() {
      setLoading(true);
      setError("");

      try {
        const result = await recordingApi.getSamples(
          sessionId,
          0,
          Math.min(sampleCount, MAX_SAMPLES),
          );
          console.debug("Sample API response:", {
              sessionId,
              sampleCount,
              requestedCount: Math.min(sampleCount, MAX_SAMPLES),
              samplesLength: result.samples?.length,
          });

        if (!cancelled) {
          setSamples(result.samples);
        }
      } catch (caught) {
        console.error(
          "Failed to load recording samples:",
          caught,
        );

        if (!cancelled) {
          setError(
            caught instanceof Error
              ? caught.message
              : "Unable to load recording samples.",
          );
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void loadSamples();

    return () => {
      cancelled = true;
    };
  }, [sessionId, sampleCount]);

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

  const lineWidth = getLineWidth(samples.length);

  const option = useMemo(() => {
    const selectedIds = new Set(selectedChannelIds);

    const series = channels
      .map((channel, channelIndex) => ({
        channel,
        channelIndex,
      }))
      .filter(({ channel }) =>
        selectedIds.has(channel.channelId),
      )
      .map(({ channel, channelIndex }) => {
        const seriesColor =
          chartTheme.color[
            channelIndex % chartTheme.color.length
          ];

        return {
          id: channel.channelId,

          name: channel.unit
            ? `${channel.name} (${channel.unit})`
            : channel.name,

          type: "line" as const,
          showSymbol: false,
          connectNulls: false,
          smooth: false,
          silent: false,
          cursor: 'default',

          itemStyle: {
            color: seriesColor,
          },

          lineStyle: {
            color: seriesColor,
            width: lineWidth,
            opacity: 1,
          },

          emphasis: {
            focus: "series" as const,
            lineStyle: {
              width: Math.max(2, lineWidth + 1),
            },
          },

          data: samples.map((sample) => [
            sample.sequence,
            sample.channels[channel.channelId] ?? null,
          ]),
        };
      });

    // The cursor position comes from backend replay state.
    // Attach it to the first visible telemetry series.
    const replayCursor =
      currentSequence === null || series.length === 0
        ? undefined
        : {
            silent: true,
            symbol: "none",
            animation: false,
            label: {
              show: false,
            },
            lineStyle: {
              color: chartTheme.cursorColor,
              width: 2,
              type: "dashed" as const,
            },
            data: [{ xAxis: currentSequence }],
          };

    const chartSeries = series.map((item, index) =>
      index === 0 && replayCursor
        ? { ...item, markLine: replayCursor }
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
        type: "scroll" as const,
        orient: "horizontal" as const,

        top: 8,
        left: "center",
        right: 10,

        itemWidth: 18,
        itemHeight: 3,
        itemGap: 14,

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
        left: 20,
        right: 35,
        top: 55,
        bottom: 45,
        containLabel: true,
      },

      xAxis: {
        type: "value" as const,
        name: "Sequence",
        nameLocation: "middle" as const,
        nameGap: 30,
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

      yAxis: {
        type: "value" as const,
        scale: true,

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

          formatter: (value: number) => {
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
          },
        },

        splitLine: {
          show: true,
          lineStyle: {
            color: chartTheme.gridLineColor,
          },
        },
      },

      dataZoom: [
        {
          type: "inside" as const,
          xAxisIndex: 0,
          zoomOnMouseWheel: true,
          moveOnMouseMove: true,
          moveOnMouseWheel: false,
        },
      ],

      series: chartSeries,
    };
  }, [
    channels,
    samples,
    selectedChannelIds,
    chartTheme,
    currentSequence,
    lineWidth,
  ]);

  // Detach our ZRender listeners when the chart unmounts.
  useEffect(() => () => listenerCleanupRef.current?.(), []);

  // Register click-to-seek when ECharts is initialized.
  // This also handles clicks on empty areas of the plot.
  const handleChartReady = useCallback(
    (chart: EChartsType) => {
      console.debug("Replay chart ready");

      const renderer = chart.getZr();

      const handleClick = (event: {
        offsetX: number;
        offsetY: number;
      }) => {
        const pixel: [number, number] = [
          event.offsetX,
          event.offsetY,
        ];

        if (
          !chart.containPixel(
            { gridIndex: 0 },
            pixel,
          )
        ) {
          return;
        }

        const coordinates = chart.convertFromPixel(
          { gridIndex: 0 },
          pixel,
        ) as number[];

        const rawSequence = coordinates[0];

        if (!Number.isFinite(rawSequence)) {
          return;
        }

        const loadedSamples = samplesRef.current;

        if (loadedSamples.length === 0) {
          return;
        }

        const firstSequence =
          loadedSamples[0].sequence;

        const lastSequence =
          loadedSamples[loadedSamples.length - 1]
            .sequence;

        const sequence = Math.max(
          firstSequence,
          Math.min(
            lastSequence,
            Math.round(rawSequence),
          ),
        );

        if (sequence === currentSequenceRef.current) {
          return;
        }

        console.debug("Chart clicked:", sequence);

        onSeekRef.current(sequence);
      };

      const handleMove = (event: { offsetX: number; offsetY: number }) => {
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
            name: channel.unit ? `${channel.name} (${channel.unit})` : channel.name,
            value: formatSampleValue(closest, channel.channelId),
          }));
        setHover({
          sequence: closest.sequence,
          timestamp: formatTimestamp(closest.timestampNanoseconds),
          x: event.offsetX,
          y: event.offsetY,
          values,
        });
      };

      const handleLeave = () => setHover(null);

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

      {sampleCount > MAX_SAMPLES && (
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{
            flexShrink: 0,
            px: 1,
          }}
        >
          Displaying the first{" "}
          {MAX_SAMPLES.toLocaleString()} of{" "}
          {sampleCount.toLocaleString()} samples.
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
          <ReactECharts
            ref={chartRef}
            option={option}
            notMerge
            onChartReady={handleChartReady}
            style={{
              height: "100%",
              width: "100%",
              cursor: "crosshair",
            }}
          />
          {hover && (
            <Box
              sx={{
                position: "absolute",
                zIndex: 10,
                pointerEvents: "none",
                left: Math.max(8, Math.min(hover.x + 14, (containerRef.current?.clientWidth ?? 450) - 260)),
                top: Math.max(8, Math.min(hover.y + 14, (containerRef.current?.clientHeight ?? 400) - 180)),
                width: 240,
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
              <Typography variant="caption" sx={{ display: "block", fontWeight: 700 }}>
                Sequence {hover.sequence.toLocaleString()}
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
                {hover.timestamp}
              </Typography>
              {hover.values.map((entry) => (
                <Box key={entry.name} sx={{ display: "flex", justifyContent: "space-between", gap: 1 }}>
                  <Typography variant="caption" sx={{ overflowWrap: "anywhere" }}>{entry.name}</Typography>
                  <Typography variant="caption" sx={{ fontWeight: 600, flexShrink: 0 }}>{entry.value}</Typography>
                </Box>
              ))}
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
    </Box>
  );
}

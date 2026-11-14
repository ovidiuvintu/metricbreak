
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

  // Keep the latest callback and sample range accessible
  // without re-registering chart listeners on each render.
  const onSeekRef = useRef(onSeek);
  const samplesRef = useRef(samples);
  const currentSequenceRef = useRef(currentSequence);

  onSeekRef.current = onSeek;
  samplesRef.current = samples;
  currentSequenceRef.current = currentSequence;

  useEffect(() => {
    let cancelled = false;

    setSamples([]);

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
        trigger: "axis" as const,
        confine: true,
        axisPointer: {
          type: "cross" as const,
          lineStyle: {
            color: chartTheme.cursorColor,
            width: 1,
            type: "dashed" as const,
          },
        },
      },

      legend: {
        show: false,
      },

      grid: {
        left: 20,
        right: 35,
        top: 20,
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

      renderer.on("click", handleClick);

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

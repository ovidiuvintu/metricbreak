
import { useEffect, useMemo, useRef, useState } from "react";
import ReactECharts from "echarts-for-react";

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
}

const MAX_SAMPLES = 5000;

export function RecordingChart({
  sessionId,
  sampleCount,
  channels,
  selectedChannelIds,
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

  const lineWidth = getLineWidth(samples.length);

  // Keep diagnostics during development.
  console.debug("MUI theme mode:", theme.palette.mode);
  console.debug("ECharts palette:", chartTheme.color);

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

    if (!container) {
      return;
    }

    const observer = new ResizeObserver(() => {
      chartRef.current?.getEchartsInstance().resize();
    });

    observer.observe(container);

    return () => observer.disconnect();
  }, []);

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
        // Assign color based on the full channel list.
        // Hiding other channels will not change this color.
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
            width: 1,
            opacity: 0.65,
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
            width: 1,
            opacity: 0.65,
          },
        },
      },

      // dataZoom: [
      //   // Y-axis: interactive zoom
      //   {
      //     type: "inside",
      //     yAxisIndex: 0,
      //   },

      //   // Y-axis: right-side slider
      //   {
      //     type: "slider",
      //     yAxisIndex: 0,
      //     right: 8,
      //     width: 18,
      //   },
      //   {
      //     type: "inside" as const,
      //     xAxisIndex: 0,
      //   },
      //   {
      //     type: "slider" as const,
      //     xAxisIndex: 0,
      //     bottom: 12,
      //     height: 20,

      //     textStyle: {
      //       color: chartTheme.axisTextColor,
      //     },

      //     borderColor: chartTheme.gridLineColor,
      //     fillerColor:
      //       theme.palette.mode === "dark"
      //         ? "rgba(96, 165, 250, 0.12)"
      //         : "rgba(37, 99, 235, 0.10)",
      //   },
      // ],
      dataZoom: [
        {
          type: "inside",
          xAxisIndex: 0,
          zoomOnMouseWheel: true,
          moveOnMouseMove: true,
          moveOnMouseWheel: false,
        },
      ],

      series,
    };
  }, [
    channels,
    samples,
    selectedChannelIds,
    chartTheme,
    theme.palette.mode,
  ]);

  console.debug(
    "CHART DIAGNOSTICS",
    JSON.stringify(
      {
        loadedSamples: samples.length,
        selectedChannels: selectedChannelIds,
        seriesCount: option.series.length,
        seriesColors: option.series.map((series) => ({
          name: series.name,
          color: series.lineStyle.color,
        })),
        firstSeriesData: option.series[0]?.data.slice(0, 5),
        chartWidth: containerRef.current?.clientWidth,
        chartHeight: containerRef.current?.clientHeight,
      },
      null,
      2,
    ),
  );

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
            style={{
              height: "100%",
              width: "100%",
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

function getLineWidth(visibleSampleCount: number): number {
  if (visibleSampleCount <= 100) return 2.5;
  if (visibleSampleCount <= 500) return 2;
  if (visibleSampleCount <= 2000) return 1.5;

  return 1;
}

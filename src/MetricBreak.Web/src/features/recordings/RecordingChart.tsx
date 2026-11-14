
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

  const [samples, setSamples] =
    useState<SampleRangeItem[]>([]);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

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
          response: result,
          samplesLength: result.samples?.length,
        });

        if (!cancelled) {
          setSamples(result.samples);
        }
      } catch (caught) {
        console.error("Failed to load recording samples:", caught);

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

    const observer = new ResizeObserver(() => {
      chartRef.current?.getEchartsInstance().resize();
    });

    observer.observe(container);

    return () => observer.disconnect();
  }, []);

  const option = useMemo(() => {
    const series = channels
      .filter((channel) =>
        selectedChannelIds.includes(channel.channelId),
      )
      .map((channel) => ({
        name: channel.unit
          ? `${channel.name} (${channel.unit})`
          : channel.name,
        type: "line" as const,
        showSymbol: false,
        connectNulls: false,
        data: samples.map((sample) => [
          sample.sequence,
          sample.channels[channel.channelId] ?? null,
        ]),
      }));

    return {
      animation: false,

      color: chartTheme.color,

      textStyle: {
        fontFamily: '"Source Sans Pro", Arial, sans-serif',
        color: theme.palette.text.primary,
      },

      tooltip: {
        trigger: "axis",
      },

      legend: {
        show: false,
      },

      grid: {
        left: 20,
        right: 35,
        top: 20,
        bottom: 75,
        containLabel: true,
      },

      xAxis: {
        type: "value",
        name: "Sequence",
        nameLocation: "middle",
        nameGap: 30,
        minInterval: 1,
        axisLabel: {
          hideOverlap: true,
          formatter: (value: number) =>
            Math.round(value).toLocaleString(),
        },
      },

      yAxis: {
        type: "value",
        scale: true,
        axisLabel: {
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
      },

      dataZoom: [
        {
          type: "inside",
          xAxisIndex: 0,
        },
        {
          type: "slider",
          xAxisIndex: 0,
          bottom: 12,
          height: 20,
        },
      ],

      series,
    };
  }, [
    channels,
    samples,
    selectedChannelIds,
    chartTheme,
    theme.palette.text.primary,
  ]);

  console.log(
    "CHART DIAGNOSTICS",
    JSON.stringify(
      {
        loadedSamples: samples.length,
        selectedChannels: selectedChannelIds,
        seriesCount: option.series.length,
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
        <Alert severity="error" sx={{ flexShrink: 0, mb: 1 }}>
          {error}
        </Alert>
      )}

      {sampleCount > MAX_SAMPLES && (
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ flexShrink: 0, px: 1 }}
        >
          Displaying the first {MAX_SAMPLES.toLocaleString()} of{" "}
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
            style={{
              height: "100%",
              width: "100%",
            }}
            notMerge
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

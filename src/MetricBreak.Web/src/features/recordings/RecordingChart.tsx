
import { useEffect, useMemo, useState } from "react";
import ReactECharts from "echarts-for-react";

import {
    Alert,
    Box,
    Card,
    CardContent,
    Checkbox,
    CircularProgress,
    FormControlLabel,
    Stack,
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
}

const MAX_SAMPLES = 5000;
const DEFAULT_VISIBLE_CHANNELS = 3;

export function RecordingChart({
    sessionId,
    sampleCount,
    channels,
}: RecordingChartProps) {
    const theme = useTheme();

    const chartTheme = useMemo(
        () => createChartTheme(theme.palette.mode),
        [theme.palette.mode]
    );
    
    const [samples, setSamples] =
        useState<SampleRangeItem[]>([]);

    const [selectedChannels, setSelectedChannels] =
        useState<string[]>([]);

    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    
    useEffect(() => {
        setSelectedChannels(
            channels
                .slice(0, DEFAULT_VISIBLE_CHANNELS)
                .map((channel) => channel.channelId),
        );
    }, [channels]);


    useEffect(() => {
        let cancelled = false;

        if (sampleCount <= 0) {
            return;
        }

        async function loadSamples() {
            setLoading(true);
            setError("");
            setSamples([]);

            try {
                const result = await recordingApi.getSamples(
                    sessionId,
                    0,
                    Math.min(sampleCount, MAX_SAMPLES),
                );

                if (!cancelled) {
                    setSamples(result.samples);
                }
            } catch (caught) {
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

    function toggleChannel(channelId: string) {
        setSelectedChannels((current) =>
            current.includes(channelId)
                ? current.filter((id) => id !== channelId)
                : [...current, channelId],
        );
    }

    const option = useMemo(() => {
        const series = channels
            .filter((channel) =>
                selectedChannels.includes(channel.channelId),
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
             textStyle: {
                fontFamily: '"Source Sans Pro", Arial, sans-serif',
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
                top: 25,
                bottom: 90,
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
                    bottom: 15,
                },
            ],

            series,
        };
    }, [channels, samples, selectedChannels]);

    return (
        <Card variant="outlined">
            <CardContent>
                <Stack spacing={2}>
                    <Box>
                        <Typography variant="h6">
                            Recording Visualization
                        </Typography>

                        <Typography
                            variant="body2"
                            color="text.secondary"
                        >
                            Inspect recorded signals by sample sequence.
                        </Typography>
                    </Box>

                    <Box
                        sx={{
                            display: "grid",
                            gridTemplateColumns: {
                                xs: "1fr",
                                sm: "repeat(2, minmax(0, 1fr))",
                                lg: "repeat(3, minmax(0, 1fr))",
                            },
                            gap: 1,
                        }}
                    >
                        {channels.map((channel) => (
                            <FormControlLabel
                                key={channel.channelId}
                                sx={{
                                    m: 0,
                                    minWidth: 0,
                                    "& .MuiFormControlLabel-label": {
                                        overflowWrap: "anywhere",
                                        fontSize: "0.875rem",
                                    },
                                }}
                                control={
                                    <Checkbox
                                        checked={selectedChannels.includes(
                                            channel.channelId,
                                        )}
                                        onChange={() =>
                                            toggleChannel(channel.channelId)
                                        }
                                        size="small"
                                    />
                                }
                                label={channel.name}
                            />
                        ))}
                    </Box>

                    {error && (
                        <Alert severity="error">
                            {error}
                        </Alert>
                    )}

                    {loading ? (
                        <Box
                            sx={{
                                display: "flex",
                                justifyContent: "center",
                                py: 6,
                            }}
                        >
                            <CircularProgress />
                        </Box>
                    ) : samples.length > 0 ? (
                        <ReactECharts
                            option={option}
                            style={{
                                height: 420,
                                width: "100%",
                            }}
                            notMerge
                        />
                    ) : (
                        <Typography color="text.secondary">
                            No samples available for visualization.
                        </Typography>
                    )}

                    {sampleCount > MAX_SAMPLES && (
                        <Alert severity="info">
                            Displaying the first {MAX_SAMPLES.toLocaleString()}
                            {" "}of {sampleCount.toLocaleString()} samples.
                            Windowed loading will be added in a subsequent
                            milestone.
                        </Alert>
                    )}
                </Stack>
            </CardContent>
        </Card>
    );
}

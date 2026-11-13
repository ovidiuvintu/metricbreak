
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

import { recordingApi } from "./recordingApi";

import type { ChannelMetadata } from "./channelMetadata";
import type { SampleRangeItem } from "./sampleTypes";

interface RecordingChartProps {
    sessionId: string;
    sampleCount: number;
    channels: ChannelMetadata[];
}

const MAX_SAMPLES = 5000;

export function RecordingChart({
    sessionId,
    sampleCount,
    channels,
}: RecordingChartProps) {
    const [samples, setSamples] =
        useState<SampleRangeItem[]>([]);

    const [selectedChannels, setSelectedChannels] =
        useState<string[]>([]);

    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    useEffect(() => {
        setSelectedChannels(channels.map(
            (channel) => channel.channelId,
        ));
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

            tooltip: {
                trigger: "axis",
            },

            legend: {
                top: 0,
            },

            grid: {
                left: 65,
                right: 30,
                top: 65,
                bottom: 75,
            },

            xAxis: {
                type: "value",
                name: "Sequence",
                nameLocation: "middle",
                nameGap: 30,
                minInterval: 1,
            },

            yAxis: {
                type: "value",
                scale: true,
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

                    <Stack
                        direction="row"
                        spacing={1}
                        sx={{
                            flexWrap: "wrap",
                        }}
                    >
                        {channels.map((channel) => (
                            <FormControlLabel
                                key={channel.channelId}
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
                    </Stack>

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

import { useEffect, useMemo, useState, type ReactNode } from "react";
import {
    Box,
    Button,
    Card,
    CardContent,
    Chip,
    LinearProgress,
    Stack,
    Typography,
} from "@mui/material";
import PlayArrowOutlinedIcon from "@mui/icons-material/PlayArrowOutlined";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";

import type { Breakpoint, CompositeBreakpoint } from "../recordings/breakpointApi";
import { recordingApi } from "../recordings/recordingApi";
import type { SessionState } from "../recordings/sessionTypes";
import { investigationApi } from "../investigations/investigationApi";
import { loadInvestigationSelection } from "../investigations/investigationSelectionStorage";
import type { InvestigationBookmark } from "../investigations/investigationTypes";
import { formatBookmarkTime } from "../investigations/formatBookmarkTime";
import { formatDurationBetweenNanoseconds } from "./formatDuration";

interface RecordingDebuggerDashboardProps {
    session: SessionState | null;
    channelCount: number;
    breakpoints: Breakpoint[];
    compositeBreakpoints: CompositeBreakpoint[];
    onImport: () => void;
    onResumeDebugging: () => void;
    onManageBreakpoints: () => void;
    onOpenInvestigations: () => void;
    onGoToSample: (sequence: number) => void;
}

function executionLabel(state: string): string {
    switch (state.toLowerCase()) {
        case "playing":
            return "Playing";
        case "paused":
            return "Paused";
        default:
            return "Ready";
    }
}

export function RecordingDebuggerDashboard({
    session,
    channelCount,
    breakpoints,
    compositeBreakpoints,
    onImport,
    onResumeDebugging,
    onManageBreakpoints,
    onOpenInvestigations,
    onGoToSample,
}: RecordingDebuggerDashboardProps) {
    const [durationLabel, setDurationLabel] = useState<string | null>(null);
    const [bookmarks, setBookmarks] = useState<InvestigationBookmark[]>([]);
    const [activeInvestigationName, setActiveInvestigationName] = useState<string | null>(null);

    const timestampOrigin = session?.timestampOrigin ?? "Unix";

    useEffect(() => {
        if (!session || session.sampleCount <= 1) {
            setDurationLabel(null);
            return;
        }

        let cancelled = false;

        void (async () => {
            try {
                const [start, end] = await Promise.all([
                    recordingApi.getSamples(session.sessionId, 0, 1),
                    recordingApi.getSamples(
                        session.sessionId,
                        Math.max(0, session.sampleCount - 1),
                        1,
                    ),
                ]);

                if (cancelled) return;

                const first = start.samples[0];
                const last = end.samples[0];
                if (!first || !last) return;

                setDurationLabel(
                    formatDurationBetweenNanoseconds(
                        Number(first.timestampNanoseconds),
                        Number(last.timestampNanoseconds),
                        timestampOrigin,
                    ),
                );
            } catch {
                if (!cancelled) setDurationLabel(null);
            }
        })();

        return () => { cancelled = true; };
    }, [session, timestampOrigin]);

    useEffect(() => {
        if (!session) {
            setBookmarks([]);
            setActiveInvestigationName(null);
            return;
        }

        const selection = loadInvestigationSelection(session.recordingName);
        if (!selection) {
            setBookmarks([]);
            setActiveInvestigationName(null);
            return;
        }

        let cancelled = false;

        void (async () => {
            try {
                const [investigations, bookmarkResponse] = await Promise.all([
                    investigationApi.listProjectInvestigations(selection.projectId),
                    investigationApi.listBookmarks(
                        selection.investigationId,
                        selection.projectId,
                    ),
                ]);

                if (cancelled) return;

                const investigation = investigations.investigations.find(
                    (item) => item.investigationId === selection.investigationId,
                );

                setActiveInvestigationName(investigation?.recordingName ?? session.recordingName);
                setBookmarks(bookmarkResponse.bookmarks);
            } catch {
                if (!cancelled) {
                    setBookmarks([]);
                    setActiveInvestigationName(null);
                }
            }
        })();

        return () => { cancelled = true; };
    }, [session]);

    const breakpointStats = useMemo(() => {
        const all = [
            ...breakpoints.map((item) => item.isEnabled),
            ...compositeBreakpoints.map((item) => item.isEnabled),
        ];
        const enabled = all.filter(Boolean).length;
        return { total: all.length, enabled, disabled: all.length - enabled };
    }, [breakpoints, compositeBreakpoints]);

    const latestBookmark = useMemo(() => {
        if (bookmarks.length === 0) return null;
        return [...bookmarks].sort(
            (a, b) => Date.parse(b.modifiedAtUtc) - Date.parse(a.modifiedAtUtc),
        )[0];
    }, [bookmarks]);

    if (!session) {
        return (
            <Box
                sx={{
                    display: "flex",
                    flex: 1,
                    minHeight: 0,
                    flexDirection: "column",
                    alignItems: "center",
                    justifyContent: "center",
                    gap: 1.5,
                    px: 2,
                    textAlign: "center",
                    overflow: "hidden",
                }}
            >
                <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                    Recording Dashboard
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 380 }}>
                    Import a CSV recording to see replay position, breakpoints, and bookmarks.
                </Typography>
                <Button
                    variant="contained"
                    size="small"
                    startIcon={<UploadFileIcon />}
                    onClick={onImport}
                >
                    Import CSV
                </Button>
            </Box>
        );
    }

    const sequence = session.currentSample.sequence;
    const progress = session.sampleCount > 0
        ? ((sequence + 1) / session.sampleCount) * 100
        : 0;

    const clockLabel = formatBookmarkTime(
        session.currentSample.timestampNanoseconds,
        timestampOrigin,
    );

    const timestampFormatLabel =
        timestampOrigin === "Elapsed"
            ? "Relative elapsed"
            : "ISO-8601 UTC";

    return (
        <Box
            sx={{
                flex: 1,
                minHeight: 0,
                height: "100%",
                overflow: "hidden",
                display: "flex",
                flexDirection: "column",
                gap: 1.5,
                maxWidth: 900,
                mx: "auto",
                width: "100%",
                py: 0.5,
            }}
        >
            <Box sx={{ flexShrink: 0 }}>
                <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{ letterSpacing: 1, textTransform: "uppercase" }}
                >
                    Recording Dashboard
                </Typography>
                <Box
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        gap: 1,
                        flexWrap: "wrap",
                        mt: 0.25,
                    }}
                >
                    <Typography
                        variant="h6"
                        sx={{ fontWeight: 700, lineHeight: 1.2 }}
                        noWrap
                    >
                        {session.recordingName}
                    </Typography>
                    <Chip
                        label={executionLabel(session.executionState)}
                        size="small"
                        color="info"
                        sx={{ height: 22 }}
                    />
                </Box>

                <Box
                    sx={{
                        display: "grid",
                        gridTemplateColumns: {
                            xs: "repeat(2, minmax(0, 1fr))",
                            sm: "repeat(4, minmax(0, 1fr))",
                        },
                        gap: 1,
                        mt: 1.25,
                    }}
                >
                    <MetricTile label="Samples" value={session.sampleCount.toLocaleString()} />
                    <MetricTile label="Channels" value={channelCount.toLocaleString()} />
                    <MetricTile label="Duration" value={durationLabel ?? "—"} />
                    <MetricTile label="Timestamps" value={timestampFormatLabel} />
                </Box>
            </Box>

            <Box
                sx={{
                    flex: 1,
                    minHeight: 0,
                    display: "grid",
                    gridTemplateColumns: { xs: "1fr 1fr", md: "1fr 1fr" },
                    gridTemplateRows: "1fr 1fr",
                    gap: 1.25,
                    overflow: "hidden",
                }}
            >
                <SectionCard
                    title="Replay position"
                    sx={{ display: "flex", flexDirection: "column", minHeight: 0 }}
                >
                    <Stack spacing={0.75} sx={{ flex: 1, minHeight: 0, justifyContent: "center" }}>
                        <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>
                            Sample {sequence.toLocaleString()} / {session.sampleCount.toLocaleString()}
                        </Typography>
                        <LinearProgress
                            variant="determinate"
                            value={Math.min(100, Math.max(0, progress))}
                            sx={{ height: 5, borderRadius: 1 }}
                        />
                        <Typography variant="caption" color="text.secondary" noWrap>
                            {clockLabel}
                        </Typography>
                        <Button
                            variant="contained"
                            size="small"
                            startIcon={<PlayArrowOutlinedIcon />}
                            onClick={onResumeDebugging}
                            sx={{ mt: 0.5, alignSelf: "flex-start" }}
                        >
                            Resume debugging
                        </Button>
                    </Stack>
                </SectionCard>

                <SectionCard
                    title="Breakpoints"
                    action={(
                        <Button
                            size="small"
                            sx={{ minWidth: 0, px: 0.5, fontSize: "0.72rem" }}
                            endIcon={<ChevronRightIcon fontSize="small" />}
                            onClick={onManageBreakpoints}
                        >
                            Manage
                        </Button>
                    )}
                >
                    <Stack spacing={0.25} sx={{ justifyContent: "center", flex: 1 }}>
                        <Typography variant="h5" sx={{ fontWeight: 700, lineHeight: 1.1 }}>
                            {breakpointStats.total}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                            {breakpointStats.enabled} enabled · {breakpointStats.disabled} disabled
                        </Typography>
                    </Stack>
                </SectionCard>

                <SectionCard
                    title="Investigation"
                    action={(
                        <Button
                            size="small"
                            sx={{ minWidth: 0, px: 0.5, fontSize: "0.72rem" }}
                            endIcon={<ChevronRightIcon fontSize="small" />}
                            onClick={onOpenInvestigations}
                        >
                            Open
                        </Button>
                    )}
                >
                    <Stack spacing={0.25} sx={{ justifyContent: "center", flex: 1, minWidth: 0 }}>
                        <Typography variant="h5" sx={{ fontWeight: 700, lineHeight: 1.1 }}>
                            {bookmarks.length}
                        </Typography>
                        <Typography variant="caption" color="text.secondary" noWrap>
                            Saved bookmarks
                            {activeInvestigationName ? ` · ${activeInvestigationName}` : ""}
                        </Typography>
                    </Stack>
                </SectionCard>

                <SectionCard title="Continue investigating">
                    {latestBookmark ? (
                        <Stack spacing={0.5} sx={{ justifyContent: "center", flex: 1, minWidth: 0 }}>
                            <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>
                                {latestBookmark.title?.trim() ||
                                    `Sample ${latestBookmark.sampleSequence.toLocaleString()}`}
                            </Typography>
                            <Typography variant="caption" color="text.secondary" noWrap>
                                Sample {latestBookmark.sampleSequence.toLocaleString()} ·{" "}
                                {formatBookmarkTime(
                                    latestBookmark.timestampNanoseconds,
                                    timestampOrigin,
                                )}
                            </Typography>
                            <Button
                                size="small"
                                sx={{ alignSelf: "flex-start", mt: 0.25 }}
                                onClick={() => onGoToSample(latestBookmark.sampleSequence)}
                            >
                                Go to bookmark
                            </Button>
                        </Stack>
                    ) : (
                        <Typography variant="caption" color="text.secondary" sx={{ m: "auto 0" }}>
                            No bookmarks yet. Save one from Investigations.
                        </Typography>
                    )}
                </SectionCard>
            </Box>
        </Box>
    );
}

function MetricTile({ label, value }: { label: string; value: string }) {
    return (
        <Card variant="outlined" sx={{ minWidth: 0 }}>
            <CardContent sx={{ py: 1, px: 1.25, "&:last-child": { pb: 1 } }}>
                <Typography variant="caption" color="text.secondary" noWrap sx={{ display: "block" }}>
                    {label}
                </Typography>
                <Typography
                    variant="body2"
                    sx={{ fontWeight: 700, lineHeight: 1.2 }}
                    noWrap
                    title={value}
                >
                    {value}
                </Typography>
            </CardContent>
        </Card>
    );
}

function SectionCard({
    title,
    children,
    action,
    sx,
}: {
    title: string;
    children: ReactNode;
    action?: ReactNode;
    sx?: object;
}) {
    return (
        <Card
            variant="outlined"
            sx={{
                minHeight: 0,
                minWidth: 0,
                display: "flex",
                flexDirection: "column",
                overflow: "hidden",
                ...sx,
            }}
        >
            <CardContent
                sx={{
                    py: 1.25,
                    px: 1.5,
                    flex: 1,
                    minHeight: 0,
                    display: "flex",
                    flexDirection: "column",
                    "&:last-child": { pb: 1.25 },
                }}
            >
                <Box
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        gap: 0.5,
                        mb: 0.75,
                        flexShrink: 0,
                    }}
                >
                    <Typography variant="caption" sx={{ fontWeight: 700, textTransform: "uppercase" }}>
                        {title}
                    </Typography>
                    {action}
                </Box>
                <Box sx={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column" }}>
                    {children}
                </Box>
            </CardContent>
        </Card>
    );
}
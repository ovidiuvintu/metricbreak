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
                    flexDirection: "column",
                    alignItems: "center",
                    justifyContent: "center",
                    gap: 2,
                    px: 2,
                    textAlign: "center",
                }}
            >
                <Typography variant="h6">Recording Dashboard</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 420 }}>
                    Import a CSV recording to see replay position, breakpoints, and
                    investigation bookmarks at a glance.
                </Typography>
                <Button
                    variant="contained"
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
                overflowY: "auto",
                py: 1,
            }}
        >
            <Stack spacing={2.5} sx={{ maxWidth: 720, mx: "auto", width: "100%" }}>
                <Box>
                    <Typography variant="overline" color="text.secondary">
                        Recording Dashboard
                    </Typography>
                    <Typography variant="h5" sx={{ fontWeight: 700 }}>
                        {session.recordingName}
                    </Typography>
                    <Chip
                        label={executionLabel(session.executionState)}
                        size="small"
                        color="info"
                        sx={{ mt: 1 }}
                    />
                </Box>

                <Box
                    sx={{
                        display: "grid",
                        gridTemplateColumns: "repeat(auto-fit, minmax(120px, 1fr))",
                        gap: 2,
                    }}
                >
                    <StatCard label="Samples" value={session.sampleCount.toLocaleString()} />
                    <StatCard label="Channels" value={channelCount.toLocaleString()} />
                    <StatCard label="Duration" value={durationLabel ?? "—"} />
                </Box>

                <SectionCard title="Recording overview">
                    <Typography variant="body2" color="text.secondary">
                        Timestamp format: {timestampFormatLabel}
                    </Typography>
                </SectionCard>

                <SectionCard title="Replay position">
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        Sample {sequence.toLocaleString()} / {session.sampleCount.toLocaleString()}
                    </Typography>
                    <LinearProgress
                        variant="determinate"
                        value={Math.min(100, Math.max(0, progress))}
                        sx={{ my: 1.5, height: 6, borderRadius: 1 }}
                    />
                    <Typography variant="body2" color="text.secondary">
                        {clockLabel}
                    </Typography>
                    <Button
                        variant="contained"
                        size="small"
                        startIcon={<PlayArrowOutlinedIcon />}
                        onClick={onResumeDebugging}
                        sx={{ mt: 2 }}
                    >
                        Resume debugging
                    </Button>
                </SectionCard>

                <SectionCard
                    title="Breakpoints"
                    action={(
                        <Button size="small" endIcon={<ChevronRightIcon />} onClick={onManageBreakpoints}>
                            Manage breakpoints
                        </Button>
                    )}
                >
                    <Typography variant="h4" sx={{ fontWeight: 700 }}>
                        {breakpointStats.total}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                        {breakpointStats.enabled} enabled · {breakpointStats.disabled} disabled
                    </Typography>
                </SectionCard>

                <SectionCard
                    title="Investigation"
                    action={(
                        <Button size="small" endIcon={<ChevronRightIcon />} onClick={onOpenInvestigations}>
                            Open investigation
                        </Button>
                    )}
                >
                    <Typography variant="h4" sx={{ fontWeight: 700 }}>
                        {bookmarks.length}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                        Saved bookmarks
                        {activeInvestigationName ? ` · ${activeInvestigationName}` : ""}
                    </Typography>
                </SectionCard>

                {latestBookmark && (
                    <SectionCard title="Continue investigating">
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            {latestBookmark.title?.trim() ||
                                `Sample ${latestBookmark.sampleSequence.toLocaleString()}`}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                            Sample {latestBookmark.sampleSequence.toLocaleString()} ·{" "}
                            {formatBookmarkTime(
                                latestBookmark.timestampNanoseconds,
                                timestampOrigin,
                            )}
                        </Typography>
                        <Button
                            size="small"
                            sx={{ mt: 1.5, alignSelf: "flex-start" }}
                            onClick={() => onGoToSample(latestBookmark.sampleSequence)}
                        >
                            Go to bookmark
                        </Button>
                    </SectionCard>
                )}
            </Stack>
        </Box>
    );
}

function StatCard({ label, value }: { label: string; value: string }) {
    return (
        <Card variant="outlined">
            <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
                <Typography variant="caption" color="text.secondary">
                    {label}
                </Typography>
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
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
}: {
    title: string;
    children: ReactNode;
    action?: ReactNode;
}) {
    return (
        <Card variant="outlined">
            <CardContent>
                <Box
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        gap: 1,
                        mb: 1,
                    }}
                >
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                        {title}
                    </Typography>
                    {action}
                </Box>
                {children}
            </CardContent>
        </Card>
    );
}
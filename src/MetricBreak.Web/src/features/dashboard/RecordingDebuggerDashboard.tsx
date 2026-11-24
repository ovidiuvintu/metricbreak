import { useEffect, useMemo, useState, type DragEvent, type ReactNode } from "react";
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
import { formatRecordingDuration } from "./formatDuration";
import { InvestigationsResumeList } from "../investigations/InvestigationsResumeList";

interface RecordingDebuggerDashboardProps {
    session: SessionState | null;
    channelCount: number;
    breakpoints: Breakpoint[];
    compositeBreakpoints: CompositeBreakpoint[];
    onImport: () => void;
    onQuickOpenFile: (file: File) => void;
    quickOpenBusy?: boolean;
    onSaveAsProject?: () => void;
    onResumeDebugging: () => void;
    onManageBreakpoints: () => void;
    onOpenInvestigations: () => void;
    onGoToSample: (sequence: number) => void;
    bookmarkRefreshKey?: number;
    onOpenInvestigation?: (
        investigationId: string,
        projectId: string,
    ) => Promise<void>;
    openInvestigationBusy?: boolean;
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
    onQuickOpenFile,
    quickOpenBusy = false,
    onSaveAsProject,
    onResumeDebugging,
    onManageBreakpoints,
    onOpenInvestigations,
    onGoToSample,
    bookmarkRefreshKey = 0,
    onOpenInvestigation,
    openInvestigationBusy = false,
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
                    formatRecordingDuration(
                        first.timestampNanoseconds,
                        last.timestampNanoseconds,
                        session.sampleCount,
                    ),
                );
            } catch {
                if (!cancelled) setDurationLabel(null);
            }
        })();

        return () => { cancelled = true; };
    }, [session]);

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
                const investigationsPromise = selection.projectId
                    ? investigationApi.listProjectInvestigations(selection.projectId)
                    : investigationApi.listWorkspaceInvestigations(session.recordingName);

                const [investigations, bookmarkResponse] = await Promise.all([
                    investigationsPromise,
                    investigationApi.listBookmarks(
                        selection.investigationId,
                        selection.projectId || null,
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
    }, [session, bookmarkRefreshKey]);

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
            <QuickOpenSurface busy={quickOpenBusy} onFile={onQuickOpenFile}>
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
                <Typography variant="body2" color="text.secondary" sx={{ maxWidth: 460 }}>
                    Drop a CSV here. Setup walks through channels, breakpoints, then debug or run.
                </Typography>
                <Stack direction="row" spacing={1}>
                    <Button
                        component="label"
                        variant="contained"
                        size="small"
                        startIcon={<UploadFileIcon />}
                        disabled={quickOpenBusy}
                    >
                        {quickOpenBusy ? "Opening..." : "Choose CSV"}
                        <input
                            type="file"
                            accept=".csv,text/csv"
                            hidden
                            onChange={(event) => {
                                const selected = event.target.files?.[0];
                                event.target.value = "";
                                if (selected) onQuickOpenFile(selected);
                            }}
                        />
                    </Button>
                    <Button
                        variant="outlined"
                        size="small"
                        onClick={onImport}
                        disabled={quickOpenBusy}
                    >
                        Import options
                    </Button>
                </Stack>
                {onOpenInvestigation && (
                    <Box sx={{ mt: 2, width: "100%", maxWidth: 420, textAlign: "left" }}>
                        <InvestigationsResumeList
                            busy={openInvestigationBusy}
                            onOpen={onOpenInvestigation}
                            limit={4}
                        />
                    </Box>
                )}
            </Box>
            </QuickOpenSurface>
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
        <QuickOpenSurface busy={quickOpenBusy} onFile={onQuickOpenFile}>
        <Box
            sx={{
                flex: 1,
                minHeight: 0,
                height: "100%",
                overflow: "hidden",
                display: "flex",
                flexDirection: "column",
                gap: 1,
                maxWidth: 900,
                mx: "auto",
                width: "100%",
                py: 0.25,
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
                    {onSaveAsProject && (
                        <Button
                            size="small"
                            variant="outlined"
                            onClick={onSaveAsProject}
                            sx={{ height: 22 }}
                        >
                            Save as Project
                        </Button>
                    )}
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
        </QuickOpenSurface>
    );
}

function QuickOpenSurface({
    children,
    busy,
    onFile,
}: {
    children: ReactNode;
    busy: boolean;
    onFile: (file: File) => void;
}) {
    const [dragDepth, setDragDepth] = useState(0);
    const dragging = dragDepth > 0;

    const acceptsFiles = (event: DragEvent) =>
        Array.from(event.dataTransfer?.types ?? []).includes("Files");

    const handleDragEnter = (event: DragEvent) => {
        if (!acceptsFiles(event) || busy) return;
        event.preventDefault();
        setDragDepth((depth) => depth + 1);
    };

    const handleDragOver = (event: DragEvent) => {
        if (!acceptsFiles(event) || busy) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = "copy";
    };

    const handleDragLeave = (event: DragEvent) => {
        if (!acceptsFiles(event)) return;
        event.preventDefault();
        setDragDepth((depth) => Math.max(0, depth - 1));
    };

    const handleDrop = (event: DragEvent) => {
        event.preventDefault();
        setDragDepth(0);
        if (busy) return;
        const file = event.dataTransfer.files?.[0];
        if (file) onFile(file);
    };

    return (
        <Box
            data-quick-open="true"
            onDragEnter={handleDragEnter}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            sx={{
                position: "relative",
                flex: 1,
                minHeight: 0,
                width: "100%",
                display: "flex",
                flexDirection: "column",
                outline: dragging ? "2px dashed" : "none",
                outlineColor: "primary.main",
                outlineOffset: -6,
                borderRadius: 1,
            }}
        >
            {children}
            {dragging && (
                <Box
                    sx={{
                        position: "absolute",
                        inset: 8,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        borderRadius: 1,
                        bgcolor: "action.hover",
                        pointerEvents: "none",
                        zIndex: 2,
                    }}
                >
                    <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                        Drop CSV to set up a session
                    </Typography>
                </Box>
            )}
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
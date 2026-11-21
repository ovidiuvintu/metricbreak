import { useCallback, useEffect, useState } from "react";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Divider,
    FormControl,
    IconButton,
    InputLabel,
    ListItemButton,
    MenuItem,
    Select,
    Stack,
    Tooltip,
    Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";

import { projectApi } from "../projects/projectApi";
import type { ProjectSummary } from "../projects/projectTypes";
import { investigationApi } from "./investigationApi";
import type { InvestigationBookmark, InvestigationSummary } from "./investigationTypes";
import { BookmarkEditorDialog } from "./BookmarkEditorDialog";
import { formatBookmarkTime } from "./formatBookmarkTime";
import {
    loadInvestigationSelection,
    saveInvestigationSelection,
} from "./investigationSelectionStorage";
import { tryQuickBookmark } from "./quickBookmark";

interface InvestigationPanelProps {
    sessionId: string | null;
    recordingName: string | null;
    currentSequence: number | null;
    currentTimestampNanoseconds: number | null;
    timestampOrigin: "Unix" | "Elapsed";
    navigationBusy: boolean;
    setupHint?: string | null;
    onSeek: (sequence: number) => void;
    onBookmarksChanged?: () => void;
}

export function InvestigationPanel({
    sessionId,
    recordingName,
    currentSequence,
    currentTimestampNanoseconds,
    timestampOrigin,
    navigationBusy,
    setupHint,
    onSeek,
    onBookmarksChanged,
}: InvestigationPanelProps) {
    const [projects, setProjects] = useState<ProjectSummary[]>([]);
    const [projectId, setProjectId] = useState("");
    const [investigations, setInvestigations] = useState<InvestigationSummary[]>([]);
    const [investigationId, setInvestigationId] = useState("");
    const [bookmarks, setBookmarks] = useState<InvestigationBookmark[]>([]);
    const [loading, setLoading] = useState(false);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [editorOpen, setEditorOpen] = useState(false);
    const [editingBookmark, setEditingBookmark] = useState<InvestigationBookmark | null>(null);

    const loadProjects = useCallback(async () => {
        try {
            const openProjects = await projectApi.getOpenProjects();
            setProjects(openProjects);

            if (openProjects.length === 1 && projectId === "") {
                setProjectId(openProjects[0].projectId);
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to load projects.");
        }
    }, [projectId]);

    const loadInvestigations = useCallback(async () => {
        if (!projectId) {
            setInvestigations([]);
            setInvestigationId("");
            return;
        }

        setLoading(true);
        setError(null);

        try {
            const response = await investigationApi.listProjectInvestigations(projectId);
            const items = recordingName
                ? response.investigations.filter(
                    (item) => item.recordingName === recordingName,
                )
                : response.investigations;

            setInvestigations(items);

            if (items.length === 1) {
                setInvestigationId(items[0].investigationId);
            } else if (!items.some((item) => item.investigationId === investigationId)) {
                setInvestigationId("");
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to load investigations.");
        } finally {
            setLoading(false);
        }
    }, [projectId, recordingName, investigationId]);

    const loadBookmarks = useCallback(async () => {
        if (!investigationId) {
            setBookmarks([]);
            return;
        }

        setLoading(true);
        setError(null);

        try {
            const response = await investigationApi.listBookmarks(
                investigationId,
                projectId || null,
            );
            setBookmarks(response.bookmarks);
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to load bookmarks.");
        } finally {
            setLoading(false);
        }
    }, [investigationId, projectId]);

    useEffect(() => {
        void loadProjects();
    }, [loadProjects]);

    useEffect(() => {
        if (!recordingName) return;
        const saved = loadInvestigationSelection(recordingName);
        if (saved) {
            setProjectId(saved.projectId);
            setInvestigationId(saved.investigationId);
        }
    }, [recordingName]);

    useEffect(() => {
        void loadInvestigations();
    }, [loadInvestigations]);

    useEffect(() => {
        void loadBookmarks();
    }, [loadBookmarks]);

    useEffect(() => {
        if (!recordingName || !projectId || !investigationId) return;
        saveInvestigationSelection({
            recordingName,
            projectId,
            investigationId,
        });
    }, [recordingName, projectId, investigationId]);

    const handleExport = async () => {
        if (!sessionId || !projectId) return;

        setBusy(true);
        setError(null);

        try {
            const response = await investigationApi.exportSession(sessionId, projectId);
            setInvestigationId(response.investigationId);
            await loadInvestigations();
            await loadBookmarks();
            onBookmarksChanged?.();
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to export investigation.");
        } finally {
            setBusy(false);
        }
    };

    const handleQuickBookmarkHere = async () => {
        if (!recordingName || currentSequence == null || currentTimestampNanoseconds == null) {
            return;
        }

        setBusy(true);
        setError(null);

        try {
            const result = await tryQuickBookmark({
                recordingName,
                sampleSequence: currentSequence,
                timestampNanoseconds: currentTimestampNanoseconds,
            });

            if (!result.ok) {
                setError(result.message);
                return;
            }

            await loadBookmarks();
            onBookmarksChanged?.();
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to add bookmark.");
        } finally {
            setBusy(false);
        }
    };

    const handleCreateBookmark = async (title: string, annotation: string) => {
        if (!investigationId || currentSequence == null || currentTimestampNanoseconds == null) {
            return;
        }

        setBusy(true);
        setError(null);

        try {
            await investigationApi.createBookmark(
                investigationId,
                {
                    sampleSequence: currentSequence,
                    timestampNanoseconds: currentTimestampNanoseconds,
                    title: title || null,
                    annotation: annotation || null,
                },
                projectId || null,
            );
            setEditorOpen(false);
            await loadBookmarks();
            onBookmarksChanged?.();
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to add bookmark.");
        } finally {
            setBusy(false);
        }
    };

    const handleUpdateBookmark = async (title: string, annotation: string) => {
        if (!investigationId || !editingBookmark) return;

        setBusy(true);
        setError(null);

        try {
            await investigationApi.updateBookmark(
                investigationId,
                editingBookmark.bookmarkId,
                { title: title || null, annotation: annotation || null },
                projectId || null,
            );
            setEditorOpen(false);
            setEditingBookmark(null);
            await loadBookmarks();
            onBookmarksChanged?.();
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to update bookmark.");
        } finally {
            setBusy(false);
        }
    };

    const handleDeleteBookmark = async (bookmarkId: string) => {
        if (!investigationId) return;

        setBusy(true);
        setError(null);

        try {
            await investigationApi.deleteBookmark(
                investigationId,
                bookmarkId,
                projectId || null,
            );
            await loadBookmarks();
            onBookmarksChanged?.();
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to delete bookmark.");
        } finally {
            setBusy(false);
        }
    };

    const canQuickBookmark = recordingName &&
        currentSequence != null &&
        currentTimestampNanoseconds != null;

    const canBookmark = investigationId !== "" &&
        currentSequence != null &&
        currentTimestampNanoseconds != null;

    return (
        <Stack spacing={1} sx={{ width: "100%", minWidth: 0, p: 1 }}>
            <Typography variant="subtitle2">Investigation</Typography>

            <Alert severity="info" sx={{ py: 0.25, "& .MuiAlert-message": { fontSize: "0.75rem" } }}>
                Save an investigation for this recording, bookmark 2–3 interesting samples from
                the chart or breakpoints, then resume from the Dashboard.
            </Alert>

            {setupHint && (
                <Alert severity="warning" sx={{ py: 0.25 }}>
                    {setupHint}
                </Alert>
            )}

            {error && <Alert severity="error">{error}</Alert>}

            {projects.length === 0 ? (
                <Typography variant="body2" color="text.secondary">
                    Open a project on the API host before saving bookmarks.
                </Typography>
            ) : (
                <FormControl size="small" fullWidth>
                    <InputLabel id="investigation-project-label">Project</InputLabel>
                    <Select
                        labelId="investigation-project-label"
                        label="Project"
                        value={projectId}
                        onChange={(event) => setProjectId(event.target.value)}
                    >
                        {projects.map((project) => (
                            <MenuItem key={project.projectId} value={project.projectId}>
                                {project.projectName}
                            </MenuItem>
                        ))}
                    </Select>
                </FormControl>
            )}

            <Stack direction="row" spacing={1}>
                <Button
                    size="small"
                    variant="outlined"
                    startIcon={<SaveOutlinedIcon />}
                    disabled={!sessionId || !projectId || busy}
                    onClick={() => void handleExport()}
                >
                    Save investigation
                </Button>
            </Stack>

            {projectId && (
                <FormControl size="small" fullWidth>
                    <InputLabel id="investigation-select-label">Investigation</InputLabel>
                    <Select
                        labelId="investigation-select-label"
                        label="Investigation"
                        value={investigationId}
                        onChange={(event) => setInvestigationId(event.target.value)}
                    >
                        {investigations.map((item) => (
                            <MenuItem key={item.investigationId} value={item.investigationId}>
                                {item.recordingName}
                            </MenuItem>
                        ))}
                    </Select>
                </FormControl>
            )}

            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1 }}>
                <Typography variant="subtitle2">
                    Bookmarks ({bookmarks.length})
                </Typography>
                <Stack direction="row" spacing={0.5}>
                    <Button
                        size="small"
                        variant="outlined"
                        disabled={!canQuickBookmark || busy || navigationBusy}
                        onClick={() => void handleQuickBookmarkHere()}
                    >
                        Quick bookmark
                    </Button>
                    <Button
                        size="small"
                        startIcon={<AddIcon />}
                        disabled={!canBookmark || busy || navigationBusy}
                        onClick={() => {
                            setEditingBookmark(null);
                            setEditorOpen(true);
                        }}
                    >
                        Add with note…
                    </Button>
                </Stack>
            </Box>

            {loading ? (
                <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
                    <CircularProgress size={24} />
                </Box>
            ) : !investigationId ? (
                <Typography variant="body2" color="text.secondary" sx={{ py: 1 }}>
                    Save or select an investigation to attach bookmarks to this recording.
                </Typography>
            ) : bookmarks.length === 0 ? (
                <Typography variant="body2" color="text.secondary" sx={{ py: 1 }}>
                    No bookmarks yet. Pause at an interesting sample and add one.
                </Typography>
            ) : (
                <Stack divider={<Divider flexItem />} spacing={0}>
                    {bookmarks.map((bookmark) => {
                        const label = bookmark.title?.trim() ||
                            `Sample ${bookmark.sampleSequence.toLocaleString()}`;

                        return (
                            <Box
                                key={bookmark.bookmarkId}
                                sx={{ display: "flex", alignItems: "flex-start", gap: 0.5 }}
                            >
                                <ListItemButton
                                    dense
                                    disabled={navigationBusy || busy}
                                    onClick={() => onSeek(bookmark.sampleSequence)}
                                    sx={{ flex: 1, alignItems: "flex-start", flexDirection: "column" }}
                                >
                                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                        {label}
                                    </Typography>
                                    <Typography variant="caption" color="text.secondary">
                                        #{bookmark.sampleSequence.toLocaleString()} ·{" "}
                                        {formatBookmarkTime(
                                            bookmark.timestampNanoseconds,
                                            timestampOrigin,
                                        )}
                                    </Typography>
                                    {bookmark.annotation && (
                                        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                                            {bookmark.annotation}
                                        </Typography>
                                    )}
                                </ListItemButton>
                                <Stack direction="row" sx={{ pt: 0.5, flexShrink: 0 }}>
                                    <Tooltip title="Edit bookmark">
                                        <span>
                                            <IconButton
                                                size="small"
                                                disabled={busy || navigationBusy}
                                                onClick={() => {
                                                    setEditingBookmark(bookmark);
                                                    setEditorOpen(true);
                                                }}
                                            >
                                                <EditOutlinedIcon fontSize="small" />
                                            </IconButton>
                                        </span>
                                    </Tooltip>
                                    <Tooltip title="Delete bookmark">
                                        <span>
                                            <IconButton
                                                size="small"
                                                color="error"
                                                disabled={busy || navigationBusy}
                                                onClick={() => void handleDeleteBookmark(bookmark.bookmarkId)}
                                            >
                                                <DeleteOutlineIcon fontSize="small" />
                                            </IconButton>
                                        </span>
                                    </Tooltip>
                                </Stack>
                            </Box>
                        );
                    })}
                </Stack>
            )}

            <BookmarkEditorDialog
                open={editorOpen}
                title={editingBookmark ? "Edit bookmark" : "Add bookmark"}
                initialTitle={editingBookmark?.title ?? ""}
                initialAnnotation={editingBookmark?.annotation ?? ""}
                busy={busy}
                onClose={() => {
                    if (busy) return;
                    setEditorOpen(false);
                    setEditingBookmark(null);
                }}
                onSave={(title, annotation) => {
                    if (editingBookmark) {
                        void handleUpdateBookmark(title, annotation);
                    } else {
                        void handleCreateBookmark(title, annotation);
                    }
                }}
            />
        </Stack>
    );
}
import { useCallback, useEffect, useState } from "react";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    Divider,
    FormControl,
    IconButton,
    InputLabel,
    ListItemButton,
    MenuItem,
    Select,
    Stack,
    TextField,
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
import type { InvestigationUiState } from "./investigationTypes";
import { useWorkspace } from "../../workspace/WorkspaceProvider";

interface InvestigationPanelProps {
    sessionId: string | null;
    recordingName: string | null;
    currentSequence: number | null;
    currentTimestampNanoseconds: number | null;
    timestampOrigin: "Unix" | "Elapsed";
    navigationBusy: boolean;
    setupHint?: string | null;
    selectedChannelIds: string[];
    rightAxisChannelIds: string[];
    onSeek: (sequence: number) => void;
    onBookmarksChanged?: () => void;
    onRestoreUiState?: (uiState: InvestigationUiState) => void;
    resolveExportUiState?: () => InvestigationUiState;
    isUiStateDirty?: () => boolean;
    onUiStateBaselineSaved?: (
        investigationId: string,
        uiState: InvestigationUiState,
    ) => void;
    onOpenInvestigation?: (
        investigationId: string,
        projectId: string,
    ) => Promise<void>;
}

export function InvestigationPanel({
    sessionId,
    recordingName,
    currentSequence,
    currentTimestampNanoseconds,
    timestampOrigin,
    navigationBusy,
    setupHint,
    selectedChannelIds,
    rightAxisChannelIds,
    onSeek,
    onBookmarksChanged,
    onRestoreUiState,
    resolveExportUiState,
    isUiStateDirty,
    onUiStateBaselineSaved,
    onOpenInvestigation,
}: InvestigationPanelProps) {
    const { setActiveInvestigation } = useWorkspace();
    const [projects, setProjects] = useState<ProjectSummary[]>([]);
    const [projectId, setProjectId] = useState("");
    const [investigationName, setInvestigationName] = useState("");
    const [investigationDescription, setInvestigationDescription] = useState("");
    const [investigations, setInvestigations] = useState<InvestigationSummary[]>([]);
    const [investigationId, setInvestigationId] = useState("");
    const [bookmarks, setBookmarks] = useState<InvestigationBookmark[]>([]);
    const [loading, setLoading] = useState(false);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [editorOpen, setEditorOpen] = useState(false);
    const [editingBookmark, setEditingBookmark] = useState<InvestigationBookmark | null>(null);
    const [discardDialogOpen, setDiscardDialogOpen] = useState(false);
    const [pendingInvestigationAction, setPendingInvestigationAction] = useState<
        | { type: "select"; investigationId: string }
        | { type: "open" }
        | null
    >(null);

    const applyInvestigationSelection = async (nextId: string) => {
        setInvestigationId(nextId);
        if (!nextId) return;

        const summary = investigations.find((item) => item.investigationId === nextId);
        const displayName = summary?.investigationName ?? summary?.recordingName ?? "";
        setInvestigationName(displayName);

        setActiveInvestigation({
            investigationId: nextId,
            investigationName: displayName,
            projectId: projectId || null,
        });

        try {
            const document = await investigationApi.getInvestigation(
                nextId,
                projectId || null,
            );
            if (document.uiState && onRestoreUiState) {
                onRestoreUiState({
                    selectedChannelIds: document.uiState.selectedChannelIds ?? [],
                    rightAxisChannelIds: document.uiState.rightAxisChannelIds ?? [],
                    chartViewport: document.uiState.chartViewport ?? null,
                });
            }
            if (document.uiState && onUiStateBaselineSaved) {
                onUiStateBaselineSaved(nextId, {
                    selectedChannelIds: document.uiState.selectedChannelIds ?? [],
                    rightAxisChannelIds: document.uiState.rightAxisChannelIds ?? [],
                    chartViewport: document.uiState.chartViewport ?? null,
                });
            }
            setInvestigationDescription(document.metadata?.description?.trim() ?? "");
        } catch {
            // Selection still works when detail load fails.
        }
    };

    const requestInvestigationSwitch = (
        action:
            | { type: "select"; investigationId: string }
            | { type: "open" },
    ) => {
        if (isUiStateDirty?.()) {
            setPendingInvestigationAction(action);
            setDiscardDialogOpen(true);
            return;
        }
        if (action.type === "select") {
            void applyInvestigationSelection(action.investigationId);
            return;
        }
        if (onOpenInvestigation && investigationId) {
            void onOpenInvestigation(investigationId, projectId);
        }
    };

    const loadProjects = useCallback(async () => {
        try {
            const openProjects = await projectApi.getOpenProjects();
            setProjects(openProjects);

            if (openProjects.length === 0) {
                if (projectId !== "") {
                    setProjectId("");
                }
                return;
            }

            const projectIsOpen = openProjects.some(
                (project) => project.projectId === projectId,
            );

            if (projectId !== "" && !projectIsOpen) {
                setProjectId("");
                setInvestigationId("");
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to load projects.");
        }
    }, [projectId]);

    const loadInvestigations = useCallback(async () => {
        setLoading(true);
        setError(null);

        try {
            const response = projectId
                ? await investigationApi.listProjectInvestigations(projectId)
                : await investigationApi.listWorkspaceInvestigations(recordingName);

            const items = projectId && recordingName
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
        if (!recordingName || !investigationId) return;
        saveInvestigationSelection({
            recordingName,
            projectId,
            investigationId,
        });
    }, [recordingName, projectId, investigationId]);

    const handleExport = async () => {
        if (!sessionId) return;

        setBusy(true);
        setError(null);

        try {
            const name = investigationName.trim() ||
                recordingName ||
                "Investigation";

            const uiState = resolveExportUiState?.() ?? {
                selectedChannelIds,
                rightAxisChannelIds,
            };

            const response = await investigationApi.exportSession(sessionId, {
                investigationId: investigationId || null,
                name,
                description: investigationDescription.trim() || null,
                projectId: projectId || null,
                uiState,
            });
            setInvestigationId(response.investigationId);
            setInvestigationName(name);
            setActiveInvestigation({
                investigationId: response.investigationId,
                investigationName: name,
                projectId: projectId || null,
            });
            await loadInvestigations();
            await loadBookmarks();
            onBookmarksChanged?.();
            onUiStateBaselineSaved?.(response.investigationId, uiState);
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to export investigation.");
        } finally {
            setBusy(false);
        }
    };

    const handleSelectInvestigation = (nextId: string) => {
        if (!nextId) {
            setInvestigationId("");
            return;
        }
        requestInvestigationSwitch({ type: "select", investigationId: nextId });
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
                investigationId,
                projectId,
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
                Save this debugging session as an investigation (project optional), bookmark a few
                interesting samples from the chart or breakpoints, then resume from the Dashboard.
            </Alert>

            {setupHint && (
                <Alert severity="warning" sx={{ py: 0.25 }}>
                    {setupHint}
                </Alert>
            )}

            {error && <Alert severity="error">{error}</Alert>}

            <FormControl size="small" fullWidth>
                <InputLabel id="investigation-project-label">Project</InputLabel>
                <Select
                    labelId="investigation-project-label"
                    label="Project"
                    value={projectId}
                    onChange={(event) => {
                        setProjectId(event.target.value);
                        setInvestigationId("");
                    }}
                >
                    <MenuItem value="">No project</MenuItem>
                    {projects.map((project) => (
                        <MenuItem key={project.projectId} value={project.projectId}>
                            {project.projectName}
                        </MenuItem>
                    ))}
                </Select>
            </FormControl>

            {projects.length === 0 && (
                <Typography variant="caption" color="text.secondary">
                    Investigations can be saved without a project. Use Settings → New Project
                    to create or open a project folder when you want investigations filed on disk.
                </Typography>
            )}

            <TextField
                size="small"
                label="Investigation name"
                value={investigationName}
                onChange={(event) => setInvestigationName(event.target.value)}
                placeholder={recordingName ?? "Investigation name"}
                fullWidth
            />

            <TextField
                size="small"
                label="Description / notes"
                value={investigationDescription}
                onChange={(event) => setInvestigationDescription(event.target.value)}
                placeholder="Optional summary of what you are investigating"
                fullWidth
                multiline
                minRows={2}
            />

            <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <Button
                    size="small"
                    variant="outlined"
                    startIcon={<SaveOutlinedIcon />}
                    disabled={!sessionId || busy}
                    onClick={() => void handleExport()}
                >
                    {investigationId ? "Save investigation" : "Save as investigation"}
                </Button>
                <Button
                    size="small"
                    variant="contained"
                    disabled={!investigationId || busy || navigationBusy || !onOpenInvestigation}
                    onClick={() => {
                        if (!investigationId || !onOpenInvestigation) return;
                        requestInvestigationSwitch({ type: "open" });
                    }}
                >
                    Open investigation
                </Button>
            </Stack>

            <FormControl size="small" fullWidth>
                <InputLabel id="investigation-select-label">Investigation</InputLabel>
                <Select
                    labelId="investigation-select-label"
                    label="Investigation"
                    value={
                        investigations.some((item) => item.investigationId === investigationId)
                            ? investigationId
                            : ""
                    }
                    onChange={(event) => void handleSelectInvestigation(event.target.value)}
                    displayEmpty
                    renderValue={(value) => {
                        if (!value) {
                            return investigations.length === 0
                                ? "None yet for this recording"
                                : "Select investigation";
                        }
                        const selected = investigations.find(
                            (item) => item.investigationId === value,
                        );
                        return selected?.investigationName ??
                            selected?.recordingName ??
                            value;
                    }}
                >
                    {investigations.map((item) => (
                        <MenuItem key={item.investigationId} value={item.investigationId}>
                            {item.investigationName ?? item.recordingName}
                            {!projectId ? " (no project)" : ""}
                        </MenuItem>
                    ))}
                </Select>
            </FormControl>
            {recordingName && investigations.length === 0 && !loading && (
                <Typography variant="caption" color="text.secondary">
                    No saved investigation for{" "}
                    <strong>{recordingName}</strong>. Load the recording on Recordings and click{" "}
                    <strong>Save as investigation</strong>.
                </Typography>
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

            <Dialog
                open={discardDialogOpen}
                onClose={() => {
                    setDiscardDialogOpen(false);
                    setPendingInvestigationAction(null);
                }}
            >
                <DialogTitle>Discard unsaved chart layout?</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        Channel selection or chart zoom changed since the last save.
                        Continue without saving?
                    </DialogContentText>
                </DialogContent>
                <DialogActions>
                    <Button
                        onClick={() => {
                            setDiscardDialogOpen(false);
                            setPendingInvestigationAction(null);
                        }}
                    >
                        Cancel
                    </Button>
                    <Button
                        color="warning"
                        onClick={() => {
                            const pending = pendingInvestigationAction;
                            setDiscardDialogOpen(false);
                            setPendingInvestigationAction(null);
                            if (!pending) return;
                            if (pending.type === "select") {
                                void applyInvestigationSelection(pending.investigationId);
                            } else if (onOpenInvestigation && investigationId) {
                                void onOpenInvestigation(investigationId, projectId);
                            }
                        }}
                    >
                        Discard and continue
                    </Button>
                </DialogActions>
            </Dialog>

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
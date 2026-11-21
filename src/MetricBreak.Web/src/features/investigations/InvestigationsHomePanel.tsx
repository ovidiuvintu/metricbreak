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
    IconButton,
    List,
    ListItemButton,
    ListItemText,
    Stack,
    Tooltip,
    Typography,
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import PlayArrowOutlinedIcon from "@mui/icons-material/PlayArrowOutlined";

import { investigationApi } from "./investigationApi";
import type { InvestigationSummary } from "./investigationTypes";

function sortInvestigations(items: InvestigationSummary[]) {
    return [...items].sort((a, b) => {
        const aKey = a.lastOpenedAtUtc ?? a.exportedAtUtc;
        const bKey = b.lastOpenedAtUtc ?? b.exportedAtUtc;
        return bKey.localeCompare(aKey);
    });
}

interface InvestigationsHomePanelProps {
    onOpen: (investigationId: string, projectId: string) => Promise<void>;
    busy?: boolean;
    limit?: number;
    title?: string;
    showDelete?: boolean;
    projectId?: string;
    onListChanged?: () => void;
}

export function InvestigationsHomePanel({
    onOpen,
    busy = false,
    limit,
    title = "Investigations",
    showDelete = false,
    projectId = "",
    onListChanged,
}: InvestigationsHomePanelProps) {
    const [items, setItems] = useState<InvestigationSummary[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [openingId, setOpeningId] = useState<string | null>(null);
    const [deleteTarget, setDeleteTarget] = useState<InvestigationSummary | null>(null);
    const [deleting, setDeleting] = useState(false);

    const load = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const response = projectId
                ? await investigationApi.listProjectInvestigations(projectId)
                : await investigationApi.listWorkspaceInvestigations();
            const sorted = sortInvestigations(response.investigations);
            setItems(limit ? sorted.slice(0, limit) : sorted);
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to load investigations.");
        } finally {
            setLoading(false);
        }
    }, [limit, projectId]);

    useEffect(() => {
        void load();
    }, [load]);

    const handleOpen = async (investigationId: string) => {
        setOpeningId(investigationId);
        try {
            await onOpen(investigationId, projectId);
        } finally {
            setOpeningId(null);
        }
    };

    const handleDelete = async () => {
        if (!deleteTarget) return;
        setDeleting(true);
        try {
            if (projectId) {
                await investigationApi.deleteProjectInvestigation(
                    deleteTarget.investigationId,
                    projectId,
                );
            } else {
                await investigationApi.deleteWorkspaceInvestigation(
                    deleteTarget.investigationId,
                );
            }
            setDeleteTarget(null);
            await load();
            onListChanged?.();
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to delete investigation.");
        } finally {
            setDeleting(false);
        }
    };

    if (loading) {
        return (
            <Box sx={{ display: "flex", justifyContent: "center", py: 2 }}>
                <CircularProgress size={24} />
            </Box>
        );
    }

    return (
        <Stack spacing={1}>
            <Typography variant="subtitle2">{title}</Typography>
            {error && <Alert severity="error">{error}</Alert>}

            {items.length === 0 ? (
                <Typography variant="caption" color="text.secondary">
                    No saved investigations yet. Import a recording and use Save as investigation.
                </Typography>
            ) : (
                <List disablePadding dense>
                    {items.map((item) => (
                        <ListItemButton
                            key={item.investigationId}
                            disabled={busy || openingId !== null || deleting}
                            onClick={() => void handleOpen(item.investigationId)}
                        >
                            <ListItemText
                                primary={item.investigationName ?? item.recordingName}
                                secondary={item.recordingName}
                                slotProps={{
                                    primary: { noWrap: true },
                                    secondary: { noWrap: true },
                                }}
                            />
                            {showDelete && (
                                <Tooltip title="Delete investigation">
                                    <span>
                                        <IconButton
                                            size="small"
                                            color="error"
                                            disabled={busy || deleting}
                                            onClick={(event) => {
                                                event.stopPropagation();
                                                setDeleteTarget(item);
                                            }}
                                        >
                                            <DeleteOutlineIcon fontSize="small" />
                                        </IconButton>
                                    </span>
                                </Tooltip>
                            )}
                            <Button
                                size="small"
                                startIcon={<PlayArrowOutlinedIcon />}
                                disabled={busy || openingId === item.investigationId}
                                onClick={(event) => {
                                    event.stopPropagation();
                                    void handleOpen(item.investigationId);
                                }}
                                sx={{ ml: 0.5 }}
                            >
                                Open
                            </Button>
                        </ListItemButton>
                    ))}
                </List>
            )}

            <Dialog open={deleteTarget !== null} onClose={() => !deleting && setDeleteTarget(null)}>
                <DialogTitle>Delete investigation?</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        {deleteTarget
                            ? projectId
                                ? `Remove "${deleteTarget.investigationName ?? deleteTarget.recordingName}" from this project. This cannot be undone.`
                                : `Remove "${deleteTarget.investigationName ?? deleteTarget.recordingName}" from the workspace store. This cannot be undone.`
                            : ""}
                    </DialogContentText>
                </DialogContent>
                <DialogActions>
                    <Button disabled={deleting} onClick={() => setDeleteTarget(null)}>
                        Cancel
                    </Button>
                    <Button color="error" disabled={deleting} onClick={() => void handleDelete()}>
                        Delete
                    </Button>
                </DialogActions>
            </Dialog>
        </Stack>
    );
}
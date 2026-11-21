import { useCallback, useEffect, useState } from "react";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    List,
    ListItemButton,
    ListItemText,
    Stack,
    Typography,
} from "@mui/material";
import PlayArrowOutlinedIcon from "@mui/icons-material/PlayArrowOutlined";

import { investigationApi } from "./investigationApi";
import type { InvestigationSummary } from "./investigationTypes";

interface InvestigationsResumeListProps {
    onOpen: (investigationId: string, projectId: string) => Promise<void>;
    busy?: boolean;
    limit?: number;
}

export function InvestigationsResumeList({
    onOpen,
    busy = false,
    limit = 6,
}: InvestigationsResumeListProps) {
    const [items, setItems] = useState<InvestigationSummary[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [openingId, setOpeningId] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const response = await investigationApi.listWorkspaceInvestigations();
            const sorted = [...response.investigations].sort((a, b) =>
                b.exportedAtUtc.localeCompare(a.exportedAtUtc));
            setItems(sorted.slice(0, limit));
        } catch (err) {
            setError(err instanceof Error ? err.message : "Unable to load investigations.");
        } finally {
            setLoading(false);
        }
    }, [limit]);

    useEffect(() => {
        void load();
    }, [load]);

    const handleOpen = async (investigationId: string) => {
        setOpeningId(investigationId);
        try {
            await onOpen(investigationId, "");
        } finally {
            setOpeningId(null);
        }
    };

    if (loading) {
        return (
            <Box sx={{ display: "flex", justifyContent: "center", py: 2 }}>
                <CircularProgress size={24} />
            </Box>
        );
    }

    if (error) {
        return <Alert severity="error">{error}</Alert>;
    }

    if (items.length === 0) {
        return (
            <Typography variant="caption" color="text.secondary">
                No saved investigations yet. Import a recording and use Save as investigation.
            </Typography>
        );
    }

    return (
        <Stack spacing={1}>
            <Typography variant="subtitle2">Recent investigations</Typography>
            <List disablePadding dense>
                {items.map((item) => (
                    <ListItemButton
                        key={item.investigationId}
                        disabled={busy || openingId !== null}
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
                        <Button
                            size="small"
                            startIcon={<PlayArrowOutlinedIcon />}
                            disabled={busy || openingId === item.investigationId}
                            onClick={(event) => {
                                event.stopPropagation();
                                void handleOpen(item.investigationId);
                            }}
                        >
                            Open
                        </Button>
                    </ListItemButton>
                ))}
            </List>
        </Stack>
    );
}
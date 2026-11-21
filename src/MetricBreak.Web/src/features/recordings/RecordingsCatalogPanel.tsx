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
import UploadFileIcon from "@mui/icons-material/UploadFile";

import { recordingCatalogApi, type RecordingCatalogItem } from "./recordingCatalogApi";
import type { CreateSessionResponse } from "./recordingTypes";

interface RecordingsCatalogPanelProps {
  onImport: () => void;
  onOpened: (response: CreateSessionResponse) => void;
  busy?: boolean;
}

export function RecordingsCatalogPanel({
  onImport,
  onOpened,
  busy = false,
}: RecordingsCatalogPanelProps) {
  const [items, setItems] = useState<RecordingCatalogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [openingId, setOpeningId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await recordingCatalogApi.list();
      setItems(response.recordings);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load recordings.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const handleOpen = async (recordingId: string) => {
    setOpeningId(recordingId);
    setError(null);
    try {
      const response = await recordingCatalogApi.open(recordingId);
      onOpened(response);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to open recording.");
    } finally {
      setOpeningId(null);
    }
  };

  return (
    <Stack spacing={2} sx={{ maxWidth: 640, p: 2 }}>
      <Typography variant="h6">Recordings</Typography>
      <Typography variant="body2" color="text.secondary">
        Imported recordings are stored locally. Open one to debug or import a new CSV.
      </Typography>

      <Button
        variant="contained"
        startIcon={<UploadFileIcon />}
        onClick={onImport}
        disabled={busy}
      >
        Import recording
      </Button>

      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
          <CircularProgress size={28} />
        </Box>
      ) : items.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          No recordings in the catalog yet. Import a CSV to get started.
        </Typography>
      ) : (
        <List disablePadding>
          {items.map((item) => (
            <ListItemButton
              key={item.recordingId}
              disabled={busy || openingId !== null}
              onClick={() => void handleOpen(item.recordingId)}
            >
              <ListItemText
                primary={item.name}
                secondary={`${item.sampleCount.toLocaleString()} samples · ${item.sourceName}`}
              />
              {openingId === item.recordingId && (
                <CircularProgress size={20} sx={{ ml: 1 }} />
              )}
            </ListItemButton>
          ))}
        </List>
      )}
    </Stack>
  );
}
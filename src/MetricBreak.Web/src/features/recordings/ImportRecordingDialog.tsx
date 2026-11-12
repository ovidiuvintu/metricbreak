
import { useState } from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import { recordingApi } from "./recordingApi";
import type { CreateSessionResponse } from "./recordingTypes";

interface Props {
  open: boolean;
  onClose: () => void;
  onImported: (session: CreateSessionResponse) => void;
}

export function ImportRecordingDialog({
  open,
  onClose,
  onImported,
}: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  function close() {
    if (busy) return;
    setFile(null);
    setError("");
    onClose();
  }

  async function submit() {
    if (!file) {
      setError("Select a CSV file.");
      return;
    }

    setBusy(true);
    setError("");

    try {
      const session = await recordingApi.importCsv(file);
      setFile(null);
      onImported(session);
      onClose();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to import recording.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open={open} onClose={close} fullWidth maxWidth="sm">
      <DialogTitle>Import CSV Recording</DialogTitle>

      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}

          <Button
            component="label"
            variant="outlined"
            disabled={busy}
          >
            Select CSV File
            <input
              type="file"
              accept=".csv,text/csv"
              hidden
              onChange={(event) => {
                setFile(event.target.files?.[0] ?? null);
                setError("");
              }}
            />
          </Button>

          {file && (
            <Typography variant="body2">
              Selected: {file.name}
            </Typography>
          )}
        </Stack>
      </DialogContent>

      <DialogActions>
        <Button onClick={close} disabled={busy}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={submit}
          disabled={!file || busy}
        >
          {busy ? "Importing..." : "Import Recording"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

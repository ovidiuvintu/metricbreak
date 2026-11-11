
import { useState } from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
} from "@mui/material";
import { projectApi } from "./projectApi";

interface Props {
  open: boolean;
  onClose: () => void;
  onOpened: () => Promise<void>;
}

export function OpenProjectDialog({
  open,
  onClose,
  onOpened,
}: Props) {
  const [rootPath, setRootPath] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  function close() {
    if (busy) return;
    setError("");
    onClose();
  }

  async function submit() {
    if (!rootPath.trim()) {
      setError("Enter an existing project directory.");
      return;
    }

    setBusy(true);
    setError("");

    try {
      await projectApi.openProject({ rootPath: rootPath.trim() });
      setRootPath("");
      await onOpened();
      onClose();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unable to open project.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open={open} onClose={close} fullWidth maxWidth="sm">
      <DialogTitle>Open Existing Project</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField
            label="Project Root Directory"
            placeholder="C:\MetricBreak\Projects\MyProject"
            value={rootPath}
            onChange={(e) => setRootPath(e.target.value)}
            helperText="Enter the existing project directory on the API host."
            disabled={busy}
            fullWidth
            autoFocus
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={close} disabled={busy}>Cancel</Button>
        <Button
          variant="contained"
          onClick={submit}
          disabled={busy}
        >
          {busy ? "Opening..." : "Open Project"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
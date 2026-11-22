import { useEffect, useState } from "react";
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

interface SaveAsProjectDialogProps {
  open: boolean;
  defaultName: string;
  busy: boolean;
  error: string;
  onClose: () => void;
  onSave: (projectName: string, rootPath: string) => void;
}

export function SaveAsProjectDialog({
  open,
  defaultName,
  busy,
  error,
  onClose,
  onSave,
}: SaveAsProjectDialogProps) {
  const [name, setName] = useState(defaultName);
  const [rootPath, setRootPath] = useState("");

  useEffect(() => {
    if (!open) return;
    setName(defaultName);
    setRootPath("");
  }, [open, defaultName]);

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Save as Project</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField
            label="Project name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            disabled={busy}
            fullWidth
            autoFocus
          />
          <TextField
            label="Root directory"
            placeholder="C:\MetricBreak\Projects\MyProject"
            value={rootPath}
            onChange={(event) => setRootPath(event.target.value)}
            helperText="Folder on the machine running the MetricBreak API. The current session is saved into this project."
            disabled={busy}
            fullWidth
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>
          Cancel
        </Button>
        <Button
          variant="contained"
          disabled={busy || !name.trim() || !rootPath.trim()}
          onClick={() => onSave(name.trim(), rootPath.trim())}
        >
          {busy ? "Saving..." : "Save as Project"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

import { useEffect, useRef, useState } from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import {
  recordingApi,
  TIMESTAMP_FORMATS,
  type TimestampPreview,
} from "./recordingApi";
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
  const [preview, setPreview] = useState<TimestampPreview | null>(null);
  const [columnIndex, setColumnIndex] = useState<number | "">("");
  const [format, setFormat] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [phase, setPhase] = useState<"idle" | "preview" | "import">("idle");
  const requestId = useRef(0);

  useEffect(() => {
    if (!open) {
      setFile(null);
      setPreview(null);
      setColumnIndex("");
      setFormat("");
      setError("");
    }
  }, [open]);

  function close() {
    if (busy) return;
    onClose();
  }

  async function loadPreview(
    selected: File,
    mapping?: { columnIndex: number; format: string },
  ) {
    const id = ++requestId.current;
    setBusy(true);
    setPhase("preview");
    setError("");
    try {
      const next = await recordingApi.previewTimestamp(selected, mapping);
      if (id !== requestId.current) return;
      setPreview(next);
      if (!mapping) {
        setColumnIndex(next.suggestedColumnIndex);
        setFormat(next.suggestedFormat);
      }
    } catch (err) {
      if (id !== requestId.current) return;
      if (!mapping) setPreview(null);
      setError(
        err instanceof Error
          ? err.message
          : "Unable to read the timestamp column.",
      );
    } finally {
      if (id === requestId.current) {
        setBusy(false);
        setPhase("idle");
      }
    }
  }

  async function submit() {
    if (!file || columnIndex === "" || !format) {
      setError("Select a CSV file.");
      return;
    }

    setBusy(true);
    setPhase("import");
    setError("");

    try {
      const session = await recordingApi.importCsv(file, {
        columnIndex,
        format,
      });
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
      setPhase("idle");
    }
  }

  return (
    <Dialog open={open} onClose={close} fullWidth maxWidth="sm">
      <DialogTitle>Import CSV Recording</DialogTitle>

      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}
          {preview?.ambiguous && (
            <Alert severity="info">
              Several timestamp mappings fit this file. Confirm the column and format.
            </Alert>
          )}
          {preview?.note && (
            <Alert severity="warning">{preview.note}</Alert>
          )}

          <Button component="label" variant="outlined" disabled={busy}>
            Select CSV File
            <input
              type="file"
              accept=".csv,text/csv"
              hidden
              onChange={(event) => {
                const selected = event.target.files?.[0] ?? null;
                setFile(selected);
                setPreview(null);
                setColumnIndex("");
                setFormat("");
                setError("");
                if (selected) void loadPreview(selected);
              }}
            />
          </Button>

          {file && (
            <Typography variant="body2">
              Selected: {file.name}
            </Typography>
          )}

          {preview && columnIndex !== "" && (
            <>
              <FormControl size="small" fullWidth disabled={busy}>
                <InputLabel id="timestamp-column-label">Timestamp column</InputLabel>
                <Select
                  labelId="timestamp-column-label"
                  label="Timestamp column"
                  value={columnIndex}
                  onChange={(event) => {
                    const next = Number(event.target.value);
                    setColumnIndex(next);
                    if (file && format) {
                      void loadPreview(file, { columnIndex: next, format });
                    }
                  }}
                >
                  {preview.columns.map((column) => (
                    <MenuItem key={column.index} value={column.index}>
                      {column.name}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <FormControl size="small" fullWidth disabled={busy}>
                <InputLabel id="timestamp-format-label">Timestamp format</InputLabel>
                <Select
                  labelId="timestamp-format-label"
                  label="Timestamp format"
                  value={format}
                  onChange={(event) => {
                    const next = event.target.value;
                    setFormat(next);
                    if (file) {
                      void loadPreview(file, { columnIndex, format: next });
                    }
                  }}
                >
                  {TIMESTAMP_FORMATS.map((item) => (
                    <MenuItem key={item.value} value={item.value}>
                      {item.label}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <Stack spacing={0.5}>
                <Typography variant="caption" color="text.secondary">
                  Normalized preview
                </Typography>
                {preview.preview.map((row) => (
                  <Typography key={row.sampleIndex} variant="body2">
                    {row.raw} → {row.normalized}
                  </Typography>
                ))}
              </Stack>
            </>
          )}
        </Stack>
      </DialogContent>

      <DialogActions>
        <Button onClick={close} disabled={busy}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={() => void submit()}
          disabled={!file || !preview || columnIndex === "" || !format || busy || error !== ""}
        >
          {phase === "preview" ? "Reading..." : phase === "import" ? "Importing..." : "Import Recording"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

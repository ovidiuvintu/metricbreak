import { useEffect, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Step,
  StepLabel,
  Stepper,
  Typography,
} from "@mui/material";
import BugReportOutlinedIcon from "@mui/icons-material/BugReportOutlined";
import PlayArrowOutlinedIcon from "@mui/icons-material/PlayArrowOutlined";
import UploadFileIcon from "@mui/icons-material/UploadFile";

import { ChannelSelectionPanel } from "./ChannelSelectionPanel";
import { BreakpointPanel } from "./BreakpointPanel";
import {
  isCsvFile,
  recordingApi,
  TIMESTAMP_FORMATS,
  type TimestampPreview,
} from "./recordingApi";
import type { CreateSessionResponse } from "./recordingTypes";
import type { ChannelMetadata } from "./channelMetadata";
import type { Breakpoint, CompositeBreakpoint } from "./breakpointApi";

const steps = ["Channels", "Breakpoints", "Debug or run"] as const;

type Phase = "pick" | "reading" | "timestamp" | "importing" | "setup" | "failed";

export type CsvWizardFinish = "debug" | "run";

interface CsvOpenWizardProps {
  open: boolean;
  initialFile: File | null;
  /** When true, always confirm the timestamp column before the three setup steps. */
  confirmTimestamp: boolean;
  sessionReady: boolean;
  channels: ChannelMetadata[];
  selectedChannelIds: string[];
  onSelectionChange: (ids: string[]) => void;
  breakpoints: Breakpoint[];
  compositeBreakpoints: CompositeBreakpoint[];
  breakpointBusy: boolean;
  breakpointError: string | null;
  onAddBreakpoint: () => void;
  onAddComposite: () => void;
  onSetBreakpointEnabled: (breakpointId: string, isEnabled: boolean) => void;
  onDeleteBreakpoint: (breakpointId: string) => void;
  onImported: (session: CreateSessionResponse) => Promise<boolean>;
  onClose: (imported: boolean) => void;
  onFinish: (mode: CsvWizardFinish) => void;
}

export function CsvOpenWizard({
  open,
  initialFile,
  confirmTimestamp,
  sessionReady,
  channels,
  selectedChannelIds,
  onSelectionChange,
  breakpoints,
  compositeBreakpoints,
  breakpointBusy,
  breakpointError,
  onAddBreakpoint,
  onAddComposite,
  onSetBreakpointEnabled,
  onDeleteBreakpoint,
  onImported,
  onClose,
  onFinish,
}: CsvOpenWizardProps) {
  const [file, setFile] = useState<File | null>(null);
  const [phase, setPhase] = useState<Phase>("pick");
  const [step, setStep] = useState(0);
  const [preview, setPreview] = useState<TimestampPreview | null>(null);
  const [columnIndex, setColumnIndex] = useState<number | "">("");
  const [format, setFormat] = useState("");
  const [error, setError] = useState("");
  const [created, setCreated] = useState<CreateSessionResponse | null>(null);
  const [seenOpen, setSeenOpen] = useState(open);
  const [launchId, setLaunchId] = useState(0);

  const requestRef = useRef(0);
  const importingRef = useRef(false);
  const onImportedRef = useRef(onImported);

  useEffect(() => {
    onImportedRef.current = onImported;
  }, [onImported]);

  if (open !== seenOpen) {
    setSeenOpen(open);
    if (open) {
      setFile(initialFile);
      setPhase(initialFile ? "reading" : "pick");
      setStep(0);
      setPreview(null);
      setColumnIndex("");
      setFormat("");
      setError("");
      setCreated(null);
      if (initialFile) setLaunchId((id) => id + 1);
    }
  }

  const stillCurrent = (request: number) => request === requestRef.current;

  async function commitImport(
    selected: File,
    mapping: { columnIndex: number; format: string },
    request: number,
  ) {
    if (importingRef.current || !stillCurrent(request)) return;
    importingRef.current = true;
    setPhase("importing");
    setError("");
    try {
      const session = await recordingApi.importCsv(selected, mapping);
      if (!stillCurrent(request)) return;
      const loaded = await onImportedRef.current(session);
      if (!stillCurrent(request)) return;
      if (!loaded) {
        setError("The file was imported, but the session could not be opened.");
        setPhase("failed");
        return;
      }
      setCreated(session);
      setPhase("setup");
      setStep(0);
    } catch (err) {
      if (!stillCurrent(request)) return;
      setError(err instanceof Error ? err.message : "Unable to import recording.");
      setPhase("timestamp");
    } finally {
      importingRef.current = false;
    }
  }

  async function readFile(selected: File, request: number, confirm: boolean) {
    let nextPreview: TimestampPreview | null = null;
    try {
      nextPreview = await recordingApi.previewTimestamp(selected);
      if (!stillCurrent(request)) return;
      setPreview(nextPreview);
      setColumnIndex(nextPreview.suggestedColumnIndex);
      setFormat(nextPreview.suggestedFormat);

      const detected = !nextPreview.ambiguous
        && nextPreview.suggestedFormat.trim().length > 0
        && Number.isInteger(nextPreview.suggestedColumnIndex);

      if (detected && !confirm) {
        await commitImport(
          selected,
          {
            columnIndex: nextPreview.suggestedColumnIndex,
            format: nextPreview.suggestedFormat,
          },
          request,
        );
        return;
      }

      setPhase("timestamp");
    } catch (err) {
      if (!stillCurrent(request)) return;
      setError(err instanceof Error ? err.message : "Unable to read the CSV.");
      setPhase(nextPreview ? "timestamp" : "failed");
    }
  }

  async function reloadPreview(mapping: { columnIndex: number; format: string }) {
    if (!file) return;
    const request = requestRef.current;
    setPhase("reading");
    setError("");
    try {
      const next = await recordingApi.previewTimestamp(file, mapping);
      if (!stillCurrent(request)) return;
      setPreview(next);
      setPhase("timestamp");
    } catch (err) {
      if (!stillCurrent(request)) return;
      setError(err instanceof Error ? err.message : "Unable to read the timestamp column.");
      setPhase("timestamp");
    }
  }

  useEffect(() => {
    if (!open || !initialFile || launchId === 0) return;
    const request = ++requestRef.current;
    void readFile(initialFile, request, confirmTimestamp);
    // readFile is recreated each render and reads the latest setters.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, initialFile, launchId, confirmTimestamp]);

  const busy = phase === "reading" || phase === "importing";
  const imported = phase === "setup" || created !== null;
  const breakpointCount = breakpoints.length + compositeBreakpoints.length;
  const canContinueChannels = channels.length === 0 || selectedChannelIds.length > 0;

  function requestClose() {
    if (busy) return;
    requestRef.current += 1;
    onClose(imported);
  }

  function chooseFile(selected: File | null) {
    if (!selected) return;
    if (!isCsvFile(selected)) {
      setFile(selected);
      setError("Choose a .csv file.");
      setPhase("failed");
      return;
    }
    const request = ++requestRef.current;
    setFile(selected);
    setPhase("reading");
    setError("");
    setPreview(null);
    setCreated(null);
    setStep(0);
    void readFile(selected, request, confirmTimestamp);
  }

  const title = file ? file.name : "Open a CSV";

  return (
    <Dialog
      open={open}
      onClose={(_, reason) => {
        if (reason === "backdropClick" || busy) return;
        requestClose();
      }}
      fullWidth
      maxWidth="md"
    >
      <DialogTitle sx={{ pb: 1 }}>
        <Typography variant="h6" component="div" noWrap title={title}>
          {title}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Set up the recording, then debug it or run it.
        </Typography>
      </DialogTitle>

      <DialogContent
        sx={{
          display: "flex",
          flexDirection: "column",
          gap: 2,
        }}
      >
        <Stepper activeStep={phase === "setup" ? step : -1}>
          {steps.map((label) => (
            <Step key={label}>
              <StepLabel>{label}</StepLabel>
            </Step>
          ))}
        </Stepper>

        {error && <Alert severity="error">{error}</Alert>}

        {phase === "pick" && (
          <Stack spacing={1.5} sx={{ alignItems: "flex-start", pt: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Choose a CSV. You will pick channels, add breakpoints, then debug or run.
            </Typography>
            <Button component="label" variant="contained" startIcon={<UploadFileIcon />}>
              Choose CSV
              <input
                type="file"
                accept=".csv,text/csv"
                hidden
                onChange={(event) => {
                  const selected = event.target.files?.[0] ?? null;
                  event.target.value = "";
                  chooseFile(selected);
                }}
              />
            </Button>
          </Stack>
        )}

        {(phase === "reading" || phase === "importing") && (
          <Stack spacing={1.5} sx={{ alignItems: "center", justifyContent: "center", flex: 1 }}>
            <CircularProgress size={32} />
            <Typography variant="body2" color="text.secondary">
              {phase === "importing" ? "Importing the recording…" : "Reading the CSV…"}
            </Typography>
          </Stack>
        )}

        {phase === "failed" && (
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Button
              variant="contained"
              onClick={() => {
                if (!file) return;
                chooseFile(file);
              }}
              disabled={!file || !isCsvFile(file)}
            >
              Try again
            </Button>
            <Button component="label" variant="outlined">
              Choose another CSV
              <input
                type="file"
                accept=".csv,text/csv"
                hidden
                onChange={(event) => {
                  const selected = event.target.files?.[0] ?? null;
                  event.target.value = "";
                  chooseFile(selected);
                }}
              />
            </Button>
          </Stack>
        )}

        {phase === "timestamp" && preview && columnIndex !== "" && (
          <Stack spacing={2}>
            {preview.ambiguous && (
              <Alert severity="info">
                Several timestamp mappings fit this file. Confirm the column and format.
              </Alert>
            )}
            {preview.note && <Alert severity="warning">{preview.note}</Alert>}
            {!preview.ambiguous && !preview.note && (
              <Typography variant="body2" color="text.secondary">
                Confirm the timestamp column, then continue to channel selection.
              </Typography>
            )}

            <FormControl size="small" fullWidth disabled={busy}>
              <InputLabel id="wizard-timestamp-column-label">Timestamp column</InputLabel>
              <Select
                labelId="wizard-timestamp-column-label"
                label="Timestamp column"
                value={columnIndex}
                onChange={(event) => {
                  const next = Number(event.target.value);
                  setColumnIndex(next);
                  if (format) void reloadPreview({ columnIndex: next, format });
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
              <InputLabel id="wizard-timestamp-format-label">Timestamp format</InputLabel>
              <Select
                labelId="wizard-timestamp-format-label"
                label="Timestamp format"
                value={format}
                onChange={(event) => {
                  const next = event.target.value;
                  setFormat(next);
                  void reloadPreview({ columnIndex, format: next });
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
          </Stack>
        )}

        {phase === "setup" && !sessionReady && (
          <Stack spacing={1.5} sx={{ alignItems: "center", justifyContent: "center", flex: 1 }}>
            <CircularProgress size={32} />
            <Typography variant="body2" color="text.secondary">
              Loading channels…
            </Typography>
          </Stack>
        )}

        {phase === "setup" && sessionReady && step === 0 && (
          <Stack spacing={1} sx={{ flex: 1, minHeight: 0 }}>
            <Typography variant="body2" color="text.secondary">
              Select the channels to display.
              {created
                ? ` This file has ${created.sampleCount.toLocaleString()} samples and ${channels.length} channels.`
                : ""}
            </Typography>
            <Box sx={{ height: 340, minHeight: 0 }}>
              <ChannelSelectionPanel
                channels={channels}
                selectedChannelIds={selectedChannelIds}
                onSelectionChange={onSelectionChange}
                showAxisControls={false}
              />
            </Box>
          </Stack>
        )}

        {phase === "setup" && sessionReady && step === 1 && (
          <Stack spacing={1} sx={{ flex: 1, minHeight: 0 }}>
            <Typography variant="body2" color="text.secondary">
              Add breakpoints if playback should stop on a value, a change, or missing data.
              This step can be skipped.
            </Typography>
            <Box sx={{ height: 340, overflow: "auto" }}>
              <BreakpointPanel
                breakpoints={breakpoints}
                compositeBreakpoints={compositeBreakpoints}
                channels={channels}
                loading={false}
                busy={breakpointBusy}
                error={breakpointError}
                onAdd={onAddBreakpoint}
                onAddComposite={onAddComposite}
                onSetEnabled={onSetBreakpointEnabled}
                onDelete={onDeleteBreakpoint}
                emptyMessage="No breakpoints yet. Add one, or continue and add them later."
              />
            </Box>
          </Stack>
        )}

        {phase === "setup" && sessionReady && step === 2 && (
          <Stack spacing={1.5}>
            <Typography variant="body2" color="text.secondary">
              {selectedChannelIds.length} {selectedChannelIds.length === 1 ? "channel" : "channels"} selected.
              {" "}
              {breakpointCount === 0
                ? "No breakpoints yet, so a run plays straight through."
                : `${breakpointCount} ${breakpointCount === 1 ? "breakpoint" : "breakpoints"} will pause a run.`}
            </Typography>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
              <Button
                variant="outlined"
                onClick={() => onFinish("debug")}
                disabled={breakpointBusy}
                sx={{
                  flex: 1,
                  justifyContent: "flex-start",
                  alignItems: "flex-start",
                  textAlign: "left",
                  py: 2,
                  px: 2,
                }}
              >
                <Stack spacing={0.75}>
                  <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                    <BugReportOutlinedIcon color="primary" />
                    <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                      Debug
                    </Typography>
                  </Stack>
                  <Typography variant="body2" color="text.secondary">
                    Open the recording paused. Step through samples and stop on breakpoints yourself.
                  </Typography>
                </Stack>
              </Button>
              <Button
                variant="outlined"
                onClick={() => onFinish("run")}
                disabled={breakpointBusy}
                sx={{
                  flex: 1,
                  justifyContent: "flex-start",
                  alignItems: "flex-start",
                  textAlign: "left",
                  py: 2,
                  px: 2,
                }}
              >
                <Stack spacing={0.75}>
                  <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                    <PlayArrowOutlinedIcon color="primary" />
                    <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                      Run
                    </Typography>
                  </Stack>
                  <Typography variant="body2" color="text.secondary">
                    Play from the start. Playback pauses when a breakpoint hits.
                  </Typography>
                </Stack>
              </Button>
            </Stack>
          </Stack>
        )}
      </DialogContent>

      <DialogActions>
        <Button onClick={requestClose} disabled={busy}>
          {imported ? "Close" : "Cancel"}
        </Button>
        {phase === "timestamp" && (
          <Button
            variant="contained"
            disabled={!file || !preview || columnIndex === "" || !format || error !== ""}
            onClick={() => {
              if (!file || columnIndex === "" || !format) return;
              void commitImport(file, { columnIndex, format }, requestRef.current);
            }}
          >
            Continue
          </Button>
        )}
        {phase === "setup" && sessionReady && step > 0 && (
          <Button onClick={() => setStep((current) => current - 1)} disabled={breakpointBusy}>
            Back
          </Button>
        )}
        {phase === "setup" && sessionReady && step < 2 && (
          <Button
            variant="contained"
            disabled={step === 0 && !canContinueChannels}
            onClick={() => setStep((current) => current + 1)}
          >
            Continue
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}

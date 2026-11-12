
import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFile";

import { recordingApi } from "./recordingApi";
import { ImportRecordingDialog } from "./ImportRecordingDialog";
import { RecordingOverview } from "./RecordingOverview";
import { ChannelList } from "./ChannelList";

import type { CreateSessionResponse } from "./recordingTypes";
import type { SessionState } from "./sessionTypes";

import type { ChannelMetadata } from "./channelMetadata";


export function RecordingDashboard() {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [session, setSession] =
    useState<CreateSessionResponse | null>(null);

  const [sessionState, setSessionState] =
    useState<SessionState | null>(null);

  // Correct: hook inside the component
  const [channels, setChannels] =
    useState<ChannelMetadata[]>([]);

  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState("");
  const [importSuccess, setImportSuccess] = useState(false);
  // Retrieve the session state after a successful CSV import.
  useEffect(() => {
    if (!session) {
      setSessionState(null);
      return;
    }

    let cancelled = false;

   
  Promise.all([
    recordingApi.getSession(session.sessionId),
    recordingApi.getChannels(session.sessionId),
  ])
    .then(([result, metadata]) => {
      if (cancelled) return;

      setSessionState(result);
      setChannels(metadata);
      setLoadError("");
    })
    .catch((error: unknown) => {
      if (cancelled) return;

      setLoadError(
        error instanceof Error
          ? error.message
          : "Unable to load recording session.",
      );
    })
    .finally(() => {
      if (!cancelled) {
        setLoading(false);
      }
    });

  return () => {
    cancelled = true;
  };
}, [session]);


  // Called when the CSV import completes successfully.
  function handleImported(importedSession: CreateSessionResponse) {
    setSession(importedSession);
    setSessionState(null);
    setLoadError("");
    setImportSuccess(true);
    setLoading(true);
  }

  // Manually reload the current session state.
  async function refreshSession() {
    if (!session) return;

    setLoading(true);
    setLoadError("");

    try {
      const result = await recordingApi.getSession(
        session.sessionId,
      );

      setSessionState(result);
    } catch (error) {
      setLoadError(
        error instanceof Error
          ? error.message
          : "Unable to refresh recording session.",
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <Stack spacing={3}>
      {/* Page header */}
      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: 2,
        }}
      >
        <Box>
          <Typography variant="h4">
            Recordings
          </Typography>

          <Typography color="text.secondary">
            Import and inspect recorded time-series data.
          </Typography>
        </Box>

        <Stack direction="row" spacing={1}>
          <Button
            variant="outlined"
            disabled={!session || loading}
            onClick={() => void refreshSession()}
          >
            Refresh
          </Button>

          <Button
            variant="contained"
            startIcon={<UploadFileIcon />}
            onClick={() => setDialogOpen(true)}
          >
            Import CSV
          </Button>
        </Stack>
      </Box>

      {/* Import confirmation */}
      {importSuccess && session && (
        <Alert
          severity="success"
          onClose={() => setImportSuccess(false)}
        >
          Recording "{session.recordingName}" imported successfully.
        </Alert>
      )}

      {/* Session retrieval error */}
      {loadError && (
        <Alert severity="error">
          {loadError}
        </Alert>
      )}

      {/* Loading indicator */}
      {loading && (
        <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
          <CircularProgress />
        </Box>
      )}

      {/* Empty state */}
      {!session && !loading && (
        <Card variant="outlined">
          <CardContent sx={{ py: 5, textAlign: "center" }}>
            <Typography variant="h6">
              No recording selected
            </Typography>

            <Typography color="text.secondary" sx={{ mt: 1 }}>
              Import a CSV file to create a replay session.
            </Typography>
          </CardContent>
        </Card>
      )}

      {/* Imported recording metadata */}
      {session && !loading && (
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h6" gutterBottom>
              Imported Recording
            </Typography>

            <Stack spacing={1}>
              <Typography>
                <strong>Name:</strong> {session.recordingName}
              </Typography>

              <Typography>
                <strong>Samples:</strong>{" "}
                {session.sampleCount.toLocaleString()}
              </Typography>

              <Typography>
                <strong>Status:</strong> {session.status}
              </Typography>

              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ overflowWrap: "anywhere" }}
              >
                <strong>Session ID:</strong> {session.sessionId}
              </Typography>

              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ overflowWrap: "anywhere" }}
              >
                <strong>Recording ID:</strong> {session.recordingId}
              </Typography>
            </Stack>
          </CardContent>
        </Card>
      )}

      {/* Recording overview and channel values */}
      {sessionState && !loading && (
        <Stack spacing={3}>
          <RecordingOverview session={sessionState} />

          <ChannelList
            sample={sessionState.currentSample}
            channels={channels}
          />
        </Stack>
      )}

      {/* CSV import dialog */}
      <ImportRecordingDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onImported={handleImported}
      />
    </Stack>
  );
}


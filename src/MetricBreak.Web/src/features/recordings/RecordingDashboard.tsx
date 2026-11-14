
import { useRef, useState } from "react";

import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";

import UploadFileIcon from "@mui/icons-material/UploadFile";
import RefreshIcon from "@mui/icons-material/Refresh";
import ShowChartIcon from "@mui/icons-material/ShowChart";

import { ChannelSelectionPanel } from "./ChannelSelectionPanel";
import type { CreateSessionResponse } from "./recordingTypes";

import { recordingApi } from "./recordingApi";
import { replayApi } from "./replayApi";

import type { SessionState } from "./sessionTypes";
import type { ChannelMetadata } from "./channelMetadata";

import { RecordingWorkspace } from "./RecordingWorkspace";
import { RecordingChart } from "./RecordingChart";
import { ImportRecordingDialog } from "./ImportRecordingDialog";

export function RecordingDashboard() {
  const [session, setSession] =
    useState<SessionState | null>(null);

  const [selectedChannelIds, setSelectedChannelIds] =
    useState<string[]>([]);

  const [channels, setChannels] =
    useState<ChannelMetadata[]>([]);

  const [loading, setLoading] = useState(false);
  const [seeking, setSeeking] = useState(false);

  const [loadError, setLoadError] =
    useState<string | null>(null);

  const [importSuccess, setImportSuccess] =
    useState<string | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);

  // Track the loaded recording so selections can be
  // preserved on refresh but reset for new recordings.
  const loadedSessionIdRef = useRef<string | null>(null);

  // Prevent overlapping seek requests.
  const seekInProgressRef = useRef(false);

  const loadRecording = async (sessionId: string) => {
    setLoading(true);
    setLoadError(null);

    try {
      const [loadedSession, loadedChannels] =
        await Promise.all([
          recordingApi.getSession(sessionId),
          recordingApi.getChannels(sessionId),
        ]);

      const isSameSession =
        loadedSessionIdRef.current ===
        loadedSession.sessionId;

      setSession(loadedSession);
      setChannels(loadedChannels);

      setSelectedChannelIds((current) => {
        const defaultSelection = loadedChannels
          .slice(0, 3)
          .map((channel) => channel.channelId);

        if (!isSameSession) {
          return defaultSelection;
        }

        const availableIds = new Set(
          loadedChannels.map(
            (channel) => channel.channelId,
          ),
        );

        // Preserve the user's selection on refresh,
        // including an intentionally empty selection.
        return current.filter((id) =>
          availableIds.has(id),
        );
      });

      loadedSessionIdRef.current =
        loadedSession.sessionId;

      console.debug("Recording loaded:", {
        sessionId: loadedSession.sessionId,
        sampleCount: loadedSession.sampleCount,
        channelCount: loadedChannels.length,
        currentSequence:
          loadedSession.currentSample?.sequence ?? null,
      });
    } catch (error) {
      console.error("Failed to load recording:", error);

      setLoadError(
        error instanceof Error
          ? error.message
          : "Unable to load recording.",
      );
    } finally {
      setLoading(false);
    }
  };

  const handleImportSuccess = async (
    result: CreateSessionResponse,
  ) => {
    setDialogOpen(false);
    setImportSuccess(null);

    await loadRecording(result.sessionId);

    setImportSuccess("Recording imported successfully.");
  };

  const handleRefresh = async () => {
    if (!session || seeking) {
      return;
    }

    await loadRecording(session.sessionId);
  };

  const handleSeek = async (sequence: number) => {
    if (!session || seekInProgressRef.current || loading) {
      return;
    }

    const targetSessionId = session.sessionId;

    seekInProgressRef.current = true;
    setSeeking(true);
    setLoadError(null);

    try {
      console.debug("Replay seek requested:", {
        sessionId: targetSessionId,
        sequence,
      });

      const response = await replayApi.seek(
        targetSessionId,
        sequence,
      );

      // Ignore a response if a different recording
      // became active while the request was in flight.
      setSession((current) => {
        if (
          !current ||
          current.sessionId !== targetSessionId
        ) {
          return current;
        }

        return response.session;
      });

      console.debug("Replay seek completed:", {
        sessionId: targetSessionId,
        sequence:
          response.session.currentSample?.sequence ??
          null,
      });
    } catch (error) {
      console.error("Replay seek failed:", error);

      setLoadError(
        error instanceof Error
          ? error.message
          : "Unable to seek replay.",
      );
    } finally {
      seekInProgressRef.current = false;
      setSeeking(false);
    }
  };

  const toolbar = (
    <Box
      sx={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        width: "100%",
        minWidth: 0,
        gap: 1,
      }}
    >
      <Box sx={{ minWidth: 0 }}>
        <Typography
          variant="body2"
          fontWeight={600}
          noWrap
        >
          {session
            ? session.recordingName
            : "Recording Workspace"}
        </Typography>

        {session && (
          <Typography
            variant="caption"
            color="text.secondary"
            component="div"
            noWrap
          >
            {session.sampleCount.toLocaleString()} samples
            {" · "}
            Sequence{" "}
            {session.currentSample?.sequence ?? "—"}
            {seeking ? " · Seeking..." : ""}
          </Typography>
        )}
      </Box>

      <Stack
        direction="row"
        spacing={1}
        flexShrink={0}
      >
        {session && (
          <Button
            size="small"
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={handleRefresh}
            disabled={loading || seeking}
          >
            Refresh
          </Button>
        )}

        <Button
          size="small"
          variant="contained"
          startIcon={<UploadFileIcon />}
          onClick={() => setDialogOpen(true)}
          disabled={loading || seeking}
        >
          Import CSV
        </Button>
      </Stack>
    </Box>
  );

  const chart = (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        width: "100%",
        height: "100%",
        minWidth: 0,
        minHeight: 0,
        position: "relative",
      }}
    >
      {loading && (
        <Box
          sx={{
            position: "absolute",
            inset: 0,
            zIndex: 2,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            bgcolor: "background.paper",
            opacity: 0.85,
          }}
        >
          <CircularProgress size={30} />
        </Box>
      )}

      {session ? (
        <Box
          sx={{
            flex: 1,
            minWidth: 0,
            minHeight: 0,
            overflow: "hidden",
          }}
        >
          <RecordingChart
            sessionId={session.sessionId}
            sampleCount={session.sampleCount}
            channels={channels}
            selectedChannelIds={selectedChannelIds}
            currentSequence={
              session.currentSample?.sequence ?? null
            }
            onSeek={handleSeek}
          />
        </Box>
      ) : (
        <Box
          sx={{
            display: "flex",
            flex: 1,
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            gap: 1.5,
            textAlign: "center",
            px: 2,
          }}
        >
          <ShowChartIcon
            sx={{
              fontSize: 56,
              color: "text.disabled",
            }}
          />

          <Typography
            variant="h6"
            fontWeight={600}
          >
            No recording loaded
          </Typography>

          <Typography
            variant="body2"
            color="text.secondary"
          >
            Import a CSV recording to begin exploring
            and debugging time-series data.
          </Typography>

          <Button
            variant="outlined"
            startIcon={<UploadFileIcon />}
            onClick={() => setDialogOpen(true)}
            sx={{ mt: 1 }}
          >
            Import CSV
          </Button>
        </Box>
      )}
    </Box>
  );

  const channelPanel = (
    <ChannelSelectionPanel
      channels={channels}
      selectedChannelIds={selectedChannelIds}
      onSelectionChange={setSelectedChannelIds}
    />
  );

  const details = (
    <Box
      sx={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 2,
        minHeight: 28,
        px: 0.5,
      }}
    >
      <Typography
        variant="caption"
        color="text.secondary"
        noWrap
      >
        {session
          ? `Session: ${session.sessionId}`
          : "Ready"}
      </Typography>

      <Typography
        variant="caption"
        color="text.secondary"
      >
        {channels.length} channels
      </Typography>
    </Box>
  );

  console.debug(
    "Selected channel IDs:",
    selectedChannelIds,
  );

  console.debug(
    "Available channels:",
    channels.length,
  );

  return (
    <>
      <Box
        sx={{
          display: "flex",
          flexDirection: "column",
          flex: 1,
          width: "100%",
          height: "100%",
          minWidth: 0,
          minHeight: 0,
          overflow: "hidden",
        }}
      >
        {(loadError || importSuccess) && (
          <Box sx={{ flexShrink: 0, mb: 1 }}>
            {loadError && (
              <Alert
                severity="error"
                onClose={() => setLoadError(null)}
              >
                {loadError}
              </Alert>
            )}

            {importSuccess && (
              <Alert
                severity="success"
                onClose={() =>
                  setImportSuccess(null)
                }
              >
                {importSuccess}
              </Alert>
            )}
          </Box>
        )}

        <RecordingWorkspace
          toolbar={toolbar}
          chart={chart}
          channels={channelPanel}
          details={details}
        />
      </Box>

      <ImportRecordingDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onImported={handleImportSuccess}
      />
    </>
  );
}

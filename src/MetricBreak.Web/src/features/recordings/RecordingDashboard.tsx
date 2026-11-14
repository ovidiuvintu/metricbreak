import { useRef, useState } from "react";

import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Tab,
  Tabs,
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
import { ReplayControls } from "./ReplayControls";
import { breakpointApi } from "./breakpointApi";
import type { Breakpoint, CreateBreakpointRequest } from "./breakpointApi";
import { BreakpointPanel } from "./BreakpointPanel";
import { BreakpointDialog } from "./BreakpointDialog";

export function RecordingDashboard() {
  const [session, setSession] =
    useState<SessionState | null>(null);

  const [selectedChannelIds, setSelectedChannelIds] =
    useState<string[]>([]);

  const [channels, setChannels] =
    useState<ChannelMetadata[]>([]);

  const [breakpoints, setBreakpoints] = useState<Breakpoint[]>([]);
  const [breakpointError, setBreakpointError] = useState<string | null>(null);
  const [breakpointBusy, setBreakpointBusy] = useState(false);
  const [breakpointDialogOpen, setBreakpointDialogOpen] = useState(false);
  const [sidebarTab, setSidebarTab] = useState<"channels" | "breakpoints">("channels");
  const breakpointMutationRef = useRef(false);
  const loadVersionRef = useRef(0);

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

  const loadRecording = async (sessionId: string): Promise<boolean> => {
    const version = ++loadVersionRef.current;
    setLoading(true);
    setLoadError(null);

    try {
      const [loadedSession, loadedChannels, loadedBreakpoints] =
        await Promise.all([
          recordingApi.getSession(sessionId),
          recordingApi.getChannels(sessionId),
          breakpointApi.list(sessionId),
        ]);

      if (version !== loadVersionRef.current) return false;

      const isSameSession =
        loadedSessionIdRef.current ===
        loadedSession.sessionId;

      setSession(loadedSession);
      setChannels(loadedChannels);
      setBreakpoints(loadedBreakpoints.breakpoints);
      setBreakpointError(null);
      if (!isSameSession) {
        setSidebarTab("channels");
        setBreakpointDialogOpen(false);
      }

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
      return true;
    } catch (error) {
      if (version !== loadVersionRef.current) return false;
      console.error("Failed to load recording:", error);

      setLoadError(
        error instanceof Error
          ? error.message
          : "Unable to load recording.",
      );
      return false;
    } finally {
      if (version === loadVersionRef.current) setLoading(false);
    }
  };

  const handleImportSuccess = async (
    result: CreateSessionResponse,
  ) => {
    setDialogOpen(false);
    setImportSuccess(null);

    if (await loadRecording(result.sessionId)) {
      setImportSuccess("Recording imported successfully.");
    }
  };

  const handleRefresh = async () => {
    if (!session || seeking || breakpointMutationRef.current || loading) {
      return;
    }

    await loadRecording(session.sessionId);
  };


  const handleStepForward = async () => {
    if (!session || seekInProgressRef.current || loading || breakpointMutationRef.current) {
      return;
    }

    seekInProgressRef.current = true;
    setSeeking(true);
    setLoadError(null);

    try {
      const response = await replayApi.stepForward(
        session.sessionId,
      );

      setSession((current) =>
        current?.sessionId === response.session.sessionId
          ? response.session
          : current,
      );
    } catch (error) {
      setLoadError(
        error instanceof Error
          ? error.message
          : "Unable to step forward.",
      );
    } finally {
      seekInProgressRef.current = false;
      setSeeking(false);
    }
  };

  const handleStepBackward = async () => {
    if (!session || seekInProgressRef.current || loading || breakpointMutationRef.current) {
      return;
    }

    seekInProgressRef.current = true;
    setSeeking(true);
    setLoadError(null);

    try {
      const response = await replayApi.stepBackward(
        session.sessionId,
      );

      setSession((current) =>
        current?.sessionId === response.session.sessionId
          ? response.session
          : current,
      );
    } catch (error) {
      setLoadError(
        error instanceof Error
          ? error.message
          : "Unable to step backward.",
      );
    } finally {
      seekInProgressRef.current = false;
      setSeeking(false);
    }
  };

  const handleContinue = async () => {
    if (!session || seekInProgressRef.current || loading || breakpointMutationRef.current) {
      return;
    }

    const sessionId = session.sessionId;

    seekInProgressRef.current = true;
    setSeeking(true);
    setLoadError(null);

    try {
      const response = await replayApi.continue(sessionId);

      setSession((current) =>
        current?.sessionId === sessionId
          ? response.session
          : current,
      );

      if (!response.executed) {
        console.debug(
          "Continue did not execute: no active breakpoints.",
        );
      } else if (response.hitBreakpoint) {
        console.debug("Replay stopped at breakpoint:", response.stop);
      } else if (response.reachedEndOfRecording) {
        console.debug("Replay reached end of recording.");
      }
    } catch (error) {
      setLoadError(
        error instanceof Error
          ? error.message
          : "Unable to continue replay.",
      );
    } finally {
      seekInProgressRef.current = false;
      setSeeking(false);
    }
  };

  const handleReset = async () => {
    if (!session || seekInProgressRef.current || loading || breakpointMutationRef.current) {
      return;
    }

    seekInProgressRef.current = true;
    setSeeking(true);
    setLoadError(null);

    try {
      const response = await replayApi.reset(
        session.sessionId,
      );

      setSession((current) =>
        current?.sessionId === response.session.sessionId
          ? response.session
          : current,
      );
    } catch (error) {
      setLoadError(
        error instanceof Error
          ? error.message
          : "Unable to reset replay.",
      );
    } finally {
      seekInProgressRef.current = false;
      setSeeking(false);
    }
  };

  const handleSeek = async (sequence: number) => {
    if (!session || seekInProgressRef.current || loading || breakpointMutationRef.current) {
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

  const runBreakpointMutation = async (
    operation: (sessionId: string) => Promise<void>,
  ): Promise<boolean> => {
    if (!session || loading || seeking || breakpointMutationRef.current) {
      return false;
    }

    const sessionId = session.sessionId;
    breakpointMutationRef.current = true;
    setBreakpointBusy(true);
    setBreakpointError(null);

    try {
      await operation(sessionId);
      const result = await breakpointApi.list(sessionId);
      // Ignore stale results if a different recording was loaded.
      setBreakpoints((current) =>
        loadedSessionIdRef.current === sessionId
          ? result.breakpoints
          : current,
      );
      return true;
    } catch (error) {
      if (loadedSessionIdRef.current === sessionId) {
        setBreakpointError(
          error instanceof Error ? error.message : "Breakpoint operation failed.",
        );
      }
      return false;
    } finally {
      breakpointMutationRef.current = false;
      setBreakpointBusy(false);
    }
  };

  const handleCreateBreakpoint = async (
    request: CreateBreakpointRequest,
  ): Promise<void> => {
    const succeeded = await runBreakpointMutation(async (sessionId) => {
      await breakpointApi.create(sessionId, request);
    });
    if (succeeded) setBreakpointDialogOpen(false);
  };

  const handleSetBreakpointEnabled = (
    breakpointId: string,
    isEnabled: boolean,
  ) => {
    void runBreakpointMutation((sessionId) =>
      breakpointApi.setEnabled(sessionId, breakpointId, isEnabled),
    );
  };

  const handleDeleteBreakpoint = (breakpointId: string) => {
    void runBreakpointMutation((sessionId) =>
      breakpointApi.delete(sessionId, breakpointId),
    );
  };

  const replayControls = session ? (
  <ReplayControls
    currentSequence={
      session.currentSample?.sequence ?? 0
    }
    sampleCount={session.sampleCount}
    busy={loading || seeking}
    onStepForward={handleStepForward}
    onStepBackward={handleStepBackward}
    onReset={handleReset}
    onContinue={handleContinue}
  />
) : null;

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
            disabled={loading || seeking || breakpointBusy}
          >
            Refresh
          </Button>
        )}

        <Button
          size="small"
          variant="contained"
          startIcon={<UploadFileIcon />}
          onClick={() => setDialogOpen(true)}
          disabled={loading || seeking || breakpointBusy}
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
    <Box sx={{ display: "flex", flexDirection: "column", minWidth: 0 }}>
      <Tabs
        value={sidebarTab}
        onChange={(_, value: "channels" | "breakpoints") => setSidebarTab(value)}
        variant="fullWidth"
        sx={{ minHeight: 40, mb: 1, "& .MuiTab-root": { minHeight: 40, px: 0.5 } }}
      >
        <Tab value="channels" label="Channels" />
        <Tab value="breakpoints" label={`Breakpoints (${breakpoints.length})`} />
      </Tabs>
      {sidebarTab === "channels" ? (
        <ChannelSelectionPanel
          channels={channels}
          selectedChannelIds={selectedChannelIds}
          onSelectionChange={setSelectedChannelIds}
        />
      ) : (
        <BreakpointPanel
          breakpoints={breakpoints}
          channels={channels}
          loading={loading}
          busy={breakpointBusy || seeking}
          error={breakpointError}
          onAdd={() => { if (session) setBreakpointDialogOpen(true); }}
          onSetEnabled={handleSetBreakpointEnabled}
          onDelete={handleDeleteBreakpoint}
        />
      )}
    </Box>
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
          replayControls={replayControls}
          details={details}
        />
      </Box>

      <BreakpointDialog
        key={session?.sessionId ?? "no-session"}
        open={breakpointDialogOpen && session !== null}
        channels={channels}
        busy={breakpointBusy}
        onClose={() => setBreakpointDialogOpen(false)}
        onCreate={handleCreateBreakpoint}
      />

      <ImportRecordingDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onImported={handleImportSuccess}
      />
    </>
  );
}

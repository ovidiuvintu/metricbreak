
import { useEffect, useRef, useState } from "react";

import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  Tab,
  Tabs,
  Typography,
} from "@mui/material";

import UploadFileIcon from "@mui/icons-material/UploadFile";
import RefreshIcon from "@mui/icons-material/Refresh";
import ShowChartIcon from "@mui/icons-material/ShowChart";

import { ChannelSelectionPanel } from "./ChannelSelectionPanel";
import { ChannelList } from "./ChannelList";
import { RecordingWorkspace } from "./RecordingWorkspace";
import { RecordingChart } from "./RecordingChart";
import { ImportRecordingDialog } from "./ImportRecordingDialog";
import { ReplayControls } from "./ReplayControls";
import { BreakpointPanel } from "./BreakpointPanel";
import { BreakpointDialog } from "./BreakpointDialog";
import { CompositeBreakpointDialog } from "./CompositeBreakpointDialog";

import { recordingApi } from "./recordingApi";
import { replayApi } from "./replayApi";
import {
  breakpointApi,
  type Breakpoint,
  type CompositeBreakpoint,
  type CreateBreakpointRequest,
  type CreateCompositeBreakpointRequest,
} from "./breakpointApi";

import type { CreateSessionResponse } from "./recordingTypes";
import type { SessionState } from "./sessionTypes";
import type { ChannelMetadata } from "./channelMetadata";
import type { SampleRangeItem } from "./sampleTypes";

type InspectorTab = 0 | 1 | 2;

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

export function RecordingDashboard() {
  const [session, setSession] = useState<SessionState | null>(
    null,
  );

  const [channels, setChannels] = useState<ChannelMetadata[]>(
    [],
  );

  const [selectedChannelIds, setSelectedChannelIds] = useState<
    string[]
  >([]);

  const [breakpoints, setBreakpoints] = useState<Breakpoint[]>(
    [],
  );

  const [compositeBreakpoints, setCompositeBreakpoints] = useState<CompositeBreakpoint[]>([]);

  const [inspectorTab, setInspectorTab] =
    useState<InspectorTab>(0);

  const [previousSampleResult, setPreviousSampleResult] = useState<{
    sessionId: string;
    currentSequence: number;
    sample: SampleRangeItem;
  } | null>(null);

  const [loading, setLoading] = useState(false);
  const [seeking, setSeeking] = useState(false);

  const [breakpointsLoading, setBreakpointsLoading] =
    useState(false);

  const [breakpointBusy, setBreakpointBusy] = useState(false);

  const [loadError, setLoadError] = useState<string | null>(
    null,
  );

  const [breakpointError, setBreakpointError] = useState<
    string | null
  >(null);

  const [importSuccess, setImportSuccess] = useState<
    string | null
  >(null);

  const [dialogOpen, setDialogOpen] = useState(false);

  const [breakpointDialogOpen, setBreakpointDialogOpen] =
    useState(false);

  const [compositeDialogOpen, setCompositeDialogOpen] = useState(false);

  const loadedSessionIdRef = useRef<string | null>(null);
  const activeSessionIdRef = useRef<string | null>(null);

  const loadVersionRef = useRef(0);
  const seekInProgressRef = useRef(false);
  const breakpointInProgressRef = useRef(false);

  // Fetch the immediately preceding *recorded* sample only when inspecting Values.
  // A cancellation flag prevents older seeks from publishing stale comparisons.
  const watchSessionId = session?.sessionId;
  const watchSequence = session?.currentSample?.sequence;

  useEffect(() => {
    if (inspectorTab !== 2 || !watchSessionId || watchSequence == null || watchSequence <= 0) {
      return;
    }

    let cancelled = false;
    void recordingApi.getSamples(watchSessionId, watchSequence - 1, 1)
      .then((response) => {
        if (cancelled) return;
        const preceding = response.samples.find((item) => item.sequence === watchSequence - 1);
        if (preceding) {
          setPreviousSampleResult({
            sessionId: watchSessionId,
            currentSequence: watchSequence,
            sample: preceding,
          });
        }
      })
      .catch((error: unknown) => {
        // No comparison is displayed when the preceding sample cannot be retrieved.
        if (!cancelled) console.warn("Unable to load previous watch sample:", error);
      });

    return () => { cancelled = true; };
  }, [inspectorTab, watchSessionId, watchSequence]);

  const previousWatchSample =
    previousSampleResult != null &&
    previousSampleResult.sessionId === watchSessionId &&
    previousSampleResult.currentSequence === watchSequence
      ? previousSampleResult.sample
      : null;

  // --------------------------------------------------
  // Recording loading
  // --------------------------------------------------

  const loadRecording = async (
    sessionId: string,
  ): Promise<boolean> => {
    const version = ++loadVersionRef.current;

    setLoading(true);
    setLoadError(null);
    setBreakpointError(null);
    setBreakpointsLoading(true);

    try {
      const [loadedSession, loadedChannels, loadedBreakpoints] =
        await Promise.all([
          recordingApi.getSession(sessionId),
          recordingApi.getChannels(sessionId),
          breakpointApi.list(sessionId),
        ]);

      if (version !== loadVersionRef.current) {
        return false;
      }

      const isSameSession =
        loadedSessionIdRef.current === loadedSession.sessionId;

      activeSessionIdRef.current = loadedSession.sessionId;

      setSession(loadedSession);
      setChannels(loadedChannels);
      setBreakpoints(loadedBreakpoints.breakpoints);
      setCompositeBreakpoints(loadedBreakpoints.compositeBreakpoints ?? []);

      setSelectedChannelIds((current) => {
        if (!isSameSession) {
          return loadedChannels
            .slice(0, 3)
            .map((channel) => channel.channelId);
        }

        const availableIds = new Set(
          loadedChannels.map((channel) => channel.channelId),
        );

        return current.filter((id) => availableIds.has(id));
      });

      if (!isSameSession) {
        setInspectorTab(0);
        setBreakpointDialogOpen(false);
        setCompositeDialogOpen(false);
      }

      loadedSessionIdRef.current = loadedSession.sessionId;

      console.debug("Recording loaded:", {
        sessionId: loadedSession.sessionId,
        sampleCount: loadedSession.sampleCount,
        channelCount: loadedChannels.length,
        breakpointCount: loadedBreakpoints.breakpoints.length +
          (loadedBreakpoints.compositeBreakpoints?.length ?? 0),
      });

      return true;
    } catch (error) {
      if (version === loadVersionRef.current) {
        setLoadError(
          errorMessage(error, "Unable to load recording."),
        );
      }

      return false;
    } finally {
      if (version === loadVersionRef.current) {
        setLoading(false);
        setBreakpointsLoading(false);
      }
    }
  };

  const handleImportSuccess = async (
    result: CreateSessionResponse,
  ) => {
    setDialogOpen(false);
    setImportSuccess(null);

    const loaded = await loadRecording(result.sessionId);

    if (loaded) {
      setImportSuccess("Recording imported successfully.");
    }
  };

  const handleRefresh = async () => {
    if (
      !session ||
      loading ||
      seeking ||
      breakpointInProgressRef.current
    ) {
      return;
    }

    await loadRecording(session.sessionId);
  };

  // --------------------------------------------------
  // Replay operations
  // --------------------------------------------------

  const executeReplay = async (
    operation: (sessionId: string) => Promise<SessionState>,
    fallbackError: string,
  ) => {
    if (
      !session ||
      seekInProgressRef.current ||
      breakpointInProgressRef.current ||
      loading
    ) {
      return;
    }

    const sessionId = session.sessionId;

    seekInProgressRef.current = true;
    setSeeking(true);
    setLoadError(null);

    try {
      const updatedSession = await operation(sessionId);

      setSession((current) =>
        current?.sessionId === sessionId
          ? updatedSession
          : current,
      );
    } catch (error) {
      if (activeSessionIdRef.current === sessionId) {
        setLoadError(errorMessage(error, fallbackError));
      }
    } finally {
      seekInProgressRef.current = false;
      setSeeking(false);
    }
  };

  const handleStepForward = async () => {
    await executeReplay(
      async (sessionId) =>
        (await replayApi.stepForward(sessionId)).session,
      "Unable to step forward.",
    );
  };

  const handleStepBackward = async () => {
    await executeReplay(
      async (sessionId) =>
        (await replayApi.stepBackward(sessionId)).session,
      "Unable to step backward.",
    );
  };

  const handleReset = async () => {
    await executeReplay(
      async (sessionId) =>
        (await replayApi.reset(sessionId)).session,
      "Unable to reset replay.",
    );
  };

  const handleSeek = async (sequence: number) => {
    await executeReplay(
      async (sessionId) =>
        (await replayApi.seek(sessionId, sequence)).session,
      "Unable to seek replay.",
    );
  };

  const handleContinue = async () => {
    await executeReplay(async (sessionId) => {
      const response = await replayApi.continue(sessionId);

      if (!response.executed) {
        console.debug(
          "Continue did not execute: no active breakpoints.",
        );
      } else if (response.hitBreakpoint) {
        console.debug(
          "Replay stopped at breakpoint:",
          response.stop,
        );
      } else if (response.reachedEndOfRecording) {
        console.debug("Replay reached end of recording.");
      }

      return response.session;
    }, "Unable to continue replay.");
  };

  // --------------------------------------------------
  // Breakpoint operations
  // --------------------------------------------------

  const refreshBreakpoints = async (sessionId: string) => {
    const response = await breakpointApi.list(sessionId);

    if (activeSessionIdRef.current === sessionId) {
      setBreakpoints(response.breakpoints);
      setCompositeBreakpoints(response.compositeBreakpoints ?? []);
    }
  };

  const executeBreakpointMutation = async (
    mutation: (sessionId: string) => Promise<void>,
  ) => {
    if (
      !session ||
      loading ||
      seeking ||
      breakpointInProgressRef.current
    ) {
      return false;
    }

    const sessionId = session.sessionId;

    breakpointInProgressRef.current = true;
    setBreakpointBusy(true);
    setBreakpointError(null);

    try {
      await mutation(sessionId);
      await refreshBreakpoints(sessionId);

      return true;
    } catch (error) {
      if (activeSessionIdRef.current === sessionId) {
        setBreakpointError(
          errorMessage(error, "Breakpoint operation failed."),
        );
      }

      return false;
    } finally {
      breakpointInProgressRef.current = false;
      setBreakpointBusy(false);
    }
  };

  const handleCreateBreakpoint = async (
    request: CreateBreakpointRequest,
  ) => {
    const succeeded = await executeBreakpointMutation(
      async (sessionId) => {
        await breakpointApi.create(sessionId, request);
      },
    );

    if (succeeded) {
      setBreakpointDialogOpen(false);
    }
  };

  const handleCreateCompositeBreakpoint = async (
    request: CreateCompositeBreakpointRequest,
  ) => {
    const succeeded = await executeBreakpointMutation(async (sessionId) => {
      await breakpointApi.createComposite(sessionId, request);
    });

    if (succeeded) {
      setCompositeDialogOpen(false);
    }
  };

  const handleSetBreakpointEnabled = (
    breakpointId: string,
    isEnabled: boolean,
  ) => {
    void executeBreakpointMutation(async (sessionId) => {
      await breakpointApi.setEnabled(
        sessionId,
        breakpointId,
        isEnabled,
      );
    });
  };

  const handleDeleteBreakpoint = (breakpointId: string) => {
    void executeBreakpointMutation(async (sessionId) => {
      await breakpointApi.delete(sessionId, breakpointId);
    });
  };

  // --------------------------------------------------
  // Replay controls
  // --------------------------------------------------

  const replayControls = session ? (
    <ReplayControls
      currentSequence={session.currentSample?.sequence ?? 0}
      sampleCount={session.sampleCount}
      busy={loading || seeking || breakpointBusy}
      onStepForward={handleStepForward}
      onStepBackward={handleStepBackward}
      onReset={handleReset}
      onContinue={handleContinue}
    />
  ) : null;

  // --------------------------------------------------
  // Toolbar
  // --------------------------------------------------

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
        <Typography variant="body2" fontWeight={600} noWrap>
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
            Sequence {session.currentSample?.sequence ?? "—"}
            {seeking ? " · Seeking..." : ""}
          </Typography>
        )}
      </Box>

      <Stack direction="row" spacing={1} flexShrink={0}>
        {session && (
          <Button
            size="small"
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={handleRefresh}
            disabled={
              loading || seeking || breakpointBusy
            }
          >
            Refresh
          </Button>
        )}

        <Button
          size="small"
          variant="contained"
          startIcon={<UploadFileIcon />}
          onClick={() => setDialogOpen(true)}
          disabled={
            loading || seeking || breakpointBusy
          }
        >
          Import CSV
        </Button>
      </Stack>
    </Box>
  );

  // --------------------------------------------------
  // Chart
  // --------------------------------------------------

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

          <Typography variant="h6" fontWeight={600}>
            No recording loaded
          </Typography>

          <Typography variant="body2" color="text.secondary">
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

  // --------------------------------------------------
  // Inspector: Channels | Breakpoints | Values
  // --------------------------------------------------

  const channelPanel = (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        height: "100%",
        minHeight: 0,
      }}
    >
      <Tabs
        value={inspectorTab}
        onChange={(_, value: InspectorTab) =>
          setInspectorTab(value)
        }
        variant="fullWidth"
        sx={{
          flexShrink: 0,
          minHeight: 40,
          "& .MuiTab-root": {
            minHeight: 40,
            minWidth: 0,
            px: 0.5,
            fontSize: "0.72rem",
            textTransform: "none",
          },
        }}
      >
        <Tab label="Channels" />
        <Tab label="Breakpoints" />
        <Tab label="Values" />
      </Tabs>

      <Box
        role="tabpanel"
        hidden={inspectorTab !== 0}
        sx={{
          flex: 1,
          minHeight: 0,
          overflowY: "auto",
          display: inspectorTab === 0 ? "block" : "none",
        }}
      >
        <ChannelSelectionPanel
          channels={channels}
          selectedChannelIds={selectedChannelIds}
          onSelectionChange={setSelectedChannelIds}
        />
      </Box>

      <Box
        role="tabpanel"
        hidden={inspectorTab !== 1}
        sx={{
          flex: 1,
          minHeight: 0,
          overflowY: "auto",
          display: inspectorTab === 1 ? "block" : "none",
        }}
      >
        {session && (
          <BreakpointPanel
            breakpoints={breakpoints}
            compositeBreakpoints={compositeBreakpoints}
            channels={channels}
            loading={loading}
            busy={breakpointBusy || seeking}
            error={breakpointError}
            onAdd={() => setBreakpointDialogOpen(true)}
            onAddComposite={() => setCompositeDialogOpen(true)}
            onSetEnabled={handleSetBreakpointEnabled}
            onDelete={handleDeleteBreakpoint}
          />
        )}
      </Box>

      <Box
        role="tabpanel"
        hidden={inspectorTab !== 2}
        sx={{
          flex: 1,
          minHeight: 0,
          overflowY: "auto",
          display: inspectorTab === 2 ? "block" : "none",
        }}
      >
        {session?.currentSample ? (
          <ChannelList
            sample={session.currentSample}
            previousSample={previousWatchSample}
            channels={channels}
            selectedChannelIds={selectedChannelIds}
          />
        ) : (
          <Typography
            variant="body2"
            color="text.secondary"
            sx={{ p: 2 }}
          >
            No current sample available.
          </Typography>
        )}
      </Box>
    </Box>
  );

  // --------------------------------------------------
  // Status bar
  // --------------------------------------------------

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

  // --------------------------------------------------
  // Render
  // --------------------------------------------------

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
                onClose={() => setImportSuccess(null)}
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

      <ImportRecordingDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        onImported={handleImportSuccess}
      />

      <BreakpointDialog
        open={breakpointDialogOpen}
        channels={channels}
        busy={breakpointBusy}
        onClose={() => {
          if (!breakpointBusy) {
            setBreakpointDialogOpen(false);
          }
        }}
        onCreate={handleCreateBreakpoint}
      />

      <CompositeBreakpointDialog
        open={compositeDialogOpen}
        channels={channels}
        busy={breakpointBusy}
        error={breakpointError}
        onClose={() => {
          if (!breakpointBusy) setCompositeDialogOpen(false);
        }}
        onCreate={handleCreateCompositeBreakpoint}
      />
    </>
  );
}

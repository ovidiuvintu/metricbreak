
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Snackbar,
  Stack,
  Tab,
  Tabs,
  Typography,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";

import UploadFileIcon from "@mui/icons-material/UploadFile";
import RefreshIcon from "@mui/icons-material/Refresh";
import AddIcon from "@mui/icons-material/Add";
import ShowChartIcon from "@mui/icons-material/ShowChart";

import { assignChartSeriesColors } from "../../theme/chartTheme";
import { ChannelSelectionPanel } from "./ChannelSelectionPanel";
import { ChannelList } from "./ChannelList";
import { RecordingWorkspace } from "./RecordingWorkspace";
import {
  RecordingChart,
  type ChartViewportReader,
} from "./RecordingChart";
import { CsvOpenWizard, type CsvWizardFinish } from "./CsvOpenWizard";
import { ReplayControls } from "./ReplayControls";
import { BreakpointPanel } from "./BreakpointPanel";
import { RecordingTimelinePanel } from "./RecordingTimelinePanel";
import { recordingTimelineApi } from "./recordingTimelineApi";
import type { RecordingTimeline } from "./recordingTimelineTypes";
import { BreakpointDialog, type BreakpointDialogDraft } from "./BreakpointDialog";
import { CompositeBreakpointDialog } from "./CompositeBreakpointDialog";
import { InvestigationPanel } from "../investigations/InvestigationPanel";
import { InvestigationsResumeList } from "../investigations/InvestigationsResumeList";
import { InvestigationsHomePanel } from "../investigations/InvestigationsHomePanel";
import { saveInvestigationSelection } from "../investigations/investigationSelectionStorage";
import { tryQuickBookmark } from "../investigations/quickBookmark";
import { useActiveInvestigationBookmarks } from "../investigations/useActiveInvestigationBookmarks";
import { RecordingDebuggerDashboard } from "../dashboard/RecordingDebuggerDashboard";
import { RecordingsCatalogPanel } from "./RecordingsCatalogPanel";
import { AppSettingsPanel } from "../../app/AppSettingsPanel";
import { ProjectDashboard } from "../projects/ProjectDashboard";
import { SaveAsProjectDialog } from "../projects/SaveAsProjectDialog";
import { projectApi } from "../projects/projectApi";
import { AnalysisPanel } from "../analysis/AnalysisPanel";
import type { PaletteMode } from "@mui/material";
import { formatOpenInvestigationError } from "../investigations/formatOpenInvestigationError";
import { ApiError } from "../../shared/api/httpClient";
import { clearLastInvestigation, loadLastInvestigation } from "../investigations/investigationResumeStorage";
import { resolveSavedChannelIds } from "../investigations/resolveSavedChannelIds";
import { useWorkspace } from "../../workspace/WorkspaceProvider";
import { saveResumeSequence, getResumeSequence } from "../dashboard/replayResumeStorage";
import { loadInvestigationSelection } from "../investigations/investigationSelectionStorage";
import type { AxisPin, InvestigationHit, InvestigationUiState } from "../investigations/investigationTypes";
import {
  axisPinsFromLegacyRightAxis,
  type AxisPins,
} from "./chartAxisAssignment";
import type { AppSection } from "../../app/AppSidebar";

import { isCsvFile, recordingApi } from "./recordingApi";
import { investigationApi } from "../investigations/investigationApi";
import { analysisApi } from "../analysis/analysisApi";
import { replayApi, type BreakpointStopDetails } from "./replayApi";
import {
  breakpointApi,
  type Breakpoint,
  type BreakpointCoverage,
  type CompositeBreakpoint,
  type CreateBreakpointRequest,
  type CreateCompositeBreakpointRequest,
  type CreateMissingDataBreakpointRequest,
  type CreateTimelineBreakpointRequest,
} from "./breakpointApi";

import type { CreateSessionResponse } from "./recordingTypes";
import type { CurrentSample, SessionState } from "./sessionTypes";
import type { ChannelExtent, ChannelMetadata } from "./channelMetadata";
import type { SampleRangeItem } from "./sampleTypes";

interface PinnedBreakpointHit {
  sequence: number;
  breakpointId: string | null;
  label: string | null;
}

type InspectorTab = 0 | 1 | 2 | 3;
type PlaybackState = "stopped" | "playing" | "paused";

type SessionNavigationAction =
  | { kind: "open"; investigationId: string; projectId: string }
  | {
      kind: "import";
      result: CreateSessionResponse;
      destination?: "dashboard" | "recordings";
    };

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

const SLIDING_WINDOW_STORAGE_KEY = "metricbreak-sliding-window";

function readSlidingWindowEnabled(): boolean {
  try {
    return localStorage.getItem(SLIDING_WINDOW_STORAGE_KEY) !== "false";
  } catch {
    return true;
  }
}

interface RecordingDashboardProps {
  section: AppSection;
  onSelectSection: (section: AppSection) => void;
  deepLinkInvestigation?: {
    investigationId: string;
    projectId: string;
  } | null;
  onDeepLinkHandled?: () => void;
  onInvestigationOpened?: (
    investigationId: string,
    projectId: string,
  ) => void;
  themeMode?: PaletteMode;
  onToggleTheme?: () => void;
}

export function RecordingDashboard({
  section,
  onSelectSection,
  deepLinkInvestigation = null,
  onDeepLinkHandled,
  onInvestigationOpened,
  themeMode = "light",
  onToggleTheme,
}: RecordingDashboardProps) {
  const [slidingWindow, setSlidingWindow] = useState(readSlidingWindowEnabled);
  const updateSlidingWindow = (enabled: boolean) => {
    setSlidingWindow(enabled);
    try {
      localStorage.setItem(SLIDING_WINDOW_STORAGE_KEY, String(enabled));
    } catch {
      // Keep the choice for this session when storage is unavailable.
    }
  };
  const {
    context,
    activeProject,
    investigationRequest,
    setRecordingSession,
    setActiveInvestigation,
    setActiveProject,
    clearInvestigation,
    refreshProjectInvestigations,
  } = useWorkspace();
  const [session, setSession] = useState<SessionState | null>(
    null,
  );

  const [channels, setChannels] = useState<ChannelMetadata[]>(
    [],
  );

  const [selectedChannelIds, setSelectedChannelIds] = useState<
    string[]
  >([]);

  const [axisPins, setAxisPins] = useState<AxisPins>({});
  const [effectiveRightAxisIds, setEffectiveRightAxisIds] = useState<string[]>([]);
  const [seriesColors, setSeriesColors] = useState<Record<string, string>>({});
  const theme = useTheme();
  const plottedSeriesColors = useMemo(() => {
    const right = new Set(effectiveRightAxisIds);
    const inputs = channels
      .map((channel, channelIndex) => ({ channel, channelIndex }))
      .filter(({ channel }) => selectedChannelIds.includes(channel.channelId))
      .map(({ channel, channelIndex }) => ({
        channelId: channel.channelId,
        channelName: channel.name,
        channelIndex,
        yAxisIndex: (right.has(channel.channelId) ? 1 : 0) as 0 | 1,
      }));
    return Object.fromEntries(
      assignChartSeriesColors(theme.palette.mode, inputs, right.size > 0),
    );
  }, [channels, selectedChannelIds, effectiveRightAxisIds, theme.palette.mode]);
  const valueSeriesColors = { ...plottedSeriesColors, ...seriesColors };

  const setAxisPin = (channelId: string, pin: AxisPin) => {
    setAxisPins((previous) => ({
      ...previous,
      [channelId]: pin,
    }));
  };

  const clearAxisPin = (channelId: string) => {
    setAxisPins((previous) => {
      const next = { ...previous };
      delete next[channelId];
      return next;
    });
  };

  const handleAxisPinChange = (
    channelId: string,
    pin: "left" | "right" | "auto",
  ) => {
    if (pin === "auto") {
      clearAxisPin(channelId);
      return;
    }
    setAxisPin(channelId, pin);
  };

  const clearAllAxisPins = () => setAxisPins({});

  const handleChannelSelectionChange = (ids: string[]) => {
    setSelectedChannelIds(ids);
    setAxisPins((previous) => {
      const next = { ...previous };
      for (const channelId of Object.keys(next)) {
        if (!ids.includes(channelId)) delete next[channelId];
      }
      return next;
    });
  };

  const [breakpoints, setBreakpoints] = useState<Breakpoint[]>(
    [],
  );

  const [compositeBreakpoints, setCompositeBreakpoints] = useState<CompositeBreakpoint[]>([]);
  const [breakpointCoverage, setBreakpointCoverage] = useState<BreakpointCoverage[]>([]);

  const [inspectorTab, setInspectorTab] =
    useState<InspectorTab>(0);

  const [recordingTimeline, setRecordingTimeline] =
    useState<RecordingTimeline | null>(null);
  const [timelineLoading, setTimelineLoading] = useState(false);

  const [previousSampleResult, setPreviousSampleResult] = useState<{
    sessionId: string;
    currentSequence: number;
    sample: SampleRangeItem;
  } | null>(null);
  // Samples already shown, keyed by session and sequence. Stepping reuses the
  // sample that was just on screen, so the Values list does not drop its
  // comparison and redraw a frame later.
  const seenSamplesRef = useRef(new Map<string, CurrentSample | SampleRangeItem>());
  const seenSessionRef = useRef<string | null>(null);

  const [loading, setLoading] = useState(false);
  const [seeking, setSeeking] = useState(false);
  const [playbackState, setPlaybackState] = useState<PlaybackState>("stopped");
  // The chart cursor stays hidden until the user starts replay on this file.
  const [replayCursorVisible, setReplayCursorVisible] = useState(false);
  const [playbackBusy, setPlaybackBusy] = useState(false);
  const playbackEpochRef = useRef(0);
  const playbackTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const playbackBusyRef = useRef(false);
  const playbackRunningRef = useRef(false);
  const playbackStateRef = useRef<PlaybackState>("stopped");
  const playbackSessionRef = useRef<string | null>(null);
  const scrubRef = useRef<{ sessionId: string; wasPlaying: boolean; pause: Promise<void> } | null>(null);


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

  const [wizardOpen, setWizardOpen] = useState(false);
  const [wizardFile, setWizardFile] = useState<File | null>(null);
  const [wizardConfirmTimestamp, setWizardConfirmTimestamp] = useState(false);
  const [wizardSessionId, setWizardSessionId] = useState<string | null>(null);
  const pendingWizardRef = useRef<{
    file: File | null;
    confirmTimestamp: boolean;
  } | null>(null);
  const requestCsvWizardRef = useRef<
    (file: File | null, confirmTimestamp: boolean) => void
  >(() => {});
  const [saveAsProjectOpen, setSaveAsProjectOpen] = useState(false);
  const [saveAsProjectBusy, setSaveAsProjectBusy] = useState(false);
  const [saveAsProjectError, setSaveAsProjectError] = useState("");

  const [breakpointDialogOpen, setBreakpointDialogOpen] =
    useState(false);
  const [breakpointDraft, setBreakpointDraft] =
    useState<BreakpointDialogDraft | null>(null);
  const [focusedBreakpointId, setFocusedBreakpointId] =
    useState<string | null>(null);

  const [bookmarkRevision, setBookmarkRevision] = useState(0);
  const [investigationSetupHint, setInvestigationSetupHint] = useState<string | null>(null);
  const [bookmarkSnackOpen, setBookmarkSnackOpen] = useState(false);
  const [bookmarkSnackMessage, setBookmarkSnackMessage] = useState("");
  const [bookmarkSnackPrompt, setBookmarkSnackPrompt] = useState(false);
  const [viewportFocusRequestId, setViewportFocusRequestId] = useState(0);
  const [viewportFocusSequence, setViewportFocusSequence] = useState<number | null>(null);
  const [restoreChartViewportRequestId, setRestoreChartViewportRequestId] =
    useState(0);
  const [restoreChartViewport, setRestoreChartViewport] =
    useState<InvestigationUiState["chartViewport"]>(null);
  const chartViewportReaderRef = useRef<ChartViewportReader | null>(null);
  const investigationUiBaselineRef = useRef<{
    investigationId: string;
    snapshot: string;
  } | null>(null);

  const snapshotInvestigationUiState = (uiState: InvestigationUiState) =>
    JSON.stringify({
      selectedChannelIds: uiState.selectedChannelIds ?? [],
      rightAxisChannelIds: uiState.rightAxisChannelIds ?? [],
      axisPins: uiState.axisPins ?? {},
      chartViewport: uiState.chartViewport ?? null,
    });
  const [breakpointHitsRefreshKey, setBreakpointHitsRefreshKey] = useState(0);
  const [breakpointHit, setBreakpointHit] = useState<PinnedBreakpointHit | null>(null);

  const [compositeDialogOpen, setCompositeDialogOpen] = useState(false);
  const [channelExtents, setChannelExtents] = useState<Record<string, ChannelExtent>>({});
  const channelExtentsSessionRef = useRef<string | null>(null);

  const channelsRef = useRef<ChannelMetadata[]>([]);
  channelsRef.current = channels;
  const loadedSessionIdRef = useRef<string | null>(null);
  const activeSessionIdRef = useRef<string | null>(null);

  const loadVersionRef = useRef(0);
  const seekInProgressRef = useRef(false);
  const breakpointInProgressRef = useRef(false);

  // Cancels scheduled work and prevents older responses from publishing state.
  // Backend generation tokens independently reject stale advances.
  const cancelLocalPlayback = () => {
    console.trace("PLAYBACK TIMER CANCELLED", {
      timer: playbackTimerRef.current,
      epoch: playbackEpochRef.current,
      running: playbackRunningRef.current,
    });
    ++playbackEpochRef.current;
    playbackRunningRef.current = false;
    if (playbackTimerRef.current !== null) {
      clearTimeout(playbackTimerRef.current);
      playbackTimerRef.current = null;
    }
  };

  useEffect(() => () => {
    ++playbackEpochRef.current;
    if (playbackTimerRef.current !== null) {
      clearTimeout(playbackTimerRef.current);
    }
  }, []);

  const seenInvestigationRequestRef = useRef(0);
  const [trackedProjectId, setTrackedProjectId] = useState(context.projectId ?? null);
  const [sessionDropTick, setSessionDropTick] = useState(0);
  const activeProjectId = context.projectId ?? null;
  if (trackedProjectId !== activeProjectId) {
    setTrackedProjectId(activeProjectId);
    const localSessionId = session?.sessionId;
    if (localSessionId && context.sessionId !== localSessionId) {
      setSessionDropTick((tick) => tick + 1);
      setPlaybackState("stopped");
      setSession(null);
      setChannels([]);
      setSelectedChannelIds([]);
      setBreakpoints([]);
      setCompositeBreakpoints([]);
      setAxisPins({});
      setEffectiveRightAxisIds([]);
      setWizardOpen(false);
      setLoadError(null);
    }
  }

  useEffect(() => {
    if (sessionDropTick === 0) return;
    cancelLocalPlayback();
    playbackSessionRef.current = null;
    playbackStateRef.current = "stopped";
    loadVersionRef.current += 1;
    loadedSessionIdRef.current = null;
    activeSessionIdRef.current = null;
  }, [sessionDropTick]);

  // Fetch the immediately preceding *recorded* sample only when inspecting Values.
  // A cancellation flag prevents older seeks from publishing stale comparisons.
  const watchSessionId = session?.sessionId;
  const watchSequence = session?.currentSample?.sequence;
  const watchSample = session?.currentSample ?? null;

  if (seenSessionRef.current !== (watchSessionId ?? null)) {
    seenSamplesRef.current.clear();
    seenSessionRef.current = watchSessionId ?? null;
  }

  const rememberSample = (sample: CurrentSample | SampleRangeItem | null | undefined) => {
    if (!watchSessionId || !sample) return;
    const cache = seenSamplesRef.current;
    const key = `${watchSessionId}:${sample.sequence}`;
    cache.delete(key);
    cache.set(key, sample);
    while (cache.size > 64) {
      const oldest = cache.keys().next().value;
      if (oldest === undefined) break;
      cache.delete(oldest);
    }
  };

  rememberSample(watchSample);
  if (previousSampleResult && previousSampleResult.sessionId === watchSessionId) {
    rememberSample(previousSampleResult.sample);
  }

  const cachedPrevious =
    watchSessionId && watchSequence != null && watchSequence > 0
      ? seenSamplesRef.current.get(`${watchSessionId}:${watchSequence - 1}`) ?? null
      : null;

  useEffect(() => {
    if (inspectorTab !== 2 || !watchSessionId || watchSequence == null || watchSequence <= 0) {
      return;
    }

    if (seenSamplesRef.current.has(`${watchSessionId}:${watchSequence - 1}`)) {
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

  useEffect(() => {
    if (!session?.recordingId || session.currentSample == null) return;
    saveResumeSequence(session.recordingId, session.currentSample.sequence);
  }, [session?.recordingId, session?.currentSample?.sequence]);

  const breakpointCoverageKey = [
    breakpoints.map(item => item.breakpointId).join(","),
    compositeBreakpoints.map(item => item.breakpointId).join(","),
  ].join("|");

  useEffect(() => {
    const sessionId = session?.sessionId;
    if (!sessionId || breakpointCoverageKey === "|") {
      setBreakpointCoverage([]);
      return;
    }

    let cancelled = false;
    void breakpointApi.coverage(sessionId)
      .then(response => {
        if (!cancelled) setBreakpointCoverage(response.coverages ?? []);
      })
      .catch(() => {
        if (!cancelled) setBreakpointCoverage([]);
      });

    return () => {
      cancelled = true;
    };
  }, [session?.sessionId, breakpointCoverageKey]);

  const fetchedPreviousSample =
    previousSampleResult != null &&
    previousSampleResult.sessionId === watchSessionId &&
    previousSampleResult.currentSequence === watchSequence &&
    previousSampleResult.sample.sequence + 1 === watchSequence
      ? previousSampleResult.sample
      : null;
  const previousWatchSample = cachedPrevious ?? fetchedPreviousSample;

  useEffect(() => {
    const sessionId = session?.sessionId;
    if (!sessionId) {
      setRecordingTimeline(null);
      return;
    }

    let cancelled = false;
    setTimelineLoading(true);

    void recordingTimelineApi
      .getForSession(sessionId)
      .then((timeline) => {
        if (!cancelled) {
          setRecordingTimeline(timeline);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setRecordingTimeline({ events: [], stages: [] });
        }
      })
      .finally(() => {
        if (!cancelled) {
          setTimelineLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [session?.sessionId]);

  useEffect(() => {
    channelExtentsSessionRef.current = null;
    setChannelExtents({});
  }, [session?.sessionId]);

  useEffect(() => {
    if (!breakpointDialogOpen && !compositeDialogOpen) return;
    const sessionId = session?.sessionId;
    const sampleCount = session?.sampleCount ?? 0;
    if (!sessionId || sampleCount < 1) return;
    if (channelExtentsSessionRef.current === sessionId) return;

    channelExtentsSessionRef.current = sessionId;
    let cancelled = false;
    let settled = false;
    void analysisApi.getWindow(sessionId, 0, sampleCount - 1)
      .then((analysis) => {
        if (cancelled) return;
        settled = true;
        const extents: Record<string, ChannelExtent> = {};
        for (const channel of analysis.channels) {
          if (channel.minimum == null || channel.maximum == null) continue;
          if (!Number.isFinite(channel.minimum) || !Number.isFinite(channel.maximum)) continue;
          extents[channel.channelId] = {
            minimum: channel.minimum,
            maximum: channel.maximum,
          };
        }
        setChannelExtents(extents);
      })
      .catch(() => {
        if (cancelled) return;
        settled = true;
        setChannelExtents({});
      });

    return () => {
      cancelled = true;
      if (!settled && channelExtentsSessionRef.current === sessionId) {
        channelExtentsSessionRef.current = null;
      }
    };
  }, [
    breakpointDialogOpen,
    compositeDialogOpen,
    session?.sessionId,
    session?.sampleCount,
  ]);

  // --------------------------------------------------
  // Recording loading
  // --------------------------------------------------

  const loadRecording = async (
    sessionId: string,
    options?: { selectChannels?: "default" | "none"; resumeCursor?: boolean },
  ): Promise<ChannelMetadata[] | null> => {
    cancelLocalPlayback();
    playbackSessionRef.current = null;
    playbackStateRef.current = "stopped";
    setPlaybackState("stopped");
    if (sessionId !== loadedSessionIdRef.current) {
      setReplayCursorVisible(false);
    }
    const version = ++loadVersionRef.current;

    setLoading(true);
    setLoadError(null);
    setBreakpointError(null);

    try {
      const [loadedSession, loadedChannels, loadedBreakpoints] =
        await Promise.all([
          recordingApi.getSession(sessionId),
          recordingApi.getChannels(sessionId),
          breakpointApi.list(sessionId),
        ]);

      if (version !== loadVersionRef.current) {
        return null;
      }

      const isSameSession =
        loadedSessionIdRef.current === loadedSession.sessionId;

      activeSessionIdRef.current = loadedSession.sessionId;

      setSession(loadedSession);
      setRecordingSession(
        loadedSession.sessionId,
        loadedSession.recordingId,
        loadedSession.recordingName,
      );
      if (!isSameSession) {
        clearInvestigation();
      }
      setChannels(loadedChannels);
      setBreakpoints(loadedBreakpoints.breakpoints);
      setCompositeBreakpoints(loadedBreakpoints.compositeBreakpoints ?? []);
      setBreakpointHitsRefreshKey((value) => value + 1);

      setSelectedChannelIds((current) => {
        if (!isSameSession) {
          if (options?.selectChannels === "none") return [];
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
        setAxisPins({});
        setEffectiveRightAxisIds([]);
      }

      loadedSessionIdRef.current = loadedSession.sessionId;

      if (!isSameSession && options?.resumeCursor !== false) {
        const resumeSequence = getResumeSequence(loadedSession.recordingId);
        if (
          resumeSequence !== null &&
          resumeSequence >= 0 &&
          resumeSequence < loadedSession.sampleCount &&
          resumeSequence !== loadedSession.currentSample.sequence
        ) {
          const seekResult = await replayApi.seek(
            loadedSession.sessionId,
            resumeSequence,
          );
          if (version === loadVersionRef.current) {
            setSession(seekResult.session);
          }
        }
      }

      if (!isSameSession) {
        setBreakpointHit(null);
      }

      console.debug("Recording loaded:", {
        sessionId: loadedSession.sessionId,
        sampleCount: loadedSession.sampleCount,
        channelCount: loadedChannels.length,
        breakpointCount: loadedBreakpoints.breakpoints.length +
          (loadedBreakpoints.compositeBreakpoints?.length ?? 0),
      });

      return loadedChannels;
    } catch (error) {
      if (version === loadVersionRef.current) {
        setLoadError(
          errorMessage(error, "Unable to load recording."),
        );
      }

      return null;
    } finally {
      if (version === loadVersionRef.current) {
        setLoading(false);
      }
    }
  };

  const [sessionNavDiscardOpen, setSessionNavDiscardOpen] = useState(false);
  const pendingSessionNavRef = useRef<SessionNavigationAction | null>(null);

  const executeImportSuccess = async (
    result: CreateSessionResponse,
    destination: "dashboard" | "recordings" = "dashboard",
  ) => {
    setImportSuccess(null);

    const loaded = await loadRecording(result.sessionId);

    if (loaded) {
      setImportSuccess("Recording imported successfully.");
      onSelectSection(destination);
    }
  };

  const runSessionNavigation = async (action: SessionNavigationAction) => {
    if (action.kind === "open") {
      await executeOpenInvestigation(action.investigationId, action.projectId);
      return;
    }
    await executeImportSuccess(action.result, action.destination ?? "dashboard");
  };

  const requestSessionNavigation = (action: SessionNavigationAction) => {
    if (isInvestigationUiDirty()) {
      pendingSessionNavRef.current = action;
      setSessionNavDiscardOpen(true);
      return;
    }
    void runSessionNavigation(action);
  };

  const handleImportSuccess = (result: CreateSessionResponse) => {
    requestSessionNavigation({ kind: "import", result });
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
  ): Promise<SessionState | null> => {
    if (
      !session ||
      seekInProgressRef.current ||
      breakpointInProgressRef.current ||
      loading
    ) {
      return null;
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
      return updatedSession;
    } catch (error) {
      if (activeSessionIdRef.current === sessionId) {
        setLoadError(errorMessage(error, fallbackError));
      }
      return null;
    } finally {
      seekInProgressRef.current = false;
      setSeeking(false);
    }
  };

  const handleStepForward = async () => {
    setReplayCursorVisible(true);
    await executeReplay(
      async (sessionId) =>
        (await replayApi.stepForward(sessionId)).session,
      "Unable to step forward.",
    );
  };

  const handleStepBackward = async () => {
    setReplayCursorVisible(true);
    await executeReplay(
      async (sessionId) =>
        (await replayApi.stepBackward(sessionId)).session,
      "Unable to step backward.",
    );
  };

  const rememberBreakpointStop = (stop: BreakpointStopDetails | null | undefined) => {
    if (!stop) return;
    const hit = stop.hits?.[0];
    const label = (() => {
      if (!hit) return null;
      if (hit.operator === "TimelineEvent" || hit.operator === "TimelineStage") {
        const kind = hit.operator === "TimelineStage" ? "Stage" : "Event";
        return `${kind} ${hit.observedText?.trim() || "Any"}`;
      }
      const channelName = channelsRef.current.find((channel) => channel.channelId === hit.channelId)?.name;
      return [channelName, hit.operator, hit.threshold]
        .filter((part) => part !== null && part !== undefined && part !== "")
        .join(" ");
    })();
    setBreakpointHit({
      sequence: stop.sequence,
      breakpointId: hit?.breakpointId ?? null,
      label,
    });
    setInspectorTab(2);
  };

  const handleReset = () => {
    setBreakpointHit(null);
    return executeReplay(
      async (sessionId) =>
        (await replayApi.reset(sessionId)).session,
      "Unable to reset replay.",
    );
  };

  // Chart click-to-seek uses the same playback-aware transaction as dragging.
  // A running recording resumes from the selected sample; a paused or stopped
  // recording stays in its previous playback state.
  const handleSeek = async (sequence: number) => {
    if (!session || scrubRef.current || seekInProgressRef.current ||
        playbackBusyRef.current || breakpointInProgressRef.current || loading) {
      return;
    }

    setReplayCursorVisible(true);
    handleScrubStart();
    await handleScrubEnd(sequence);
  };

  const handleContinue = async () => {
    setReplayCursorVisible(true);
    await executeReplay(async (sessionId) => {
      const response = await replayApi.continue(sessionId);

      if (!response.executed) {
        console.debug(
          "Continue did not execute: no active breakpoints.",
        );
      } else if (response.hitBreakpoint) {
        rememberBreakpointStop(response.stop);
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

  const handleChangeNavigation = async (
    channelId: string,
    direction: "next" | "previous",
  ) => {
    setReplayCursorVisible(true);
    await executeReplay(async (sessionId) => {
      const response = direction === "next"
        ? await replayApi.nextChange(sessionId, channelId)
        : await replayApi.previousChange(sessionId, channelId);
      if (!response.foundChange) {
        console.debug(`No ${direction} transition found for channel ${channelId}.`);
      }
      return response.session;
    }, `Unable to navigate to ${direction} change.`);
  };

  // --------------------------------------------------
  // Continuous playback (forward, 1x recorded time)
  // --------------------------------------------------

  const handlePlay = async (resumeSession?: SessionState) => {
    const targetSession = resumeSession ?? session;
    if (!targetSession || playbackRunningRef.current || playbackBusyRef.current ||
        seekInProgressRef.current || breakpointInProgressRef.current || loading ||
        targetSession.sampleCount === 0 ||
        (targetSession.currentSample?.sequence ?? 0) >= targetSession.sampleCount - 1) {
      return;
    }

    const sessionId = targetSession.sessionId;
    const epoch = ++playbackEpochRef.current;
    playbackBusyRef.current = true;
    setPlaybackBusy(true);
    setLoadError(null);

    try {
      const { generation } = await replayApi.startPlayback(sessionId);
      if (epoch !== playbackEpochRef.current ||
          activeSessionIdRef.current !== sessionId) return;

      playbackSessionRef.current = sessionId;
      playbackRunningRef.current = true;
      playbackStateRef.current = "playing";
      setPlaybackState("playing");
      setReplayCursorVisible(true);

       // V2: schedule against an absolute recorded-time clock. Network and
       // rendering latency do not accumulate into the replay timeline.
       // The backend evaluates every intermediate sample for breakpoints.
       const maxBatch = 64;
       const fetchSize = 256;
       const minimumUiIntervalMs = 16;
       const clockStartMs = performance.now();
       let recordingStartNs: bigint | null = null;
       let cache = new Map<number, bigint>();
       let cacheStart = -1;
       let cacheEnd = -1;

       const timestampAt = async (sequence: number): Promise<bigint> => {
         if (sequence < cacheStart || sequence >= cacheEnd || !cache.has(sequence)) {
           const start = Math.max(0, sequence - 1);
           const result = await recordingApi.getSamples(sessionId, start, fetchSize);
           if (epoch !== playbackEpochRef.current) throw new Error("Playback cancelled.");
           cache = new Map(result.samples.map(sample => [
             sample.sequence, BigInt(sample.timestampNanoseconds),
           ]));
           cacheStart = start;
           cacheEnd = start + fetchSize;
         }
         const timestamp = cache.get(sequence);
         if (timestamp === undefined) throw new Error(`Recorded sample ${sequence} was not found.`);
         return timestamp;
       };

       const advance = async (current: SessionState): Promise<void> => {
          console.log("PLAYBACK TICK", {
            epoch,
            currentEpoch: playbackEpochRef.current,
            sequence: current.currentSample?.sequence,
            running: playbackRunningRef.current,
          });

         if (epoch !== playbackEpochRef.current) return;
         try {
           const sequence = current.currentSample?.sequence ?? 0;
           if (recordingStartNs === null) {
             recordingStartNs = await timestampAt(sequence);
           }
           if (epoch !== playbackEpochRef.current) return;

           // Compare relative nanoseconds using BigInt to avoid precision loss
           // for absolute timestamps. Milliseconds are sufficient for timers.
           const elapsedMs = performance.now() - clockStartMs;
           const dueNs = recordingStartNs + BigInt(Math.floor(elapsedMs * 1_000_000));
           let count = 0;
           let nextTimestamp: bigint | null = null;
           const lastSequence = current.sampleCount - 1;

           while (count < maxBatch && sequence + count < lastSequence) {
             const timestamp = await timestampAt(sequence + count + 1);
             if (epoch !== playbackEpochRef.current) return;
             if (timestamp > dueNs) {
               nextTimestamp = timestamp;
               break;
             }
             count++;
           }

           if (count === 0) {
            console.log("PLAYBACK SCHEDULER", {
              sequence,
              elapsedMs,
              recordingStartNs: recordingStartNs.toString(),
              dueNs: dueNs.toString(),
              nextTimestamp: nextTimestamp?.toString(),
              count,
              delaySeconds:
                nextTimestamp !== null
                  ? Number((nextTimestamp - dueNs) / 1_000_000n) / 1000
                  : null,
            });
             if (sequence >= lastSequence) {
               playbackRunningRef.current = false;
               playbackStateRef.current = "stopped";
               setPlaybackState("stopped");
               return;
             }
             if (nextTimestamp === null) nextTimestamp = await timestampAt(sequence + 1);
             if (epoch !== playbackEpochRef.current) return;
             const remainingNs = nextTimestamp - dueNs;
             const delayMs = remainingNs > 0n ? Number(remainingNs / 1_000_000n) : 0;
             const scheduledDelay = Math.max(
              1,
              Math.min(delayMs, 2_147_483_647)
            );

            console.log("PLAYBACK TIMER SCHEDULED", {
              sequence,
              scheduledDelay
            });

            playbackTimerRef.current = setTimeout(() => {
              console.log("PLAYBACK TIMER FIRED", {
                sequence,
                epoch,
                currentEpoch: playbackEpochRef.current
              });

              playbackTimerRef.current = null;
              void advance(current);
            }, scheduledDelay);
            return;
           }

           const response = await replayApi.advancePlayback(sessionId, generation, count);
           if (epoch !== playbackEpochRef.current ||
               activeSessionIdRef.current !== sessionId) return;

           setSession(previous => previous?.sessionId === sessionId
             ? response.session : previous);

           if (response.hitBreakpoint || response.endOfRecording ||
               response.samplesAdvanced === 0) {
             playbackRunningRef.current = false;
             playbackStateRef.current = response.hitBreakpoint ? "paused" : "stopped";
             setPlaybackState(playbackStateRef.current);
             if (response.hitBreakpoint) {
               rememberBreakpointStop(response.stop);
               console.debug("Playback stopped at breakpoint:", response.stop);
             }
             return;
           }

           // Keep rendering responsive even when catching up on dense data.
           playbackTimerRef.current = setTimeout(() => {
             playbackTimerRef.current = null;
             void advance(response.session);
           }, minimumUiIntervalMs);
         } catch (error) {
           if (epoch !== playbackEpochRef.current) return;
           cancelLocalPlayback();
           playbackStateRef.current = "paused";
           setPlaybackState("paused");
           setLoadError(errorMessage(error, "Unable to advance playback."));
         }
       };

      void advance(targetSession);    } catch (error) {
      if (epoch === playbackEpochRef.current) {
        playbackStateRef.current = "stopped";
        setPlaybackState("stopped");
        setLoadError(errorMessage(error, "Unable to start playback."));
      }
    } finally {
      playbackBusyRef.current = false;
      setPlaybackBusy(false);
    }
  };

  const handlePause = async () => {
    if (!session || !playbackRunningRef.current || playbackBusyRef.current) return;
    const sessionId = session.sessionId;
    cancelLocalPlayback();
    playbackBusyRef.current = true;
    setPlaybackBusy(true);
    try {
      const updated = await replayApi.pausePlayback(sessionId);
      if (activeSessionIdRef.current === sessionId) {
        setSession(updated);
        playbackStateRef.current = "paused";
        setPlaybackState("paused");
      }
    } catch (error) {
      if (activeSessionIdRef.current === sessionId)
        setLoadError(errorMessage(error, "Unable to pause playback."));
    } finally {
      playbackBusyRef.current = false;
      setPlaybackBusy(false);
    }
  };

  const handleStop = async () => {
    if (!session || playbackBusyRef.current) return;
    const sessionId = session.sessionId;
    cancelLocalPlayback();
    playbackBusyRef.current = true;
    setPlaybackBusy(true);
    try {
      const updated = await replayApi.stopPlayback(sessionId);
      if (activeSessionIdRef.current === sessionId) {
        setSession(updated);
        playbackStateRef.current = "stopped";
        setPlaybackState("stopped");
      }
    } catch (error) {
      if (activeSessionIdRef.current === sessionId)
        setLoadError(errorMessage(error, "Unable to stop playback."));
    } finally {
      playbackBusyRef.current = false;
      setPlaybackBusy(false);
    }
  };

  // Scrubbing is a transaction: cancel local scheduling, invalidate the old
  // backend generation, seek once on release, then resume only if playing.
  const handleScrubStart = () => {
    if (!session || scrubRef.current) return;
    const sessionId = session.sessionId;
    const wasPlaying = playbackRunningRef.current;
    cancelLocalPlayback();
    if (wasPlaying) {
      playbackStateRef.current = "paused";
      setPlaybackState("paused");
    }
    // The backend pause is serialized before seek, even if release is immediate.
    const pause = wasPlaying
      ? replayApi.pausePlayback(sessionId).then(() => undefined)
      : Promise.resolve();
    scrubRef.current = { sessionId, wasPlaying, pause };
  };

  const handleScrubEnd = async (sequence: number) => {
    const scrub = scrubRef.current;
    if (!scrub) return;
    scrubRef.current = null;
    let resumed = false;
    playbackBusyRef.current = true;
    seekInProgressRef.current = true;
    setPlaybackBusy(true);
    setSeeking(true);
    try {
      await scrub.pause;
      if (activeSessionIdRef.current !== scrub.sessionId) return;
      const result = await replayApi.seek(scrub.sessionId, sequence);
      if (activeSessionIdRef.current !== scrub.sessionId) return;
      setSession(result.session);
      // Keep stopped/paused semantics if the recording wasn't playing.
      if (!scrub.wasPlaying) return;
      playbackBusyRef.current = false;
      seekInProgressRef.current = false;
      setPlaybackBusy(false);
      setSeeking(false);
      await handlePlay(result.session);
      resumed = true;
    } catch (error) {
      if (activeSessionIdRef.current === scrub.sessionId) {
        setLoadError(errorMessage(error, "Unable to scrub recording."));
        playbackStateRef.current = "paused";
        setPlaybackState("paused");
      }
    } finally {
      if (!resumed) {
        playbackBusyRef.current = false;
        seekInProgressRef.current = false;
        setPlaybackBusy(false);
        setSeeking(false);
      }
    }
  };

  // --------------------------------------------------
  // Breakpoint operations
  // --------------------------------------------------

  const refreshBreakpoints = async (sessionId: string) => {
    const response = await breakpointApi.list(sessionId);

    if (activeSessionIdRef.current === sessionId) {
      setBreakpoints(response.breakpoints);
      setBreakpointHitsRefreshKey((value) => value + 1);
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

  const handleRunToBreakpoint = async (
    breakpointId: string,
    direction: "forward" | "backward",
  ) => {
    setReplayCursorVisible(true);
    await executeReplay(async (sessionId) => {
      const response = await breakpointApi.runTo(sessionId, breakpointId, direction);
      setFocusedBreakpointId(breakpointId);
      if (response.hitBreakpoint) {
        rememberBreakpointStop(response.stop);
      } else {
        setInspectorTab(1);
      }
      if (!response.hitBreakpoint) {
        setBreakpointError(
          direction === "forward"
            ? "No later occurrences for this breakpoint."
            : "No earlier occurrences for this breakpoint.",
        );
      } else {
        setBreakpointError(null);
        const hitSequence = response.session.currentSample?.sequence;
        if (hitSequence != null) {
          setBookmarkSnackMessage(`Stopped at sample ${hitSequence.toLocaleString()}`);
          setBookmarkSnackPrompt(true);
          setBookmarkSnackOpen(true);
          setViewportFocusSequence(hitSequence);
          setViewportFocusRequestId((value) => value + 1);
        }
      }
      return response.session;
    }, "Unable to navigate to breakpoint occurrence.");
  };

  const handleOpenBreakpointDialog = (draft: BreakpointDialogDraft) => {
    setBreakpointDraft(draft);
    setBreakpointDialogOpen(true);
    setInspectorTab(1);
  };

  const handleCreateBreakpoint = async (
    request: CreateBreakpointRequest,
    options?: { jumpToNext?: boolean },
  ) => {
    let createdBreakpointId: string | null = null;
    const succeeded = await executeBreakpointMutation(
      async (sessionId) => {
        const created = await breakpointApi.create(sessionId, request);
        createdBreakpointId = created.breakpointId;
      },
    );

    if (succeeded) {
      setBreakpointDialogOpen(false);
      setBreakpointDraft(null);
      setInspectorTab(1);
      if (createdBreakpointId) {
        setFocusedBreakpointId(createdBreakpointId);
        if (options?.jumpToNext) {
          await handleRunToBreakpoint(createdBreakpointId, "forward");
        }
      }
    }
  };

  const handleCreateMissingDataBreakpoint = async (
    request: CreateMissingDataBreakpointRequest,
  ) => {
    let createdBreakpointId: string | null = null;
    const succeeded = await executeBreakpointMutation(
      async (sessionId) => {
        const created = await breakpointApi.createMissingData(sessionId, request);
        createdBreakpointId = created.breakpointId;
      },
    );

    if (succeeded) {
      setBreakpointDialogOpen(false);
      setBreakpointDraft(null);
      setInspectorTab(1);
      if (createdBreakpointId) {
        setFocusedBreakpointId(createdBreakpointId);
        await handleRunToBreakpoint(createdBreakpointId, "forward");
      }
    }
  };

  const handleCreateTimelineBreakpoint = async (
    request: CreateTimelineBreakpointRequest,
  ) => {
    let createdBreakpointId: string | null = null;
    const succeeded = await executeBreakpointMutation(
      async (sessionId) => {
        const created = await breakpointApi.createTimeline(sessionId, request);
        createdBreakpointId = created.breakpointId;
      },
    );

    if (succeeded) {
      setBreakpointDialogOpen(false);
      setBreakpointDraft(null);
      setInspectorTab(1);
      if (createdBreakpointId) {
        setFocusedBreakpointId(createdBreakpointId);
        await handleRunToBreakpoint(createdBreakpointId, "forward");
      }
    }
  };

  const handleQuickBookmark = async (payload?: {
    sequence?: number;
    timestampNanoseconds?: number | string;
    title?: string;
    annotation?: string;
  }) => {
    if (!session?.currentSample) return;

    const sequence = payload?.sequence ?? session.currentSample.sequence;
    const timestampNanoseconds =
      payload?.timestampNanoseconds ?? session.currentSample.timestampNanoseconds;

    const result = await tryQuickBookmark({
      recordingName: session.recordingName,
      investigationId: context.investigationId,
      projectId: context.projectId,
      sampleSequence: sequence,
      timestampNanoseconds,
      title: payload?.title,
      annotation: payload?.annotation,
      ensureInvestigation: () => ensureSavedInvestigation(),
    });

    if (!result.ok) {
      setInvestigationSetupHint(result.message);
      onSelectSection("investigations");
      return;
    }

    setBookmarkRevision((value) => value + 1);
    setInvestigationSetupHint(null);
    setBookmarkSnackMessage(`Bookmarked sample ${sequence.toLocaleString()}`);
    setBookmarkSnackPrompt(false);
    setBookmarkSnackOpen(true);
  };

  const activeInvestigationSelection = session
    ? loadInvestigationSelection(session.recordingName)
    : null;

  const chartInvestigationBookmarks = useActiveInvestigationBookmarks(
    session?.recordingName ?? null,
    context.investigationId,
    context.projectId,
    bookmarkRevision,
  );

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
      busy={loading || seeking || breakpointBusy || playbackBusy}
      playbackState={playbackState}
      onPlay={() => void handlePlay()}
      onPause={() => void handlePause()}
      onStop={() => void handleStop()}
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
        <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>
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
          </Typography>
        )}

        {session && (context.investigationName || activeInvestigationSelection) && (
          <Chip
            size="small"
            label={
              context.investigationName
                ? `Investigation: ${context.investigationName}`
                : "Investigation active"
            }
            variant="outlined"
            color="primary"
            onClick={() => onSelectSection("investigations")}
            sx={{ mt: 0.5, maxWidth: "100%" }}
          />
        )}
      </Box>

      <Stack direction="row" spacing={1} sx={{ flexShrink: 0 }}>
        {session && (
          <>
          <Button
            size="small"
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={handleRefresh}
            disabled={
              loading || breakpointBusy || playbackRunningRef.current || playbackBusy
            }
          >
            Refresh
          </Button>
          <Button
            size="small"
            variant="outlined"
            startIcon={<AddIcon />}
            onClick={() => {
              setBreakpointDraft(null);
              setBreakpointDialogOpen(true);
              setInspectorTab(1);
            }}
            disabled={
              loading || breakpointBusy || playbackRunningRef.current || playbackBusy
              || channels.length === 0
            }
          >
            Add breakpoint
          </Button>
          <Button
            size="small"
            variant="outlined"
            onClick={() => {
              setSaveAsProjectError("");
              setSaveAsProjectOpen(true);
            }}
            disabled={
              loading || breakpointBusy || playbackRunningRef.current || playbackBusy || saveAsProjectBusy
            }
          >
            Save as Project
          </Button>
          </>
        )}

        <Button
          size="small"
          variant="contained"
          startIcon={<UploadFileIcon />}
          onClick={() => requestCsvWizardRef.current(null, true)}
          disabled={
            loading || breakpointBusy || playbackRunningRef.current || playbackBusy
          }
        >
          Import CSV
        </Button>
      </Stack>
    </Box>
  );

  const rememberInvestigationUiBaseline = useCallback(
    (investigationId: string, uiState: InvestigationUiState) => {
      investigationUiBaselineRef.current = {
        investigationId,
        snapshot: snapshotInvestigationUiState(uiState),
      };
    },
    [],
  );

  const channelNamesFor = useCallback((ids: string[]) => ids.flatMap((id) => {
    const channel = channels.find((item) => item.channelId === id);
    return channel ? [channel.name] : [];
  }), [channels]);

  const breakpointHitPayload = useCallback((): InvestigationHit | null => {
    if (!breakpointHit) return null;
    return {
      sequenceNumber: breakpointHit.sequence,
      breakpointId: breakpointHit.breakpointId,
      label: breakpointHit.label,
    };
  }, [breakpointHit]);

  const applyInvestigationUiState = useCallback((
    uiState: InvestigationUiState,
    channelList?: ChannelMetadata[],
  ): InvestigationUiState => {
    const source = channelList ?? channels;
    const selected = resolveSavedChannelIds(
      source,
      uiState.selectedChannelNames,
      uiState.selectedChannelIds,
    );
    const nextSelected = selected
      ?? source.slice(0, 3).map((channel) => channel.channelId);
    setSelectedChannelIds(nextSelected);
    const right = resolveSavedChannelIds(
      source,
      uiState.rightAxisChannelNames,
      uiState.rightAxisChannelIds,
    ) ?? [];
    const currentIds = new Set(source.map((channel) => channel.channelId));
    const savedPins = uiState.axisPins ?? {};
    const pinsStillValid = Object.keys(savedPins).length > 0
      && Object.keys(savedPins).every((id) => currentIds.has(id));
    const pins = pinsStillValid ? savedPins as AxisPins : axisPinsFromLegacyRightAxis(right);
    setAxisPins(pins);
    const viewport = uiState.chartViewport ?? null;
    if (viewport) {
      setRestoreChartViewport(viewport);
      setRestoreChartViewportRequestId((value) => value + 1);
    }
    const namesFor = (ids: string[]) => ids.flatMap((id) => {
      const channel = source.find((item) => item.channelId === id);
      return channel ? [channel.name] : [];
    });
    return {
      selectedChannelIds: nextSelected,
      rightAxisChannelIds: right,
      selectedChannelNames: namesFor(nextSelected),
      rightAxisChannelNames: namesFor(right),
      axisPins: pins,
      chartViewport: viewport,
    };
  }, [channels]);

  const resolveExportUiState = useCallback((): InvestigationUiState => ({
    selectedChannelIds,
    rightAxisChannelIds: effectiveRightAxisIds,
    selectedChannelNames: channelNamesFor(selectedChannelIds),
    rightAxisChannelNames: channelNamesFor(effectiveRightAxisIds),
    axisPins,
    chartViewport: chartViewportReaderRef.current?.read() ?? null,
  }), [selectedChannelIds, effectiveRightAxisIds, axisPins, channelNamesFor]);

  const handleSaveAsProject = async (projectName: string, rootPath: string) => {
    if (!session) return;

    setSaveAsProjectBusy(true);
    setSaveAsProjectError("");

    try {
      const project = await projectApi.createProject({ projectName, rootPath });
      const exported = await investigationApi.exportSession(session.sessionId, {
        name: projectName,
        projectId: project.projectId,
        uiState: resolveExportUiState(),
        breakpointHit: breakpointHitPayload(),
      });
      setActiveProject(project, { keepSession: true });
      setActiveInvestigation({
        investigationId: exported.investigationId,
        investigationName: projectName,
        projectId: project.projectId,
      });
      await refreshProjectInvestigations();
      saveInvestigationSelection({
        recordingName: session.recordingName,
        projectId: project.projectId,
        investigationId: exported.investigationId,
      });
      rememberInvestigationUiBaseline(exported.investigationId, resolveExportUiState());
      setSaveAsProjectOpen(false);
      setImportSuccess(`Saved as project ${project.projectName}.`);
    } catch (error) {
      setSaveAsProjectError(errorMessage(error, "Unable to save this session as a project."));
    } finally {
      setSaveAsProjectBusy(false);
    }
  };

  const isInvestigationUiDirty = useCallback(() => {
    const investigationId = context.investigationId;
    if (!investigationId || !session) return false;
    const baseline = investigationUiBaselineRef.current;
    if (!baseline || baseline.investigationId !== investigationId) return false;
    return (
      baseline.snapshot !==
      snapshotInvestigationUiState(resolveExportUiState())
    );
  }, [context.investigationId, session, resolveExportUiState]);

  const openCsvWizard = useCallback((file: File | null, confirmTimestamp: boolean) => {
    setLoadError(null);
    setWizardSessionId(null);
    setWizardFile(file);
    setWizardConfirmTimestamp(confirmTimestamp);
    setWizardOpen(true);
  }, []);

  const requestCsvWizard = useCallback((file: File | null, confirmTimestamp: boolean) => {
    if (file && !isCsvFile(file)) {
      setLoadError("Drop a .csv file to open a session.");
      return;
    }
    if (wizardOpen || loading) return;

    if (isInvestigationUiDirty()) {
      pendingWizardRef.current = { file, confirmTimestamp };
      setSessionNavDiscardOpen(true);
      return;
    }

    openCsvWizard(file, confirmTimestamp);
  }, [wizardOpen, loading, isInvestigationUiDirty, openCsvWizard]);

  useEffect(() => {
    requestCsvWizardRef.current = requestCsvWizard;
  }, [requestCsvWizard]);

  const handleWizardImported = async (result: CreateSessionResponse) => {
    const loaded = await loadRecording(result.sessionId, { selectChannels: "none" });
    if (loaded) setWizardSessionId(result.sessionId);
    return loaded !== null;
  };

  const registerSessionInActiveProject = async () => {
    if (!activeProject || !session) return;
    const exported = await investigationApi.exportSession(session.sessionId, {
      investigationId: context.investigationId,
      name: session.recordingName,
      projectId: activeProject.projectId,
      uiState: resolveExportUiState(),
      breakpointHit: breakpointHitPayload(),
    });
    setActiveInvestigation({
      investigationId: exported.investigationId,
      investigationName: context.investigationName ?? session.recordingName,
      projectId: activeProject.projectId,
    });
    await refreshProjectInvestigations();
  };

  const closeCsvWizard = (imported: boolean) => {
    setWizardOpen(false);
    setWizardFile(null);
    setBreakpointDialogOpen(false);
    setBreakpointDraft(null);
    setCompositeDialogOpen(false);
    if (imported) {
      void registerSessionInActiveProject().catch((error) => {
        setLoadError(error instanceof Error ? error.message : "Unable to save this recording in the active project.");
      });
      onSelectSection("recordings");
    }
  };

  const completeCsvWizard = async (mode: CsvWizardFinish) => {
    try {
      await registerSessionInActiveProject();
    } catch (error) {
      setLoadError(error instanceof Error ? error.message : "Unable to save this recording in the active project.");
    }
    const breakpointCount = breakpoints.length + compositeBreakpoints.length;
    setWizardOpen(false);
    setWizardFile(null);
    setBreakpointDialogOpen(false);
    setBreakpointDraft(null);
    setCompositeDialogOpen(false);
    onSelectSection("recordings");
    setReplayCursorVisible(true);
    setInspectorTab(breakpointCount > 0 ? 1 : 0);
    if (mode !== "run") return;

    const reset = await handleReset();
    if (!reset) return;
    if (breakpointCount > 0) {
      await handleContinue();
      return;
    }
    await handlePlay(reset);
  };

  async function ensureSavedInvestigation(): Promise<{
    investigationId: string;
    projectId: string | null;
  } | null> {
    if (!session) return null;
    if (context.investigationId) {
      return {
        investigationId: context.investigationId,
        projectId: context.projectId,
      };
    }

    const uiState = resolveExportUiState();
    const exported = await investigationApi.exportSession(session.sessionId, {
      name: session.recordingName,
      projectId: context.projectId,
      uiState,
      breakpointHit: breakpointHitPayload(),
    });
    setActiveInvestigation({
      investigationId: exported.investigationId,
      investigationName: session.recordingName,
      projectId: context.projectId,
    });
    saveInvestigationSelection({
      recordingName: session.recordingName,
      projectId: context.projectId ?? "",
      investigationId: exported.investigationId,
    });
    rememberInvestigationUiBaseline(exported.investigationId, uiState);
    return {
      investigationId: exported.investigationId,
      projectId: context.projectId,
    };
  }

  const registerChartViewportReader = useCallback(
    (reader: ChartViewportReader | null) => {
      chartViewportReaderRef.current = reader;
    },
    [],
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
            axisPins={axisPins}
            onAxisPinChange={handleAxisPinChange}
            onClearAxisPins={clearAllAxisPins}
            onEffectiveRightAxisChange={setEffectiveRightAxisIds}
            onSeriesColorsChange={setSeriesColors}
            currentSequence={
              session.currentSample?.sequence ?? null
            }
            showCursor={replayCursorVisible}
            playbackState={playbackState}
            slidingWindow={slidingWindow}
            currentTimestampNanoseconds={session.currentSample?.timestampNanoseconds ?? null}
            timestampOrigin={session.timestampOrigin ?? "Unix"}
            onSeek={handleSeek}
            onScrubStart={handleScrubStart}
            onScrubEnd={(sequence) => void handleScrubEnd(sequence)}
            onOpenBreakpointDialog={handleOpenBreakpointDialog}
            onQuickBookmarkSample={(payload) => void handleQuickBookmark(payload)}
            investigationBookmarks={chartInvestigationBookmarks}
            timelineEvents={recordingTimeline?.events ?? []}
            timelineStages={recordingTimeline?.stages ?? []}
            viewportFocusRequestId={viewportFocusRequestId}
            viewportFocusSequence={viewportFocusSequence}
            restoreChartViewport={restoreChartViewport}
            restoreChartViewportRequestId={restoreChartViewportRequestId}
            registerChartViewportReader={registerChartViewportReader}
            breakpoints={breakpoints}
            compositeBreakpoints={compositeBreakpoints}
            breakpointHitsRefreshKey={breakpointHitsRefreshKey}
            onBreakpointTimelineHit={(sequence, breakpointId) => {
              setFocusedBreakpointId(breakpointId);
              setInspectorTab(1);
              setViewportFocusSequence(sequence);
              setViewportFocusRequestId((value) => value + 1);
            }}
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

          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            No recording loaded
          </Typography>

          <Typography variant="body2" color="text.secondary">
            Import a CSV recording to begin exploring
            and debugging time-series data.
          </Typography>

          <Button
            variant="outlined"
            startIcon={<UploadFileIcon />}
            onClick={() => requestCsvWizardRef.current(null, true)}
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
          minHeight: 34,
          "& .MuiTab-root": {
            minHeight: 34,
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
        <Tab label="Timeline" />
      </Tabs>

      <Box
        role="tabpanel"
        hidden={inspectorTab !== 0}
        sx={{
          flex: "1 1 0%",
          minHeight: 0,
          overflowY: "auto",
          overflowX: "hidden",
          scrollbarGutter: "stable",
          overscrollBehavior: "contain",
          display: inspectorTab === 0 ? "block" : "none",
        }}
      >
        <ChannelSelectionPanel
          channels={channels}
          selectedChannelIds={selectedChannelIds}
          axisPins={axisPins}
          effectiveRightAxisChannelIds={effectiveRightAxisIds}
          onSelectionChange={handleChannelSelectionChange}
          onAxisPinChange={handleAxisPinChange}
          onClearAllAxisPins={clearAllAxisPins}
        />
      </Box>

      <Box
        role="tabpanel"
        hidden={inspectorTab !== 1}
        sx={{
          flex: "1 1 0%",
          minHeight: 0,
          overflowY: "auto",
          overflowX: "hidden",
          scrollbarGutter: "stable",
          overscrollBehavior: "contain",
          display: inspectorTab === 1 ? "block" : "none",
        }}
      >
        {session && (
          <BreakpointPanel
            breakpoints={breakpoints}
            compositeBreakpoints={compositeBreakpoints}
            channels={channels}
            loading={loading}
            busy={breakpointBusy || seeking || playbackRunningRef.current || playbackBusy}
            error={breakpointError}
            onAdd={() => {
              setBreakpointDraft(null);
              setBreakpointDialogOpen(true);
            }}
            onRunTo={(breakpointId, direction) => {
              void handleRunToBreakpoint(breakpointId, direction);
            }}
            focusedBreakpointId={focusedBreakpointId}
            coverage={breakpointCoverage}
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
          flex: "1 1 0%",
          minHeight: 0,
          overflowY: "auto",
          overflowX: "hidden",
          scrollbarGutter: "stable",
          overscrollBehavior: "contain",
          display: inspectorTab === 2 ? "block" : "none",
        }}
      >
        {session?.currentSample ? (
          <ChannelList
            sample={session.currentSample}
            previousSample={previousWatchSample}
            channels={channels}
            selectedChannelIds={selectedChannelIds}
            seriesColors={valueSeriesColors}
            navigationBusy={loading || breakpointBusy || playbackRunningRef.current || playbackBusy}
            onNextChange={(channelId) => void handleChangeNavigation(channelId, "next")}
            onPreviousChange={(channelId) => void handleChangeNavigation(channelId, "previous")}
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

      <Box
        role="tabpanel"
        hidden={inspectorTab !== 3}
        sx={{
          flex: "1 1 0%",
          minHeight: 0,
          overflow: "hidden",
          display: inspectorTab === 3 ? "flex" : "none",
          flexDirection: "column",
          p: 1,
        }}
      >
        <RecordingTimelinePanel
          timeline={recordingTimeline}
          loading={timelineLoading}
          currentSequence={session?.currentSample?.sequence ?? null}
          onSeek={(sequence) => void handleSeek(sequence)}
        />
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
        minHeight: 22,
        px: 0.25,
      }}
    >
      <Typography
        variant="caption"
        color="text.secondary"
        noWrap
        sx={{ fontSize: "0.68rem" }}
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

  const navigationBusy =
    loading || seeking || breakpointBusy ||
    playbackRunningRef.current || playbackBusy;

  const executeOpenInvestigation = async (
    investigationId: string,
    projectId: string,
  ) => {
    cancelLocalPlayback();
    setLoadError(null);
    setLoading(true);

    try {
      const opened = await investigationApi.openInvestigation(investigationId, {
        projectId: projectId || null,
        closeSessionId: session?.sessionId ?? null,
      });

      const document = await investigationApi.getInvestigation(
        investigationId,
        projectId || null,
      );

      const loadedChannels = await loadRecording(opened.sessionId, {
        selectChannels: document.uiState ? "none" : "default",
        resumeCursor: false,
      });
      if (!loadedChannels) return;

      const savedHit = document.session.breakpointHit;
      setBreakpointHit(savedHit
        ? {
            sequence: savedHit.sequenceNumber,
            breakpointId: savedHit.breakpointId ?? null,
            label: savedHit.label ?? null,
          }
        : null);

      if (document.uiState) {
        const uiState: InvestigationUiState = {
          selectedChannelIds: document.uiState.selectedChannelIds ?? [],
          rightAxisChannelIds: document.uiState.rightAxisChannelIds ?? [],
          selectedChannelNames: document.uiState.selectedChannelNames ?? [],
          rightAxisChannelNames: document.uiState.rightAxisChannelNames ?? [],
          axisPins: document.uiState.axisPins,
          chartViewport: document.uiState.chartViewport ?? null,
        };
        const restored = applyInvestigationUiState(uiState, loadedChannels);
        rememberInvestigationUiBaseline(investigationId, restored);
      }

      const name =
        document.metadata?.name?.trim() ||
        opened.investigationName ||
        opened.recordingName;

      setActiveInvestigation({
        investigationId,
        investigationName: name,
        projectId: projectId || null,
      });

      if (opened.recordingName) {
        saveInvestigationSelection({
          recordingName: opened.recordingName,
          projectId: projectId || "",
          investigationId,
        });
      }

      setBookmarkRevision((value) => value + 1);
      onInvestigationOpened?.(investigationId, projectId);
      onSelectSection("recordings");
    } catch (error) {
      if (
        error instanceof ApiError
        && error.status === 404
        && loadLastInvestigation()?.investigationId === investigationId
      ) {
        clearLastInvestigation();
      }
      const message = formatOpenInvestigationError(error);
      setLoadError(message);
      if (
        message.includes("local catalog")
        || message.includes("Import the CSV")
      ) {
        onSelectSection("recordings");
      }
    } finally {
      setLoading(false);
    }
  };

  const openInvestigationRef = useRef(executeOpenInvestigation);
  useEffect(() => {
    openInvestigationRef.current = executeOpenInvestigation;
  });

  useEffect(() => {
    if (!investigationRequest) return;
    if (investigationRequest.nonce === seenInvestigationRequestRef.current) return;
    seenInvestigationRequestRef.current = investigationRequest.nonce;
    void openInvestigationRef.current(
      investigationRequest.investigationId,
      investigationRequest.projectId,
    );
  }, [investigationRequest]);

  const handleOpenInvestigation = async (
    investigationId: string,
    projectId: string,
  ) => {
    requestSessionNavigation({
      kind: "open",
      investigationId,
      projectId,
    });
  };

  const deepLinkStartedRef = useRef<string | null>(null);

  useEffect(() => {
    if (!deepLinkInvestigation) return;

    const { investigationId, projectId } = deepLinkInvestigation;
    const key = `${investigationId}:${projectId}`;
    if (deepLinkStartedRef.current === key) return;

    deepLinkStartedRef.current = key;
    onDeepLinkHandled?.();
    void handleOpenInvestigation(investigationId, projectId);
  }, [deepLinkInvestigation, onDeepLinkHandled]);

  const projectsView = (
    <Box
      sx={{
        flex: 1,
        minHeight: 0,
        overflowY: "auto",
      }}
    >
      <ProjectDashboard
        onOpenInvestigation={handleOpenInvestigation}
        openInvestigationBusy={navigationBusy}
      />
    </Box>
  );

  const analysisView = (
    <Box
      sx={{
        flex: 1,
        minHeight: 0,
        overflow: "hidden",
        display: "flex",
        flexDirection: "column",
      }}
    >
      <AnalysisPanel
        session={session}
        channels={channels}
        selectedChannelIds={selectedChannelIds}
        breakpointKey={[
          ...breakpoints.map((item) => `${item.breakpointId}:${item.isEnabled}`),
          ...compositeBreakpoints.map((item) => `${item.breakpointId}:${item.isEnabled}`),
        ].join("|")}
        breakpointHit={breakpointHit}
        onInspectSample={(sequence) => {
          void handleSeek(sequence).then(() => onSelectSection("recordings"));
        }}
        onBookmarkChange={(request) => {
          void handleQuickBookmark({
            sequence: request.sequence,
            title: request.title,
            annotation: request.annotation,
          });
        }}
      />
    </Box>
  );

  const settingsView = (
    <Box
      sx={{
        flex: 1,
        minHeight: 0,
        overflowY: "auto",
      }}
    >
      <AppSettingsPanel
        themeMode={themeMode}
        onToggleTheme={onToggleTheme ?? (() => undefined)}
        slidingWindow={slidingWindow}
        onSlidingWindowChange={updateSlidingWindow}
      />
    </Box>
  );

  const investigationsView = (
    <Box
      sx={{
        flex: 1,
        minHeight: 0,
        overflow: "hidden",
        display: "flex",
        flexDirection: { xs: "column", md: "row" },
        gap: 2,
        p: { xs: 0.5, md: 1 },
      }}
    >
      <Box
        sx={{
          flex: { xs: "0 0 auto", md: "0 0 360px" },
          minHeight: 0,
          maxHeight: { xs: 280, md: "100%" },
          overflowY: "auto",
        }}
      >
        <InvestigationsHomePanel
          title="Workspace investigations"
          showDelete
          onOpen={handleOpenInvestigation}
          busy={loading || navigationBusy}
          onListChanged={() => setBookmarkRevision((value) => value + 1)}
        />
      </Box>
      <Box
        sx={{
          flex: 1,
          minHeight: 0,
          minWidth: 0,
          overflowY: "auto",
        }}
      >
      <InvestigationPanel
        sessionId={session?.sessionId ?? null}
        recordingName={session?.recordingName ?? null}
        currentSequence={session?.currentSample?.sequence ?? null}
        currentTimestampNanoseconds={
          session?.currentSample?.timestampNanoseconds ?? null
        }
        timestampOrigin={session?.timestampOrigin ?? "Unix"}
        navigationBusy={navigationBusy}
        setupHint={investigationSetupHint}
        selectedChannelIds={selectedChannelIds}
        onSeek={(sequence) => void handleSeek(sequence)}
        onBookmarksChanged={() => setBookmarkRevision((value) => value + 1)}
        onRestoreUiState={applyInvestigationUiState}
        resolveExportUiState={resolveExportUiState}
        isUiStateDirty={isInvestigationUiDirty}
        onUiStateBaselineSaved={rememberInvestigationUiBaseline}
        onOpenInvestigation={handleOpenInvestigation}
        breakpointHit={breakpointHitPayload()}
        ensureInvestigation={() => ensureSavedInvestigation()}
      />
      </Box>
    </Box>
  );

  const dashboardView = (
    <RecordingDebuggerDashboard
      session={session}
      channelCount={channels.length}
      breakpoints={breakpoints}
      compositeBreakpoints={compositeBreakpoints}
      onImport={() => requestCsvWizardRef.current(null, true)}
      onQuickOpenFile={(file) => requestCsvWizardRef.current(file, false)}
      quickOpenBusy={wizardOpen}
      onSaveAsProject={() => {
        setSaveAsProjectError("");
        setSaveAsProjectOpen(true);
      }}
      onResumeDebugging={() => onSelectSection("recordings")}
      onManageBreakpoints={() => {
        onSelectSection("recordings");
        setInspectorTab(1);
      }}
      onOpenInvestigations={() => onSelectSection("investigations")}
      onGoToSample={(sequence) => {
        void handleSeek(sequence).then(() => onSelectSection("recordings"));
      }}
      bookmarkRefreshKey={bookmarkRevision}
      onOpenInvestigation={handleOpenInvestigation}
      openInvestigationBusy={loading || navigationBusy}
    />
  );

  const sectionPlaceholder = (
    <Box
      sx={{
        display: "flex",
        flex: 1,
        alignItems: "center",
        justifyContent: "center",
        px: 2,
      }}
    >
      <Typography variant="body2" color="text.secondary" sx={{ textAlign: "center" }}>
        This section is not available yet. Open the Dashboard or Recordings view.
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
        {breakpointHit && (section === "recordings" || section === "analysis") && (
          <Alert
            severity="info"
            sx={{ flexShrink: 0, mb: 0.5 }}
            action={section === "analysis" ? undefined : (
              <Button
                color="inherit"
                size="small"
                onClick={() => onSelectSection("analysis")}
              >
                What changed
              </Button>
            )}
          >
            Breakpoint hit at sample {breakpointHit.sequence.toLocaleString()}
            {breakpointHit.label ? ` (${breakpointHit.label})` : ""}.
            Step backward to inspect the lead-up. This event stays pinned when the cursor moves.
          </Alert>
        )}

        {(loadError || importSuccess) && (
          <Box sx={{ flexShrink: 0, mb: 0.5 }}>
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

        {section === "dashboard" ? dashboardView
          : section === "investigations" ? investigationsView
          : section === "recordings" ? (
            session ? (
            <RecordingWorkspace
              toolbar={toolbar}
              chart={chart}
              channels={channelPanel}
              replayControls={replayControls}
              details={details}
            />
            ) : (
              <Box
                sx={{
                  flex: 1,
                  minHeight: 0,
                  overflowY: "auto",
                  display: "flex",
                  flexDirection: "column",
                  gap: 3,
                  p: 1,
                }}
              >
                <RecordingsCatalogPanel
                  busy={loading}
                  onImport={() => requestCsvWizardRef.current(null, true)}
                  onOpened={(result) => void handleImportSuccess(result)}
                />
                <InvestigationsResumeList
                  busy={loading}
                  onOpen={handleOpenInvestigation}
                />
              </Box>
            )
          ) : section === "projects" ? projectsView
          : section === "analysis" ? analysisView
          : section === "settings" ? settingsView
          : sectionPlaceholder}
      </Box>

      <CsvOpenWizard
        open={wizardOpen}
        initialFile={wizardFile}
        confirmTimestamp={wizardConfirmTimestamp}
        sessionReady={
          wizardSessionId !== null
          && session?.sessionId === wizardSessionId
          && !loading
        }
        channels={channels}
        selectedChannelIds={selectedChannelIds}
        onSelectionChange={handleChannelSelectionChange}
        breakpoints={breakpoints}
        compositeBreakpoints={compositeBreakpoints}
        breakpointBusy={breakpointBusy}
        breakpointError={breakpointError}
        onAddBreakpoint={() => {
          setBreakpointDraft(null);
          setBreakpointDialogOpen(true);
        }}
        onAddComposite={() => setCompositeDialogOpen(true)}
        onSetBreakpointEnabled={handleSetBreakpointEnabled}
        onDeleteBreakpoint={handleDeleteBreakpoint}
        onImported={handleWizardImported}
        onClose={closeCsvWizard}
        onFinish={(mode) => void completeCsvWizard(mode)}
      />

      <SaveAsProjectDialog
        open={saveAsProjectOpen}
        defaultName={session?.recordingName.replace(/\.csv$/i, "") ?? ""}
        busy={saveAsProjectBusy}
        error={saveAsProjectError}
        onClose={() => {
          if (!saveAsProjectBusy) setSaveAsProjectOpen(false);
        }}
        onSave={(projectName, rootPath) => void handleSaveAsProject(projectName, rootPath)}
      />

      <BreakpointDialog
        open={breakpointDialogOpen}
        channels={channels}
        channelExtents={channelExtents}
        seriesColors={valueSeriesColors}
        busy={breakpointBusy}
        draft={breakpointDraft}
        timeline={recordingTimeline}
        onClose={() => {
          if (!breakpointBusy) {
            setBreakpointDialogOpen(false);
            setBreakpointDraft(null);
          }
        }}
        onCreate={(request) => handleCreateBreakpoint(request)}
        onCreateMissingData={handleCreateMissingDataBreakpoint}
        onCreateTimeline={handleCreateTimelineBreakpoint}
      />

      <CompositeBreakpointDialog
        open={compositeDialogOpen}
        channels={channels}
        channelExtents={channelExtents}
        seriesColors={valueSeriesColors}
        busy={breakpointBusy}
        error={breakpointError}
        onClose={() => {
          if (!breakpointBusy) setCompositeDialogOpen(false);
        }}
        onCreate={handleCreateCompositeBreakpoint}
      />

      <Dialog
        open={sessionNavDiscardOpen}
        onClose={() => {
          setSessionNavDiscardOpen(false);
          pendingSessionNavRef.current = null;
          pendingWizardRef.current = null;
        }}
      >
        <DialogTitle>Discard unsaved chart layout?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Channel selection or chart zoom changed since the last save.
            Continue without saving?
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => {
              setSessionNavDiscardOpen(false);
              pendingSessionNavRef.current = null;
              pendingWizardRef.current = null;
            }}
          >
            Cancel
          </Button>
          <Button
            color="warning"
            onClick={() => {
              const pending = pendingSessionNavRef.current;
              const pendingWizard = pendingWizardRef.current;
              setSessionNavDiscardOpen(false);
              pendingSessionNavRef.current = null;
              pendingWizardRef.current = null;
              if (pending) {
                void runSessionNavigation(pending);
              }
              if (pendingWizard) {
                openCsvWizard(pendingWizard.file, pendingWizard.confirmTimestamp);
              }
            }}
          >
            Discard and continue
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={bookmarkSnackOpen}
        autoHideDuration={bookmarkSnackPrompt ? 8000 : 4000}
        onClose={() => {
          setBookmarkSnackOpen(false);
          setBookmarkSnackPrompt(false);
        }}
        message={bookmarkSnackMessage}
        action={
          bookmarkSnackPrompt ? (
            <Button
              color="inherit"
              size="small"
              onClick={() => {
                setBookmarkSnackOpen(false);
                setBookmarkSnackPrompt(false);
                void handleQuickBookmark();
              }}
            >
              Bookmark hit
            </Button>
          ) : undefined
        }
      />
    </>
  );
}

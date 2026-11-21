
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
import { InvestigationPanel } from "../investigations/InvestigationPanel";
import { RecordingDebuggerDashboard } from "../dashboard/RecordingDebuggerDashboard";
import { saveResumeSequence, getResumeSequence } from "../dashboard/replayResumeStorage";
import type { AppSection } from "../../app/AppSidebar";

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
type PlaybackState = "stopped" | "playing" | "paused";

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

interface RecordingDashboardProps {
  section: AppSection;
  onSelectSection: (section: AppSection) => void;
}

export function RecordingDashboard({
  section,
  onSelectSection,
}: RecordingDashboardProps) {
  const [session, setSession] = useState<SessionState | null>(
    null,
  );

  const [channels, setChannels] = useState<ChannelMetadata[]>(
    [],
  );

  const [selectedChannelIds, setSelectedChannelIds] = useState<
    string[]
  >([]);

  const [rightAxisChannelIds, setRightAxisChannelIds] = useState<string[]>([]);

  const toggleRightAxisChannel = (channelId: string) => {
    setRightAxisChannelIds((previous) =>
      previous.includes(channelId)
        ? previous.filter((id) => id !== channelId)
        : [...previous, channelId],
    );
  };

  const setChannelChartAxis = (channelId: string, side: "left" | "right") => {
    setRightAxisChannelIds((previous) => {
      if (side === "left") return previous.filter((id) => id !== channelId);
      if (previous.includes(channelId)) return previous;
      return [...previous, channelId];
    });
  };

  const handleChannelSelectionChange = (ids: string[]) => {
    setSelectedChannelIds(ids);
    setRightAxisChannelIds((previous) => previous.filter((id) => ids.includes(id)));
  };

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
  const [playbackState, setPlaybackState] = useState<PlaybackState>("stopped");
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

  const [dialogOpen, setDialogOpen] = useState(false);

  const [breakpointDialogOpen, setBreakpointDialogOpen] =
    useState(false);

  const [compositeDialogOpen, setCompositeDialogOpen] = useState(false);

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

  useEffect(() => {
    if (!session?.recordingId || session.currentSample == null) return;
    saveResumeSequence(session.recordingId, session.currentSample.sequence);
  }, [session?.recordingId, session?.currentSample?.sequence]);

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
    cancelLocalPlayback();
    playbackSessionRef.current = null;
    playbackStateRef.current = "stopped";
    setPlaybackState("stopped");
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
        setRightAxisChannelIds([]);
      }

      loadedSessionIdRef.current = loadedSession.sessionId;

      if (!isSameSession) {
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
      onSelectSection("dashboard");
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

  // Chart click-to-seek uses the same playback-aware transaction as dragging.
  // A running recording resumes from the selected sample; a paused or stopped
  // recording stays in its previous playback state.
  const handleSeek = async (sequence: number) => {
    if (!session || scrubRef.current || seekInProgressRef.current ||
        playbackBusyRef.current || breakpointInProgressRef.current || loading) {
      return;
    }

    handleScrubStart();
    await handleScrubEnd(sequence);
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

  const handleChangeNavigation = async (
    channelId: string,
    direction: "next" | "previous",
  ) => {
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
            {seeking ? " · Seeking..." : ""}
          </Typography>
        )}
      </Box>

      <Stack direction="row" spacing={1} sx={{ flexShrink: 0 }}>
        {session && (
          <Button
            size="small"
            variant="outlined"
            startIcon={<RefreshIcon />}
            onClick={handleRefresh}
            disabled={
              loading || seeking || breakpointBusy || playbackRunningRef.current || playbackBusy
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
            loading || seeking || breakpointBusy || playbackRunningRef.current || playbackBusy
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
            rightAxisChannelIds={rightAxisChannelIds}
            onToggleRightAxis={toggleRightAxisChannel}
            currentSequence={
              session.currentSample?.sequence ?? null
            }
            currentTimestampNanoseconds={session.currentSample?.timestampNanoseconds ?? null}
            timestampOrigin={session.timestampOrigin ?? "Unix"}
            onSeek={handleSeek}
            onScrubStart={handleScrubStart}
            onScrubEnd={(sequence) => void handleScrubEnd(sequence)}
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
          rightAxisChannelIds={rightAxisChannelIds}
          onSelectionChange={handleChannelSelectionChange}
          onChartAxisChange={setChannelChartAxis}
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
            navigationBusy={loading || seeking || breakpointBusy || playbackRunningRef.current || playbackBusy}
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

  const investigationsView = (
    <Box
      sx={{
        flex: 1,
        minHeight: 0,
        overflowY: "auto",
        maxWidth: 520,
      }}
    >
      {session ? (
        <InvestigationPanel
          sessionId={session.sessionId}
          recordingName={session.recordingName}
          currentSequence={session.currentSample?.sequence ?? null}
          currentTimestampNanoseconds={
            session.currentSample?.timestampNanoseconds ?? null
          }
          timestampOrigin={session.timestampOrigin ?? "Unix"}
          navigationBusy={navigationBusy}
          onSeek={(sequence) => void handleSeek(sequence)}
        />
      ) : (
        <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
          Import a recording under Recordings before saving investigation bookmarks.
        </Typography>
      )}
    </Box>
  );

  const dashboardView = (
    <RecordingDebuggerDashboard
      session={session}
      channelCount={channels.length}
      breakpoints={breakpoints}
      compositeBreakpoints={compositeBreakpoints}
      onImport={() => setDialogOpen(true)}
      onResumeDebugging={() => onSelectSection("recordings")}
      onManageBreakpoints={() => {
        onSelectSection("recordings");
        setInspectorTab(1);
      }}
      onOpenInvestigations={() => onSelectSection("investigations")}
      onGoToSample={(sequence) => {
        void handleSeek(sequence).then(() => onSelectSection("recordings"));
      }}
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
            <RecordingWorkspace
              toolbar={toolbar}
              chart={chart}
              channels={channelPanel}
              replayControls={replayControls}
              details={details}
            />
          ) : sectionPlaceholder}
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

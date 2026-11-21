import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";

import {
  emptyWorkspaceContext,
  type WorkspaceContextState,
  type WorkspaceMode,
} from "./workspaceTypes";
import { loadWorkspaceContext, saveWorkspaceContext } from "./workspaceStorage";

interface WorkspaceContextValue {
  context: WorkspaceContextState;
  setRecordingSession: (
    sessionId: string,
    recordingId: string,
    recordingName: string,
  ) => void;
  setActiveInvestigation: (params: {
    investigationId: string;
    investigationName: string | null;
    projectId: string | null;
  }) => void;
  clearInvestigation: () => void;
  setMode: (mode: WorkspaceMode) => void;
}

const WorkspaceContext = createContext<WorkspaceContextValue | null>(null);

export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const [context, setContext] = useState<WorkspaceContextState>(
  () => loadWorkspaceContext() ?? emptyWorkspaceContext(),
  );

  const commit = useCallback(
    (updater: (previous: WorkspaceContextState) => WorkspaceContextState) => {
      setContext((previous) => {
        const next = updater(previous);
        saveWorkspaceContext(next);
        return next;
      });
    },
    [],
  );

  const setRecordingSession = useCallback(
    (sessionId: string, recordingId: string, recordingName: string) => {
      commit((previous) => ({
        ...previous,
        sessionId,
        recordingId,
        recordingName,
        mode: previous.investigationId ? "investigation" : "recording",
      }));
    },
    [commit],
  );

  const setActiveInvestigation = useCallback(
    (params: {
      investigationId: string;
      investigationName: string | null;
      projectId: string | null;
    }) => {
      commit((previous) => ({
        ...previous,
        mode: "investigation",
        investigationId: params.investigationId,
        investigationName: params.investigationName,
        projectId: params.projectId ?? "",
      }));
    },
    [commit],
  );

  const clearInvestigation = useCallback(() => {
    commit((previous) => ({
      ...previous,
      mode: "recording",
      investigationId: null,
      investigationName: null,
      projectId: null,
    }));
  }, [commit]);

  const setMode = useCallback(
    (mode: WorkspaceMode) => {
      commit((previous) =>
        previous.mode === mode ? previous : { ...previous, mode });
    },
    [commit],
  );

  const value = useMemo(
    () => ({
      context,
      setRecordingSession,
      setActiveInvestigation,
      clearInvestigation,
      setMode,
    }),
    [
      context,
      setRecordingSession,
      setActiveInvestigation,
      clearInvestigation,
      setMode,
    ],
  );

  return (
    <WorkspaceContext.Provider value={value}>
      {children}
    </WorkspaceContext.Provider>
  );
}

export function useWorkspace(): WorkspaceContextValue {
  const value = useContext(WorkspaceContext);
  if (!value) {
    throw new Error("useWorkspace must be used inside WorkspaceProvider.");
  }
  return value;
}
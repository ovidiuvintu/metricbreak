import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";

import { readAppUrlState } from "../app/appNavigationUrl";
import { investigationApi } from "../features/investigations/investigationApi";
import type { InvestigationSummary } from "../features/investigations/investigationTypes";
import { projectApi } from "../features/projects/projectApi";
import type { ProjectSummary } from "../features/projects/projectTypes";
import {
  emptyWorkspaceContext,
  type InvestigationOpenRequest,
  type WorkspaceContextState,
  type WorkspaceMode,
} from "./workspaceTypes";
import { loadWorkspaceContext, saveWorkspaceContext } from "./workspaceStorage";

interface SetActiveProjectOptions {
  /** Keep the current session when the open recording is being saved into this project. */
  keepSession?: boolean;
}

interface WorkspaceContextValue {
  context: WorkspaceContextState;
  activeProject: ProjectSummary | null;
  projectInvestigations: InvestigationSummary[];
  projectInvestigationsLoading: boolean;
  investigationRequest: InvestigationOpenRequest | null;
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
  setActiveProject: (
    project: ProjectSummary | null,
    options?: SetActiveProjectOptions,
  ) => void;
  refreshProjectInvestigations: () => Promise<void>;
  requestOpenInvestigation: (investigationId: string) => void;
  setMode: (mode: WorkspaceMode) => void;
}

const WorkspaceContext = createContext<WorkspaceContextValue | null>(null);

function withProject(
  state: WorkspaceContextState,
  project: ProjectSummary | null,
): WorkspaceContextState {
  return {
    ...state,
    projectId: project?.projectId ?? null,
    projectName: project?.projectName ?? null,
    projectRootPath: project?.rootPath ?? null,
  };
}

export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const [context, setContext] = useState<WorkspaceContextState>(
    () => loadWorkspaceContext() ?? emptyWorkspaceContext(),
  );
  const [activeProject, setActiveProjectState] = useState<ProjectSummary | null>(null);
  const [projectInvestigations, setProjectInvestigations] = useState<InvestigationSummary[]>([]);
  const [projectInvestigationsLoading, setProjectInvestigationsLoading] = useState(false);
  const [investigationRequest, setInvestigationRequest] =
    useState<InvestigationOpenRequest | null>(null);
  const activeProjectRef = useRef<ProjectSummary | null>(null);
  const requestNonce = useRef(0);

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
        projectId: params.projectId ?? previous.projectId,
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
    }));
  }, [commit]);

  const setActiveProject = useCallback(
    (project: ProjectSummary | null, options?: SetActiveProjectOptions) => {
      activeProjectRef.current = project;
      setActiveProjectState(project);
      setProjectInvestigations([]);
      setProjectInvestigationsLoading(project !== null);
      commit((previous) => {
        const nextId = project?.projectId ?? null;
        const sameProject = (previous.projectId ?? null) === nextId;
        if (options?.keepSession || sameProject) {
          return withProject(previous, project);
        }

        return withProject(emptyWorkspaceContext(), project);
      });
    },
    [commit],
  );

  const refreshProjectInvestigations = useCallback(async () => {
    const projectId = activeProjectRef.current?.projectId;
    if (!projectId) {
      setProjectInvestigations([]);
      return;
    }

    setProjectInvestigationsLoading(true);
    try {
      const response = await investigationApi.listProjectInvestigations(projectId);
      if (activeProjectRef.current?.projectId !== projectId) return;
      setProjectInvestigations(response.investigations);
    } catch {
      if (activeProjectRef.current?.projectId === projectId) {
        setProjectInvestigations([]);
      }
    } finally {
      if (activeProjectRef.current?.projectId === projectId) {
        setProjectInvestigationsLoading(false);
      }
    }
  }, []);

  const requestOpenInvestigation = useCallback((investigationId: string) => {
    requestNonce.current += 1;
    setInvestigationRequest({
      investigationId,
      nonce: requestNonce.current,
    });
  }, []);

  const setMode = useCallback(
    (mode: WorkspaceMode) => {
      commit((previous) =>
        previous.mode === mode ? previous : { ...previous, mode });
    },
    [commit],
  );

  useEffect(() => {
    const projectId = activeProject?.projectId ?? null;
    if (!projectId) return;

    let cancelled = false;
    void investigationApi.listProjectInvestigations(projectId)
      .then((response) => {
        if (!cancelled) setProjectInvestigations(response.investigations);
      })
      .catch(() => {
        if (!cancelled) setProjectInvestigations([]);
      })
      .finally(() => {
        if (!cancelled) setProjectInvestigationsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [activeProject?.projectId]);

  useEffect(() => {
    let cancelled = false;

    const restore = async () => {
      const url = readAppUrlState();
      const stored = loadWorkspaceContext();
      const targetId = url.projectId?.trim() || stored?.projectId || "";
      if (!targetId) return;

      let projects: ProjectSummary[];
      try {
        projects = await projectApi.getOpenProjects();
      } catch {
        return;
      }
      if (cancelled) return;

      let project = projects.find((item) => item.projectId === targetId) ?? null;
      const rootPath = stored?.projectRootPath?.trim();
      if (!project && rootPath && stored?.projectId === targetId) {
        try {
          project = await projectApi.openProject({ rootPath });
        } catch {
          project = null;
        }
      }
      if (cancelled || !project) return;

      const urlProjectId = url.projectId?.trim() || "";
      const keepSession = Boolean(urlProjectId)
        && urlProjectId === stored?.projectId
        && stored?.projectId === project.projectId;
      setActiveProject(project, { keepSession });

      const urlInvestigationId = url.investigationId?.trim();
      if (urlInvestigationId && (!urlProjectId || urlProjectId === project.projectId)) {
        setActiveInvestigation({
          investigationId: urlInvestigationId,
          investigationName: stored?.investigationId === urlInvestigationId
            ? (stored.investigationName ?? null)
            : null,
          projectId: project.projectId,
        });
      }
    };

    void restore();
    return () => {
      cancelled = true;
    };
  }, [setActiveProject, setActiveInvestigation]);

  const value = useMemo(
    () => ({
      context,
      activeProject,
      projectInvestigations,
      projectInvestigationsLoading,
      investigationRequest,
      setRecordingSession,
      setActiveInvestigation,
      clearInvestigation,
      setActiveProject,
      refreshProjectInvestigations,
      requestOpenInvestigation,
      setMode,
    }),
    [
      context,
      activeProject,
      projectInvestigations,
      projectInvestigationsLoading,
      investigationRequest,
      setRecordingSession,
      setActiveInvestigation,
      clearInvestigation,
      setActiveProject,
      refreshProjectInvestigations,
      requestOpenInvestigation,
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

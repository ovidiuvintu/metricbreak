export type WorkspaceMode = "recording" | "investigation";

export interface InvestigationOpenRequest {
  investigationId: string;
  nonce: number;
}

export interface WorkspaceContextState {
  mode: WorkspaceMode;
  sessionId: string | null;
  recordingId: string | null;
  recordingName: string | null;
  investigationId: string | null;
  investigationName: string | null;
  projectId: string | null;
  projectName: string | null;
  projectRootPath: string | null;
}

export const emptyWorkspaceContext = (): WorkspaceContextState => ({
  mode: "recording",
  sessionId: null,
  recordingId: null,
  recordingName: null,
  investigationId: null,
  investigationName: null,
  projectId: null,
  projectName: null,
  projectRootPath: null,
});
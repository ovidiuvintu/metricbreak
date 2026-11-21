import type { WorkspaceContextState } from "./workspaceTypes";

const STORAGE_KEY = "metricbreak-workspace-context";

export function loadWorkspaceContext(): WorkspaceContextState | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as WorkspaceContextState;
  } catch {
    return null;
  }
}

export function saveWorkspaceContext(state: WorkspaceContextState): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  } catch {
    // Storage may be unavailable.
  }
}
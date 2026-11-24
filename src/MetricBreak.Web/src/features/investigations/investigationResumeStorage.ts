const STORAGE_KEY = "metricbreak-last-investigation";

export interface LastInvestigation {
  investigationId: string;
  projectId: string;
  projectRootPath: string | null;
}

export function loadLastInvestigation(): LastInvestigation | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as LastInvestigation;
    if (!parsed.investigationId) return null;
    return {
      investigationId: parsed.investigationId,
      projectId: parsed.projectId ?? "",
      projectRootPath: parsed.projectRootPath ?? null,
    };
  } catch {
    return null;
  }
}

export function saveLastInvestigation(investigation: LastInvestigation): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(investigation));
  } catch {
    // Storage may be unavailable.
  }
}

export function clearLastInvestigation(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Storage may be unavailable.
  }
}

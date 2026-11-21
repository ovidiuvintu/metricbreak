const STORAGE_KEY = "metricbreak-investigation-selection";

export interface InvestigationSelection {
    recordingName: string;
    projectId: string;
    investigationId: string;
}

export function loadInvestigationSelection(
    recordingName: string,
): InvestigationSelection | null {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        if (!raw) return null;

        const parsed = JSON.parse(raw) as InvestigationSelection;
        if (parsed.recordingName !== recordingName) return null;
        if (!parsed.projectId || !parsed.investigationId) return null;

        return parsed;
    } catch {
        return null;
    }
}

export function saveInvestigationSelection(selection: InvestigationSelection): void {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(selection));
    } catch {
        // Storage may be unavailable.
    }
}
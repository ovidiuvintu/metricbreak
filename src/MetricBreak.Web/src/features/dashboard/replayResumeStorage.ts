const STORAGE_PREFIX = "metricbreak-replay-resume:";

export function getResumeSequence(recordingId: string): number | null {
    try {
        const raw = localStorage.getItem(`${STORAGE_PREFIX}${recordingId}`);
        if (raw === null) return null;
        const parsed = Number.parseInt(raw, 10);
        return Number.isFinite(parsed) ? parsed : null;
    } catch {
        return null;
    }
}

export function saveResumeSequence(recordingId: string, sequence: number): void {
    try {
        localStorage.setItem(`${STORAGE_PREFIX}${recordingId}`, String(sequence));
    } catch {
        // Storage may be unavailable.
    }
}
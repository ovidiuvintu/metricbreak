// The clock span stops at the last timestamp, one sample short of the time
// those samples cover. 10,800 samples at 1 Hz run from 10:00:00 through
// 12:59:59, which is 3 hours once the final second is included.
export function formatRecordingDuration(
    startNanoseconds: string | number,
    endNanoseconds: string | number,
    sampleCount: number,
): string | null {
    if (sampleCount < 2) return null;

    let start: bigint;
    let end: bigint;
    try {
        start = BigInt(startNanoseconds);
        end = BigInt(endNanoseconds);
    } catch {
        return null;
    }

    if (end < start) return null;

    const span = end - start;
    const covered = span * BigInt(sampleCount) / BigInt(sampleCount - 1);
    const seconds = Number(covered / 1_000_000_000n);
    if (!Number.isFinite(seconds)) return null;

    return formatWholeSeconds(seconds);
}

function formatWholeSeconds(totalSeconds: number): string {
    const rounded = Math.max(0, Math.round(totalSeconds));
    const hours = Math.floor(rounded / 3600);
    const minutes = Math.floor((rounded % 3600) / 60);
    const seconds = rounded % 60;

    if (hours > 0) {
        if (minutes === 0 && seconds === 0) return `${hours} hr`;
        if (seconds === 0) return `${hours} hr ${minutes} min`;
        if (minutes === 0) return `${hours} hr ${seconds} sec`;
        return `${hours} hr ${minutes} min ${seconds} sec`;
    }

    if (minutes === 0) return `${seconds} sec`;
    if (seconds === 0) return `${minutes} min`;
    return `${minutes} min ${seconds} sec`;
}
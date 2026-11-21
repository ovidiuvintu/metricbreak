export function formatDurationBetweenNanoseconds(
    startNanoseconds: number,
    endNanoseconds: number,
    origin: "Unix" | "Elapsed",
): string | null {
    if (endNanoseconds < startNanoseconds) return null;

    const spanNanoseconds = endNanoseconds - startNanoseconds;

    if (origin === "Elapsed") {
        const seconds = spanNanoseconds / 1e9;
        if (seconds < 60) return `${Math.round(seconds)} sec`;
        const minutes = Math.floor(seconds / 60);
        const remainder = Math.round(seconds % 60);
        if (remainder === 0) return `${minutes} min`;
        return `${minutes} min ${remainder} sec`;
    }

    const spanMs = spanNanoseconds / 1e6;
    if (spanMs < 60_000) return `${Math.round(spanMs / 1000)} sec`;
    const minutes = Math.floor(spanMs / 60_000);
    const seconds = Math.round((spanMs % 60_000) / 1000);
    if (seconds === 0) return `${minutes} min`;
    return `${minutes} min ${seconds} sec`;
}
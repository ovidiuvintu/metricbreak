export function formatBookmarkTime(
    timestampNanoseconds: number,
    origin: "Unix" | "Elapsed" = "Unix",
): string {
    const nanoseconds = BigInt(Math.trunc(timestampNanoseconds));

    if (origin === "Elapsed") {
        const negative = nanoseconds < 0n;
        const magnitude = negative ? -nanoseconds : nanoseconds;
        const seconds = magnitude / 1_000_000_000n;
        const fraction = (magnitude % 1_000_000_000n)
            .toString()
            .padStart(9, "0")
            .replace(/0+$/, "");
        const body = fraction.length === 0
            ? seconds.toString()
            : `${seconds}.${fraction}`;
        return `${negative ? "-" : ""}${body} s`;
    }

    const milliseconds = nanoseconds / 1_000_000n;
    const date = new Date(Number(milliseconds));
    return Number.isNaN(date.getTime())
        ? String(timestampNanoseconds)
        : date.toISOString();
}
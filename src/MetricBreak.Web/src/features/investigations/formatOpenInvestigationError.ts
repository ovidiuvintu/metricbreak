import { ApiError } from "../../shared/api/httpClient";

function errorMessage(error: unknown, fallback: string): string {
    return error instanceof Error ? error.message : fallback;
}

export function formatOpenInvestigationError(error: unknown): string {
    const fallback = "Unable to open investigation.";
    const message = error instanceof ApiError
        ? error.message
        : errorMessage(error, fallback);

    if (
        message.includes("local catalog")
        || message.includes("Import the CSV")
    ) {
        return `${message} Open Recordings, import the CSV (or open it from the catalog), then try again.`;
    }

    return message || fallback;
}
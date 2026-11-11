
export class ApiError extends Error {
    constructor(
        public readonly status: number,
        public readonly code: string,
        message: string,
    ) {
        super(message);
        this.name = "ApiError";
    }
}

export async function apiRequest<T>(
    path: string,
    options?: RequestInit,
): Promise<T> {
    let response: Response;

    try {
        response = await fetch(path, {
            ...options,
            headers: {
                ...(options?.body ? { "Content-Type": "application/json" } : {}),
                ...options?.headers,
            },
        });
    } catch {
        throw new ApiError(
            0,
            "NetworkError",
            "Cannot connect to the MetricBreak API.",
        );
    }

    if (!response.ok) {
        const body: unknown = await response.json().catch(() => null);

        const error =
            body !== null && typeof body === "object"
                ? (body as Record<string, unknown>)
                : {};

        throw new ApiError(
            response.status,
            typeof error.code === "string" ? error.code : "RequestFailed",
            typeof error.message === "string"
                ? error.message
                : `Request failed (${response.status}).`,
        );
    }

    if (response.status === 204) {
        return undefined as T;
    }

    return (await response.json()) as T;
}

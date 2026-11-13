
export interface SampleRangeItem {
    sequence: number;
    timestampNanoseconds: string;
    channels: Record<string, number | null>;
}

export interface SampleRangeResponse {
    sessionId: string;
    start: number;
    count: number;
    totalSamples: number;
    samples: SampleRangeItem[];
}

import type { TypedChannelValues } from "./typedChannelTypes";

export interface CurrentSample {
  sequence: number;
  timestampNanoseconds: number;
  channels: Record<string, number | null>;
  typedChannels?: TypedChannelValues;
}

export interface SessionState {
  sessionId: string;
  recordingId: string;
  recordingName: string;
  sampleCount: number;
  executionState: string;
  pauseReason: string;
  currentSample: CurrentSample;
  timestampOrigin?: "Unix" | "Elapsed";
}

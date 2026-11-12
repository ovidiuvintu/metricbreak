
export interface CurrentSample {
  sequence: number;
  timestampNanoseconds: number;
  channels: Record<string, number | null>;
}

export interface SessionState {
  sessionId: string;
  recordingId: string;
  recordingName: string;
  sampleCount: number;
  executionState: string;
  pauseReason: string;
  currentSample: CurrentSample;
}

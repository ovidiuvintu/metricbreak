import type { TypedChannelValues } from "./typedChannelTypes";

export interface SampleRangeItem {
  sequence: number;
  timestampNanoseconds: string;
  channels: Record<string, number | null>;
  typedChannels?: TypedChannelValues;
}

export interface SampleRangeResponse {
  sessionId: string;
  start: number;
  count: number;
  totalSamples: number;
  samples: SampleRangeItem[];
}

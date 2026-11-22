export interface ChannelWindowSummary {
  channelId: string;
  name: string;
  unit: string | null;
  valueType: "Numeric" | "Boolean" | "Categorical" | string;
  presentCount: number;
  missingCount: number;
  changeCount: number | null;
  minimum: number | null;
  minimumSequence: number | null;
  maximum: number | null;
  maximumSequence: number | null;
  mean: number | null;
  trueCount: number | null;
  falseCount: number | null;
  distinctCount: number | null;
  mode: string | null;
  modeCount: number | null;
}

export interface ChannelComparison {
  leftChannelId: string;
  leftChannelName: string;
  rightChannelId: string;
  rightChannelName: string;
  overlapCount: number;
  correlation: number | null;
}

export interface WindowAnalysis {
  startSequence: number;
  endSequence: number;
  startTimestampNanoseconds: string;
  endTimestampNanoseconds: string;
  sampleCount: number;
  channels: ChannelWindowSummary[];
  comparisons: ChannelComparison[];
}

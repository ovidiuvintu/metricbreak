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
  median: number | null;
  standardDeviation: number | null;
  percentile05: number | null;
  percentile25: number | null;
  percentile75: number | null;
  percentile95: number | null;
}

export interface ChannelComparison {
  leftChannelId: string;
  leftChannelName: string;
  rightChannelId: string;
  rightChannelName: string;
  overlapCount: number;
  correlation: number | null;
}

export interface DuplicateTimestampGroup {
  timestampNanoseconds: string;
  sequences: number[];
}

export interface SamplingGap {
  beforeSequence: number;
  afterSequence: number;
  intervalNanoseconds: string;
  missingTimestamps: number | null;
}

export interface ChannelCoverage {
  channelId: string;
  name: string;
  unit: string | null;
  missingCount: number;
  firstSequence: number;
}

export interface SamplingHealth {
  sampleCount: number;
  startTimestampNanoseconds: string;
  endTimestampNanoseconds: string;
  positiveIntervalCount: number;
  intervalMinimumNanoseconds: string | null;
  intervalMedianNanoseconds: string | null;
  intervalMaximumNanoseconds: string | null;
  intervalModeNanoseconds: string | null;
  intervalModeCount: number;
  cadenceSource: "confirmed" | "declared" | null;
  cadenceNanoseconds: string | null;
  duplicateGroupCount: number;
  duplicateSampleCount: number;
  duplicates: DuplicateTimestampGroup[];
  gapCount: number;
  missingTimestampCount: number | null;
  gaps: SamplingGap[];
  missingChannels: ChannelCoverage[];
  channelCount: number;
}

export interface WindowAnalysis {
  startSequence: number;
  endSequence: number;
  startTimestampNanoseconds: string;
  endTimestampNanoseconds: string;
  sampleCount: number;
  channels: ChannelWindowSummary[];
  comparisons: ChannelComparison[];
  sampling: SamplingHealth;
}

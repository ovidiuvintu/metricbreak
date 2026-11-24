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

export interface AnalysisReading {
  valueType: string;
  numericValue: number | null;
  booleanValue: boolean | null;
  text: string | null;
}

export interface ChannelSnapshot {
  channelId: string;
  name: string;
  unit: string | null;
  sequence: number;
  value: AnalysisReading;
}

export interface PrecedingChange {
  channelId: string;
  name: string;
  unit: string | null;
  previousSequence: number;
  eventSequence: number;
  previous: AnalysisReading;
  current: AnalysisReading;
}

export interface ChannelFirstMove {
  channelId: string;
  name: string;
  unit: string | null;
  baselineSequence: number;
  firstChangeSequence: number;
  baseline: AnalysisReading;
  value: AnalysisReading;
}

export interface ConditionRun {
  breakpointId: string;
  label: string;
  isEnabled: boolean;
  matchesAtEvent: boolean;
  startSequence: number | null;
  endSequence: number | null;
  sampleCount: number | null;
  durationNanoseconds: string | null;
}

export interface EventAnalysis {
  eventSequence: number;
  eventTimestampNanoseconds: string;
  lookbackStartSequence: number;
  precedingSequence: number | null;
  states: ChannelSnapshot[];
  immediateChanges: PrecedingChange[];
  firstMoves: ChannelFirstMove[];
  conditionRuns: ConditionRun[];
}

export interface ChannelDivergence {
  channelId: string;
  name: string;
  unit: string | null;
  valueType: string;
  comparedCount: number;
  mismatchCount: number;
  firstMismatchSequence: number | null;
  maxAbsoluteDelta: number | null;
  maxAbsoluteDeltaSequence: number | null;
}

export interface RecordingComparison {
  referenceRecordingId: string;
  referenceName: string;
  subjectSampleCount: number;
  referenceSampleCount: number;
  comparedSampleCount: number;
  onlyInSubject: string[];
  onlyInReference: string[];
  channels: ChannelDivergence[];
}

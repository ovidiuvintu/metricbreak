import type { SampleRangeItem } from "./sampleTypes";
import type { ChannelMetadata } from "./channelMetadata";

export type AxisPin = "left" | "right";

export type AxisPins = Record<string, AxisPin>;

export type PlottedSeriesRange = {
  min: number;
  max: number;
  span: number;
  scale: number;
};

export const AXIS_RANGE_RATIO_THRESHOLD = 40;
export const AXIS_RANGE_MIN_SCALE = 1e-9;

export function numericPlotValue(
  sample: SampleRangeItem,
  channelId: string,
): number | null {
  const typedSample = sample as SampleRangeItem & {
    typedChannels?: Record<string, { valueType: string; value: unknown } | undefined>;
  };
  if (typedSample.typedChannels?.[channelId]?.valueType === "Missing") return null;
  const value = sample.channels[channelId];
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

export function isNumericChartChannel(
  samples: SampleRangeItem[],
  channelId: string,
): boolean {
  let sawNumeric = false;
  for (const sample of samples) {
    const typed = sample.typedChannels?.[channelId];
    if (typed) {
      if (typed.valueType === "Boolean" || typed.valueType === "Categorical") {
        return false;
      }
      if (
        typed.valueType === "Numeric"
        && typeof typed.value === "number"
        && Number.isFinite(typed.value)
      ) {
        sawNumeric = true;
      }
      continue;
    }
    const projected = sample.channels[channelId];
    if (typeof projected === "number" && Number.isFinite(projected)) {
      sawNumeric = true;
    }
  }
  return sawNumeric;
}

export function collectPlottedSeriesRange(
  samples: SampleRangeItem[],
  channelId: string,
): PlottedSeriesRange | null {
  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;

  for (const sample of samples) {
    const value = numericPlotValue(sample, channelId);
    if (value === null) continue;
    min = Math.min(min, value);
    max = Math.max(max, value);
  }

  if (!Number.isFinite(min) || !Number.isFinite(max)) return null;

  const span = max - min;
  const extent = Math.max(Math.abs(min), Math.abs(max));
  const scale = Math.max(span, extent, AXIS_RANGE_MIN_SCALE);

  return { min, max, span, scale };
}

/**
 * Suggests channels for the right Y-axis from loaded page samples and selection.
 * Returns empty when a single axis is sufficient.
 */
export function suggestRightAxisChannelIds(
  channels: ChannelMetadata[],
  selectedChannelIds: string[],
  samples: SampleRangeItem[],
): string[] {
  if (samples.length === 0 || selectedChannelIds.length < 2) return [];

  const selected = new Set(selectedChannelIds);
  const numericIds = channels
    .filter(
      (channel) =>
        selected.has(channel.channelId)
        && isNumericChartChannel(samples, channel.channelId),
    )
    .map((channel) => channel.channelId);

  if (numericIds.length < 2) return [];

  const ranges = numericIds
    .map((channelId) => ({
      channelId,
      range: collectPlottedSeriesRange(samples, channelId),
    }))
    .filter((item): item is { channelId: string; range: PlottedSeriesRange } =>
      item.range !== null);

  if (ranges.length < 2) return [];

  const scales = ranges.map((item) => item.range.scale);
  const minScale = Math.min(...scales);
  const maxScale = Math.max(...scales);

  if (minScale <= 0 || maxScale / minScale < AXIS_RANGE_RATIO_THRESHOLD) {
    return [];
  }

  const sorted = [...ranges].sort((a, b) => b.range.scale - a.range.scale);
  const largest = sorted[0];
  const second = sorted[1];

  if (largest.range.scale / second.range.scale >= AXIS_RANGE_RATIO_THRESHOLD) {
    return [largest.channelId];
  }

  const withLog = ranges.map((item) => ({
    channelId: item.channelId,
    log: Math.log10(item.range.scale),
  }));
  withLog.sort((a, b) => a.log - b.log);

  let maxGap = 0;
  let splitAt = 1;

  for (let index = 1; index < withLog.length; index += 1) {
    const gap = withLog[index].log - withLog[index - 1].log;
    if (gap > maxGap) {
      maxGap = gap;
      splitAt = index;
    }
  }

  if (maxGap < Math.log10(AXIS_RANGE_RATIO_THRESHOLD)) {
    return [largest.channelId];
  }

  return withLog.slice(splitAt).map((item) => item.channelId);
}

export function resolveEffectiveRightAxisIds(
  suggestedRight: string[],
  selectedChannelIds: string[],
  axisPins: AxisPins,
): string[] {
  const selected = new Set(selectedChannelIds);
  const right = new Set(
    suggestedRight.filter((channelId) => selected.has(channelId)),
  );

  for (const [channelId, pin] of Object.entries(axisPins)) {
    if (!selected.has(channelId)) continue;
    if (pin === "right") right.add(channelId);
    if (pin === "left") right.delete(channelId);
  }

  return [...right];
}

export function axisPinsFromLegacyRightAxis(
  rightAxisChannelIds: string[],
): AxisPins {
  const pins: AxisPins = {};
  for (const channelId of rightAxisChannelIds) {
    pins[channelId] = "right";
  }
  return pins;
}
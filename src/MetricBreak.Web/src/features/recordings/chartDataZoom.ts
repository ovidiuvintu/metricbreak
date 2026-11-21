import type { EChartsType } from "echarts/core";

/** Whether a sequence lies in the current inside-dataZoom window (not just the page). */
export function sequenceInZoomedViewport(
  sequence: number,
  visibleMin: number,
  visibleMax: number,
  epsilon = 0.001,
): boolean {
  return sequence >= visibleMin - epsilon && sequence <= visibleMax + epsilon;
}

export type SequenceViewport = {
  pageStart: number;
  pageEnd: number;
  startPercent: number;
  endPercent: number;
  fullMin: number;
  fullMax: number;
  visibleMin: number;
  visibleMax: number;
  visibleWidth: number;
};

const MIN_ZOOM_SPAN_PERCENT = 1.5;

function pageBounds(pageStart: number, pageEnd: number) {
  const fullMin = pageStart;
  const fullMax = Math.max(pageStart + 1, pageEnd - 1);
  const fullWidth = Math.max(0.001, fullMax - fullMin);
  return { fullMin, fullMax, fullWidth };
}

export function readSequenceViewport(
  chart: EChartsType,
  pageStart: number,
  pageEnd: number,
): SequenceViewport | null {
  if (chart.isDisposed()) return null;

  let option: { dataZoom?: unknown } | undefined;
  try {
    option = chart.getOption() as { dataZoom?: unknown } | undefined;
  } catch {
    return null;
  }
  if (!option) return null;

  const zoom = (option.dataZoom as Array<{
    start?: number;
    end?: number;
  }> | undefined)?.[0];

  const { fullMin, fullMax, fullWidth } = pageBounds(pageStart, pageEnd);
  const startPercent = Math.max(0, Math.min(100, Number(zoom?.start ?? 0)));
  const endPercent = Math.max(startPercent, Math.min(100, Number(zoom?.end ?? 100)));
  const visibleMin = fullMin + fullWidth * startPercent / 100;
  const visibleMax = fullMin + fullWidth * endPercent / 100;

  return {
    pageStart,
    pageEnd,
    startPercent,
    endPercent,
    fullMin,
    fullMax,
    visibleMin,
    visibleMax,
    visibleWidth: Math.max(0, visibleMax - visibleMin),
  };
}

export function applyViewportPercents(
  chart: EChartsType,
  startPercent: number,
  endPercent: number,
  dataZoomIndex = 0,
): void {
  if (chart.isDisposed()) return;

  const start = Math.max(0, Math.min(100, startPercent));
  const end = Math.max(start, Math.min(100, endPercent));

  chart.dispatchAction({
    type: "dataZoom",
    dataZoomIndex,
    start,
    end,
  });
}

export function resetViewport(chart: EChartsType, dataZoomIndex = 0): void {
  applyViewportPercents(chart, 0, 100, dataZoomIndex);
}

export function sequencesToViewportPercents(
  pageStart: number,
  pageEnd: number,
  minSequence: number,
  maxSequence: number,
): { startPercent: number; endPercent: number } {
  const { fullMin, fullMax, fullWidth } = pageBounds(pageStart, pageEnd);
  if (maxSequence < minSequence) {
    [minSequence, maxSequence] = [maxSequence, minSequence];
  }

  const left = Math.max(fullMin, Math.min(fullMax, minSequence));
  const right = Math.max(fullMin, Math.min(fullMax, maxSequence));
  let startPercent = ((left - fullMin) / fullWidth) * 100;
  let endPercent = ((right - fullMin) / fullWidth) * 100;

  if (endPercent - startPercent < MIN_ZOOM_SPAN_PERCENT) {
    const mid = (left + right) / 2;
    const halfSpan = (MIN_ZOOM_SPAN_PERCENT / 100) * fullWidth / 2;
    const spanMin = Math.max(fullMin, mid - halfSpan);
    const spanMax = Math.min(fullMax, mid + halfSpan);
    startPercent = ((spanMin - fullMin) / fullWidth) * 100;
    endPercent = ((spanMax - fullMin) / fullWidth) * 100;
  }

  return {
    startPercent: Math.max(0, Math.min(100, startPercent)),
    endPercent: Math.max(0, Math.min(100, endPercent)),
  };
}

export function zoomViewportByFactor(
  chart: EChartsType,
  pageStart: number,
  pageEnd: number,
  factor: number,
): void {
  const viewport = readSequenceViewport(chart, pageStart, pageEnd);
  if (!viewport || viewport.visibleWidth <= 0) return;

  const { fullMin, fullWidth } = pageBounds(pageStart, pageEnd);
  const midpoint = (viewport.visibleMin + viewport.visibleMax) / 2;
  let nextWidth = viewport.visibleWidth * factor;
  const minWidth = (MIN_ZOOM_SPAN_PERCENT / 100) * fullWidth;
  const maxWidth = fullWidth;
  nextWidth = Math.max(minWidth, Math.min(maxWidth, nextWidth));

  let nextMin = midpoint - nextWidth / 2;
  let nextMax = midpoint + nextWidth / 2;

  if (nextMin < viewport.fullMin) {
    nextMin = viewport.fullMin;
    nextMax = Math.min(viewport.fullMax, nextMin + nextWidth);
  }
  if (nextMax > viewport.fullMax) {
    nextMax = viewport.fullMax;
    nextMin = Math.max(viewport.fullMin, nextMax - nextWidth);
  }

  const startPercent = ((nextMin - fullMin) / fullWidth) * 100;
  const endPercent = ((nextMax - fullMin) / fullWidth) * 100;
  applyViewportPercents(chart, startPercent, endPercent);
}

export function zoomViewportAtAnchor(
  chart: EChartsType,
  pageStart: number,
  pageEnd: number,
  anchorSequence: number,
  factor: number,
): void {
  const viewport = readSequenceViewport(chart, pageStart, pageEnd);
  if (!viewport || viewport.visibleWidth <= 0) return;

  const { fullMin, fullWidth } = pageBounds(pageStart, pageEnd);
  const anchor = Math.max(
    viewport.fullMin,
    Math.min(viewport.fullMax, anchorSequence),
  );
  const relative = viewport.visibleWidth > 0
    ? (anchor - viewport.visibleMin) / viewport.visibleWidth
    : 0.5;

  let nextWidth = viewport.visibleWidth * factor;
  const minWidth = (MIN_ZOOM_SPAN_PERCENT / 100) * fullWidth;
  const maxWidth = fullWidth;
  nextWidth = Math.max(minWidth, Math.min(maxWidth, nextWidth));

  let nextMin = anchor - nextWidth * relative;
  let nextMax = nextMin + nextWidth;

  if (nextMin < viewport.fullMin) {
    nextMin = viewport.fullMin;
    nextMax = Math.min(viewport.fullMax, nextMin + nextWidth);
  }
  if (nextMax > viewport.fullMax) {
    nextMax = viewport.fullMax;
    nextMin = Math.max(viewport.fullMin, nextMax - nextWidth);
  }

  const startPercent = ((nextMin - fullMin) / fullWidth) * 100;
  const endPercent = ((nextMax - fullMin) / fullWidth) * 100;
  applyViewportPercents(chart, startPercent, endPercent);
}

export function zoomViewportToSequence(
  chart: EChartsType,
  pageStart: number,
  pageEnd: number,
  sequence: number,
  viewportFraction = 0.15,
): void {
  const { fullMin, fullMax, fullWidth } = pageBounds(pageStart, pageEnd);
  const clamped = Math.max(fullMin, Math.min(fullMax, sequence));
  const span = Math.max(fullWidth * viewportFraction, (MIN_ZOOM_SPAN_PERCENT / 100) * fullWidth);
  let minSequence = clamped - span / 2;
  let maxSequence = clamped + span / 2;

  if (minSequence < fullMin) {
    minSequence = fullMin;
    maxSequence = Math.min(fullMax, minSequence + span);
  }
  if (maxSequence > fullMax) {
    maxSequence = fullMax;
    minSequence = Math.max(fullMin, maxSequence - span);
  }

  const percents = sequencesToViewportPercents(
    pageStart,
    pageEnd,
    minSequence,
    maxSequence,
  );
  applyViewportPercents(chart, percents.startPercent, percents.endPercent);
}
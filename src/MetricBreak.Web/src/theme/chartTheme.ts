import type { PaletteMode } from "@mui/material";
import { colors, fontFamily } from "./tokens";

export type ChannelDomain =
  | "pressure"
  | "rate"
  | "volume"
  | "density"
  | "chemical"
  | "diagnostic";

export interface SeriesStyle {
  color: string;
  lineStyle: {
    width: number;
    type: "solid" | "dashed" | "dotted";
  };
  domain: ChannelDomain;
}

// Curated 8-color base palettes (WCAG >= 4.5:1 on respective backgrounds, Okabe-Ito inspired)
const darkSeriesColors = [
  "#4EA8FF", // 0: Sky Blue (Primary Rate)
  "#FFB454", // 1: Amber/Orange (Secondary Rate / Clean Rate)
  "#FF6B8A", // 2: Coral/Rose (Primary Pressure)
  "#49D6C5", // 3: Mint/Teal (Density / Proppant)
  "#C792EA", // 4: Lavender (Cumulative Volume)
  "#E5C95C", // 5: Gold (Secondary Pressure / Additives)
  "#78D47C", // 6: Sage Green (Secondary Volume / Clean Vol)
  "#94A3B8", // 7: Cool Slate (Diagnostics)
];

const lightSeriesColors = [
  "#1D4ED8", // 0: Cobalt Blue
  "#B45309", // 1: Deep Amber
  "#BE123C", // 2: Crimson Rose
  "#047857", // 3: Forest Teal
  "#6D28D9", // 4: Deep Violet
  "#A16207", // 5: Ochre Gold
  "#15803D", // 6: Deep Green
  "#475569", // 7: Dark Slate
];

/** Blues, teals, cyans — left Y-axis fallback sequence (no oranges) */
const darkCoolAxisColors = [
  "#4EA8FF",
  "#22D3EE",
  "#49D6C5",
  "#818CF8",
  "#38BDF8",
  "#2DD4BF",
  "#6366F1",
  "#64748B",
];

const lightCoolAxisColors = [
  "#1D4ED8",
  "#0E7490",
  "#047857",
  "#4F46E5",
  "#2563EB",
  "#0F766E",
  "#6D28D9",
  "#475569",
];

/** Corals, golds, reds — right Y-axis fallback (orange reserved, not first) */
const darkWarmAxisColors = [
  "#FF6B8A",
  "#E5C95C",
  "#F87171",
  "#FBBF24",
  "#F472B6",
  "#FB7185",
  "#F97316",
  "#FDA4AF",
];

const lightWarmAxisColors = [
  "#BE123C",
  "#A16207",
  "#DC2626",
  "#CA8A04",
  "#DB2777",
  "#E11D48",
  "#C2410C",
  "#F43F5E",
];

const DOMAIN_CHART_ORDER: ChannelDomain[] = [
  "rate",
  "volume",
  "pressure",
  "density",
  "chemical",
  "diagnostic",
];

export type AxisColorTemperature = "cool" | "warm";

/**
 * Infers the physical telemetry domain from a CSV column header.
 */
export function inferChannelDomain(channelName: string): ChannelDomain {
  const name = channelName.toLowerCase();
  if (name.includes("press") || name.includes("psi")) return "pressure";
  if (name.includes("rate") || name.includes("bpm") || name.includes("scfm")) return "rate";
  if (name.includes("vol") || name.includes("bbl") || name.includes("total")) return "volume";
  if (name.includes("dens") || name.includes("conc") || name.includes("ppg") || name.includes("sand")) return "density";
  if (name.includes("gpt") || name.includes("chem") || name.includes("acid") || name.includes("gel")) return "chemical";
  return "diagnostic";
}

/**
 * Stable series color by original channel index. The first eight colors use the
 * curated palette; additional channels receive deterministic, non-repeating
 * HSL values using the golden angle.
 */
export function getSeriesColor(mode: PaletteMode, channelIndex: number): string {
  const palette = mode === "dark" ? darkSeriesColors : lightSeriesColors;
  if (channelIndex >= 0 && channelIndex < palette.length) {
    return palette[channelIndex];
  }

  const extraIndex = Math.max(0, channelIndex - palette.length);
  const hue = Math.round((extraIndex * 137.508 + 18) % 360);
  const saturation = 64 + (Math.floor(extraIndex / 12) % 3) * 6;

  // Boost lightness slightly in dark mode for deep blue/violet hues (210°–285°)
  const isDeepHue = hue >= 210 && hue <= 285;
  const baseLightness = mode === "dark" ? (isDeepHue ? 68 : 62) : 38;
  const lightness = baseLightness + (Math.floor(extraIndex / 8) % 3) * 5;

  return `hsl(${hue}, ${saturation}%, ${lightness}%)`;
}

function getTemperatureExtraColor(
  mode: PaletteMode,
  temperature: AxisColorTemperature,
  extraIndex: number,
): string {
  const coolHue = (195 + extraIndex * 27) % 360;
  const warmHue = (32 + extraIndex * 21) % 360;
  let hue = temperature === "cool" ? coolHue : warmHue;

  if (temperature === "cool") {
    if (hue > 250 || hue < 165) hue = 195 + (extraIndex * 17) % 70 + 165;
  } else if (hue > 58 && hue < 165) {
    hue = 32 + (extraIndex * 19) % 45;
  }

  const saturation = 64 + (Math.floor(extraIndex / 10) % 3) * 6;
  const baseLightness = mode === "dark" ? 62 : 38;
  const lightness = baseLightness + (Math.floor(extraIndex / 8) % 3) * 5;

  return `hsl(${Math.round(hue)}, ${saturation}%, ${lightness}%)`;
}

/**
 * Series color within a left (cool) or right (warm) axis group when dual Y-axes
 * are in use. Index is per-axis (0 = first series on that side).
 */
export function getAxisTemperatureSeriesColor(
  mode: PaletteMode,
  temperature: AxisColorTemperature,
  indexOnAxis: number,
): string {
  const palette =
    mode === "dark"
      ? temperature === "cool"
        ? darkCoolAxisColors
        : darkWarmAxisColors
      : temperature === "cool"
        ? lightCoolAxisColors
        : lightWarmAxisColors;

  if (indexOnAxis >= 0 && indexOnAxis < palette.length) {
    return palette[indexOnAxis];
  }

  return getTemperatureExtraColor(
    mode,
    temperature,
    Math.max(0, indexOnAxis - palette.length),
  );
}

function domainColorCandidates(
  mode: PaletteMode,
  temperature: AxisColorTemperature,
  domain: ChannelDomain,
): string[] {
  const dark = mode === "dark";

  if (temperature === "cool") {
    switch (domain) {
      case "rate":
        return dark
          ? ["#4EA8FF", "#60A5FA", "#38BDF8"]
          : ["#1D4ED8", "#2563EB", "#0369A1"];
      case "volume":
        return dark
          ? ["#22D3EE", "#06B6D4", "#2DD4BF"]
          : ["#0E7490", "#0891B2", "#0F766E"];
      case "pressure":
        return dark
          ? ["#818CF8", "#6366F1", "#A78BFA"]
          : ["#4F46E5", "#6D28D9", "#7C3AED"];
      case "density":
        return dark
          ? ["#49D6C5", "#14B8A6", "#5EEAD4"]
          : ["#047857", "#0D9488", "#115E59"];
      case "chemical":
        return dark
          ? ["#67E8F9", "#93C5FD", "#6EE7B7"]
          : ["#0C4A6E", "#1E40AF", "#14532D"];
      default:
        return dark ? [...darkCoolAxisColors] : [...lightCoolAxisColors];
    }
  }

  switch (domain) {
    case "rate":
      return dark
        ? ["#FF6B8A", "#FB7185", "#F472B6"]
        : ["#BE123C", "#DB2777", "#E11D48"];
    case "volume":
      return dark
        ? ["#E5C95C", "#FBBF24", "#FDE047"]
        : ["#A16207", "#CA8A04", "#D97706"];
    case "pressure":
      // Red, then green. A second pink or magenta reads as the same series
      // when both traces share the right axis.
      return dark
        ? ["#FF2D55", "#3DDC5A", "#FF9F0A"]
        : ["#BE123C", "#15803D", "#C2410C"];
    case "density":
      return dark
        ? ["#FB923C", "#F97316", "#FDBA74"]
        : ["#C2410C", "#EA580C", "#9A3412"];
    case "chemical":
      return dark
        ? ["#FCD34D", "#FCA5A5", "#F9A8D4"]
        : ["#B45309", "#C2410C", "#9D174D"];
    default:
      return dark ? [...darkWarmAxisColors] : [...lightWarmAxisColors];
  }
}

function normalizeHex(color: string): string {
  return color.trim().toLowerCase();
}

function hexToRgb(hex: string): [number, number, number] | null {
  const raw = hex.replace("#", "").trim();
  const expanded =
    raw.length === 3 ? raw.split("").map((c) => c + c).join("") : raw;
  if (!/^[0-9a-fA-F]{6}$/.test(expanded)) return null;
  const value = Number.parseInt(expanded, 16);
  return [(value >> 16) & 255, (value >> 8) & 255, value & 255];
}

function rgbToHue(r: number, g: number, b: number): number {
  const rn = r / 255;
  const gn = g / 255;
  const bn = b / 255;
  const max = Math.max(rn, gn, bn);
  const min = Math.min(rn, gn, bn);
  const delta = max - min;
  if (delta === 0) return 0;

  let hue = 0;
  if (max === rn) hue = ((gn - bn) / delta) % 6;
  else if (max === gn) hue = (bn - rn) / delta + 2;
  else hue = (rn - gn) / delta + 4;

  return Math.round(hue * 60 + 360) % 360;
}

function colorHue(color: string): number | null {
  const trimmed = color.trim();
  const hslMatch = /^hsla?\(\s*([\d.]+)/i.exec(trimmed);
  if (hslMatch) return Number.parseFloat(hslMatch[1]);

  const rgb = hexToRgb(trimmed);
  if (!rgb) return null;
  return rgbToHue(...rgb);
}

function huesAreTooClose(a: string, b: string, minHueDelta = 26): boolean {
  if (normalizeHex(a) === normalizeHex(b)) return true;

  const ha = colorHue(a);
  const hb = colorHue(b);
  if (ha === null || hb === null) return false;

  const delta = Math.min(Math.abs(ha - hb), 360 - Math.abs(ha - hb));
  return delta < minHueDelta;
}

function isColorConflicting(candidate: string, used: string[]): boolean {
  return used.some((existing) => huesAreTooClose(candidate, existing));
}

function pickDistinctColor(
  mode: PaletteMode,
  temperature: AxisColorTemperature,
  candidates: string[],
  fallbackStart: number,
  used: string[],
): string {
  for (const candidate of candidates) {
    if (!isColorConflicting(candidate, used)) return candidate;
  }

  for (let offset = 0; offset < 32; offset++) {
    const candidate = getAxisTemperatureSeriesColor(mode, temperature, fallbackStart + offset);
    if (!isColorConflicting(candidate, used)) return candidate;
  }

  return candidates[0] ?? getAxisTemperatureSeriesColor(mode, temperature, fallbackStart);
}

export type ChartChannelColorInput = {
  channelId: string;
  channelName: string;
  channelIndex: number;
  yAxisIndex: 0 | 1;
};

/**
 * Assigns perceptually distinct colors. Dual Y-axis mode uses domain slots within
 * cool (left) and warm (right) families and deduplicates hues across the whole chart.
 */
export function assignChartSeriesColors(
  mode: PaletteMode,
  channels: ChartChannelColorInput[],
  dualYAxis: boolean,
): Map<string, string> {
  const result = new Map<string, string>();
  const used: string[] = [];

  if (dualYAxis) {
    for (const yAxisIndex of [0, 1] as const) {
      const temperature: AxisColorTemperature = yAxisIndex === 0 ? "cool" : "warm";
      const group = channels
        .filter((channel) => channel.yAxisIndex === yAxisIndex)
        .sort((a, b) => {
          const domainA = inferChannelDomain(a.channelName);
          const domainB = inferChannelDomain(b.channelName);
          const orderA = DOMAIN_CHART_ORDER.indexOf(domainA);
          const orderB = DOMAIN_CHART_ORDER.indexOf(domainB);
          if (orderA !== orderB) return orderA - orderB;
          return a.channelName.localeCompare(b.channelName);
        });

      const domainCounts = new Map<ChannelDomain, number>();

      for (const channel of group) {
        const domain = inferChannelDomain(channel.channelName);
        const rank = domainCounts.get(domain) ?? 0;
        domainCounts.set(domain, rank + 1);

        const pool = domainColorCandidates(mode, temperature, domain);
        const rotated = [...pool.slice(rank), ...pool, ...pool];
        const color = pickDistinctColor(mode, temperature, rotated, rank + group.length, used);
        result.set(channel.channelId, color);
        used.push(color);
      }
    }

    return result;
  }

  const sorted = [...channels].sort((a, b) => a.channelIndex - b.channelIndex);
  for (const channel of sorted) {
    const domain = inferChannelDomain(channel.channelName);
    const style = getSeriesStyle(mode, channel.channelName, channel.channelIndex);
    const candidates = [
      style.color,
      ...domainColorCandidates(mode, "cool", domain),
      ...domainColorCandidates(mode, "warm", domain),
    ];
    const color = pickDistinctColor(mode, "cool", candidates, channel.channelIndex, used);
    result.set(channel.channelId, color);
    used.push(color);
  }

  return result;
}

/** Accent for shared multi-channel Y-axis titles, lines, and tick labels */
export function getAxisTemperatureAccent(
  mode: PaletteMode,
  temperature: AxisColorTemperature,
): string {
  return domainColorCandidates(mode, temperature, "rate")[0];
}

/**
 * Returns a complete ECharts-compatible series style (color + stroke pattern + width)
 * based on channel name semantics and original CSV column index.
 */
export function getSeriesStyle(
  mode: PaletteMode,
  channelName: string,
  channelIndex: number
): SeriesStyle {
  const palette = mode === "dark" ? darkSeriesColors : lightSeriesColors;
  const domain = inferChannelDomain(channelName);
  const lower = channelName.toLowerCase();

  let color: string;
  switch (domain) {
    case "pressure":
      color = lower.includes("treat") ? palette[2] : getSeriesColor(mode, channelIndex);
      break;
    case "rate":
      color = lower.includes("slurry")
        ? palette[0]
        : lower.includes("clean")
          ? palette[1]
          : getSeriesColor(mode, channelIndex);
      break;
    case "volume":
      color = lower.includes("slurry")
        ? palette[4]
        : lower.includes("clean")
          ? palette[6]
          : getSeriesColor(mode, channelIndex);
      break;
    case "density":
      color = lower.includes("prop") ? palette[3] : getSeriesColor(mode, channelIndex);
      break;
    default:
      color = getSeriesColor(mode, channelIndex);
  }

  const isPrimary =
    lower.includes("treating_press") ||
    lower.includes("slurry_rate") ||
    lower.includes("proppant_conc");

  const isCleanOrBaseline =
    lower.includes("clean_") ||
    lower.includes("backside") ||
    lower.includes("hydrostatic");

  const isVolume = domain === "volume";

  let type: SeriesStyle["lineStyle"]["type"] = "solid";
  if (isCleanOrBaseline) {
    type = "dashed";
  } else if (isVolume) {
    type = "dotted";
  }

  return {
    color,
    domain,
    lineStyle: {
      width: isPrimary ? 2.2 : isVolume ? 1.6 : 1.8,
      type,
    },
  };
}

export function createChartTheme(mode: PaletteMode) {
  const dark = mode === "dark";

  return {
    backgroundColor: "transparent",

    textStyle: {
      fontFamily,
      color: dark ? colors.gray[200] : colors.gray[900],
    },

    color: dark ? darkSeriesColors : lightSeriesColors,

    axisTextColor: dark ? colors.gray[400] : colors.gray[700],

    gridLineColor: dark ? colors.gray[800] : colors.gray[300],

    cursorColor: dark ? colors.primary[400] : colors.primary[700],

    emphasis: {
      focus: "series" as const,
      blurScope: "coordinateSystem" as const,
    },
    unselectedOpacity: 0.18,
  };
}

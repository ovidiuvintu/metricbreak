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

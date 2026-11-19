
import type { PaletteMode } from "@mui/material";
import { colors, fontFamily } from "./tokens";

// Distinct categorical hues for dark chart backgrounds.
// Avoid adjacent neon lime/yellow and cyan/mint combinations.
const darkSeriesColors = [
  "#4EA8FF", // Blue
  "#FFB454", // Orange
  "#C792EA", // Purple
  "#49D6C5", // Teal
  "#FF6B8A", // Rose
  "#E5C95C", // Gold
  "#78D47C", // Green
  "#B4BBD0", // Slate
];

const lightSeriesColors = [
  "#2563EB", // Blue
  "#059669", // Emerald
  "#B45309", // Amber
  "#E11D48", // Rose
  "#7C3AED", // Violet
  "#0891B2", // Cyan
  "#C2410C", // Orange
  "#BE185D", // Pink
];


/**
 * Stable series color by original channel index. The first eight colors use the
 * curated palette; additional channels receive deterministic, non-repeating
 * HSL values instead of cycling back to channel 1.
 *
 * Note: unique CSS colors do not guarantee perceptual separation for a large
 * number of simultaneous series. Hover emphasis remains important.
 */
export function getSeriesColor(mode: PaletteMode, channelIndex: number): string {
  const palette = mode === "dark" ? darkSeriesColors : lightSeriesColors;
  if (channelIndex >= 0 && channelIndex < palette.length) {
    return palette[channelIndex];
  }

  const extraIndex = Math.max(0, channelIndex - palette.length);
  const hue = Math.round((extraIndex * 137.508 + 18) % 360);
  const saturation = 62 + (Math.floor(extraIndex / 12) % 3) * 6;
  const lightness = mode === "dark"
    ? 62 + (Math.floor(extraIndex / 8) % 3) * 5
    : 39 + (Math.floor(extraIndex / 8) % 3) * 5;
  return `hsl(${hue}, ${saturation}%, ${lightness}%)`;
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

    axisTextColor: dark
      ? colors.gray[400]
      : colors.gray[700],

    gridLineColor: dark
      ? colors.gray[800]
      : colors.gray[300],

    cursorColor: dark
      ? colors.primary[400]
      : colors.primary[700],
  };
  
}

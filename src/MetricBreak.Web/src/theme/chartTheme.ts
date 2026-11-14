
import type { PaletteMode } from "@mui/material";
import { colors, fontFamily } from "./tokens";

const darkSeriesColors = [
  "#00E5FF", // Bright cyan
  "#A3FF12", // Lime green
  "#FFE600", // Yellow
  "#FF6B6B", // Coral
  "#D6A4FF", // Violet
  "#FF9F43", // Orange
  "#FF70C5", // Pink
  "#64FFDA", // Mint
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

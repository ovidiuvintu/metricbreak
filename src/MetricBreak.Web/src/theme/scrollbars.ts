
import { colors } from "./tokens";
import type { MetricBreakThemeMode } from "./tokens";

export function createScrollbarStyles(
  mode: MetricBreakThemeMode
) {
  const dark = mode === "dark";

  const track = dark
    ? colors.gray[900]
    : colors.gray[200];

  const thumb = dark
    ? colors.gray[700]
    : colors.gray[400];

  const thumbHover = dark
    ? colors.primary[500]
    : colors.primary[600];

  return {
    // Firefox and browsers supporting CSS Scrollbars
    scrollbarWidth: "thin",
    scrollbarColor: `${thumb} ${track}`,

    // Chromium and Safari scrollbar styling
    "&::-webkit-scrollbar": {
      width: "9px",
      height: "9px",
    },

    "&::-webkit-scrollbar-track": {
      backgroundColor: track,
      borderRadius: "8px",
    },

    "&::-webkit-scrollbar-thumb": {
      backgroundColor: thumb,
      borderRadius: "8px",
      border: `2px solid ${track}`,
    },

    "&::-webkit-scrollbar-thumb:hover": {
      backgroundColor: thumbHover,
    },

    "&::-webkit-scrollbar-corner": {
      backgroundColor: track,
    },
  };
}

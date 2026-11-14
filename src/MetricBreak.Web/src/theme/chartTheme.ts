
import type { PaletteMode } from "@mui/material";
import { colors, fontFamily } from "./tokens";

export function createChartTheme(mode: PaletteMode) {
  const dark = mode === "dark";

  return {
    backgroundColor: "transparent",

    textStyle: {
      fontFamily,
      color: dark ? colors.gray[200] : colors.gray[900],
    },

    color: dark
      ? [
          colors.blue[400],
          colors.green[400],
          colors.primary[400],
          colors.red[400],
        ]
      : [
          colors.blue[600],
          colors.green[700],
          colors.primary[700],
          colors.red[600],
        ],

    axisTextColor: dark ? colors.gray[400] : colors.gray[700],

    gridLineColor: dark ? colors.gray[800] : colors.gray[300],

    cursorColor: dark ? colors.primary[400] : colors.primary[700],
  };
}

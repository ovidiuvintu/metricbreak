
import { createTheme, type PaletteMode } from "@mui/material/styles";
import { colors, fontFamily } from "./tokens";
import { createScrollbarStyles } from "./scrollbars";

export function createMetricBreakTheme(mode: PaletteMode) {
  const dark = mode === "dark";

  return createTheme({
    palette: {
      mode,

      primary: {
        main: dark ? colors.primary[400] : colors.primary[700],
      },

      secondary: {
        main: dark ? colors.blue[400] : colors.blue[600],
      },

      success: {
        main: dark ? colors.green[400] : colors.green[700],
      },

      error: {
        main: dark ? colors.red[400] : colors.red[600],
      },

      info: {
        main: dark ? colors.blue[400] : colors.blue[600],
      },

      background: {
        default: dark ? "#151D27" : colors.gray[100],
        paper: dark ? colors.gray[900] : "#FFFFFF",
      },

      text: {
        primary: dark ? colors.gray[200] : colors.gray[900],
        secondary: dark ? colors.gray[500] : colors.gray[600],
      },

      divider: dark ? colors.gray[800] : colors.gray[300],
    },

    typography: {
      fontFamily,
      fontWeightRegular: 400,
      fontWeightMedium: 600,
      fontWeightBold: 700,

      h4: { fontWeight: 700 },
      h5: { fontWeight: 700 },
      h6: { fontWeight: 600 },

      button: {
        fontWeight: 600,
        textTransform: "none",
      },
    },

    shape: {
      borderRadius: 8,
    },

    components: {
      MuiCssBaseline: {
        styleOverrides: {
        html: {
            ...createScrollbarStyles(mode),
        },

        body: {
            fontFamily,
        },

        // Scrollable elements throughout MetricBreak
        "*": {
            ...createScrollbarStyles(mode),
        },
        },
      },
      MuiButton: {
        styleOverrides: {
          root: {
            boxShadow: "none",
          },
        },
      },
    },
  });
}

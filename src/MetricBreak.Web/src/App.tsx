import { useMemo, useState } from "react";

import {
  Box,
  CssBaseline,
  ThemeProvider,
  type PaletteMode,
} from "@mui/material";

import { createMetricBreakTheme } from "./theme/theme";
import { AppShell } from "./app/AppShell";
import { RecordingDashboard } from "./features/recordings/RecordingDashboard";

const THEME_STORAGE_KEY = "metricbreak-theme";

function getInitialThemeMode(): PaletteMode {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);

    if (stored === "light" || stored === "dark") {
      return stored;
    }
  } catch {
    // Storage may be unavailable.
  }

  return "light";
}

export default function App() {
  const [mode, setMode] = useState<PaletteMode>(
    getInitialThemeMode
  );

  const theme = useMemo(
    () => createMetricBreakTheme(mode),
    [mode]
  );

  const toggleTheme = () => {
    const next = mode === "light" ? "dark" : "light";

    setMode(next);

    try {
      localStorage.setItem(THEME_STORAGE_KEY, next);
    } catch {
      // Continue using the theme in memory.
    }
  };

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />

      <Box
        sx={{
          minHeight: "100vh",
          bgcolor: "background.default",
          color: "text.primary",
        }}
      >
        <AppShell onToggleTheme={toggleTheme}>
          <RecordingDashboard />
        </AppShell>
      </Box>
    </ThemeProvider>
  );
}
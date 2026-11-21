import { useEffect, useMemo, useState } from "react";

import {
  Box,
  CssBaseline,
  ThemeProvider,
  type PaletteMode,
} from "@mui/material";

import { createMetricBreakTheme } from "./theme/theme";
import { AppShell } from "./app/AppShell";
import type { AppSection } from "./app/AppSidebar";
import { RecordingDashboard } from "./features/recordings/RecordingDashboard";
import { WorkspaceProvider } from "./workspace/WorkspaceProvider";

const THEME_STORAGE_KEY = "metricbreak-theme";
const CHART_PLOT_CONTEXT_ATTR = "data-chart-plot";

function isChartPlotContextMenu(target: EventTarget | null): boolean {
  return target instanceof Element && Boolean(target.closest(`[${CHART_PLOT_CONTEXT_ATTR}]`));
}

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

  const [section, setSection] = useState<AppSection>("dashboard");

  const theme = useMemo(
    () => createMetricBreakTheme(mode),
    [mode]
  );

  useEffect(() => {
    const onContextMenu = (event: MouseEvent) => {
      if (isChartPlotContextMenu(event.target)) return;
      event.preventDefault();
    };

    document.addEventListener("contextmenu", onContextMenu, { capture: true });
    return () => document.removeEventListener("contextmenu", onContextMenu, { capture: true });
  }, []);

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
        <WorkspaceProvider>
          <AppShell
            onToggleTheme={toggleTheme}
            selected={section}
            onSelectSection={setSection}
          >
            <RecordingDashboard
              section={section}
              onSelectSection={setSection}
            />
          </AppShell>
        </WorkspaceProvider>
      </Box>
    </ThemeProvider>
  );
}
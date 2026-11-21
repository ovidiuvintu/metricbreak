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
import { readAppUrlState, writeAppUrlState } from "./app/appNavigationUrl";
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

  const initialUrl = readAppUrlState();
  const [section, setSection] = useState<AppSection>(
    initialUrl.section ?? "dashboard",
  );
  const [deepLinkInvestigation, setDeepLinkInvestigation] = useState<{
    investigationId: string;
    projectId: string;
  } | null>(
    initialUrl.investigationId
      ? {
          investigationId: initialUrl.investigationId,
          projectId: initialUrl.projectId ?? "",
        }
      : null,
  );

  const selectSection = (next: AppSection) => {
    setSection(next);
    writeAppUrlState({ section: next });
  };

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
            onSelectSection={selectSection}
          >
            <RecordingDashboard
              section={section}
              onSelectSection={selectSection}
              themeMode={mode}
              onToggleTheme={toggleTheme}
              deepLinkInvestigation={deepLinkInvestigation}
              onDeepLinkHandled={() => setDeepLinkInvestigation(null)}
              onInvestigationOpened={(investigationId, projectId) => {
                setDeepLinkInvestigation({ investigationId, projectId });
                writeAppUrlState({
                  section: "recordings",
                  investigationId,
                  projectId,
                });
              }}
            />
          </AppShell>
        </WorkspaceProvider>
      </Box>
    </ThemeProvider>
  );
}
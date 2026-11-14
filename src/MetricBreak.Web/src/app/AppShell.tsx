
import { useState, type ReactNode } from "react";
import { Box } from "@mui/material";

import { AppToolbar } from "./AppToolbar";
import { AppSidebar, type AppSection } from "./AppSidebar";

interface AppShellProps {
  children: ReactNode;
  onToggleTheme: () => void;
}

export function AppShell({
  children,
  onToggleTheme,
}: AppShellProps) {
  const [selected, setSelected] =
    useState<AppSection>("recordings");

  const [mobileOpen, setMobileOpen] = useState(false);

  const handleSelectSection = (section: AppSection) => {
    setSelected(section);
    setMobileOpen(false);
  };

  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        width: "100%",
        minHeight: "100vh",
        m: 0,
        p: 0,
        bgcolor: "background.default",
      }}
    >
      {/* Application toolbar */}
      <AppToolbar
        onToggleTheme={onToggleTheme}
        onOpenSidebar={() => setMobileOpen(true)}
      />

      {/* Sidebar and workspace */}
      <Box
        sx={{
          display: "flex",
          flex: 1,
          width: "100%",
          minWidth: 0,
          minHeight: 0,
        }}
      >
        {/* Navigation sidebar */}
        <AppSidebar
          selected={selected}
          onSelect={handleSelectSection}
          mobileOpen={mobileOpen}
          onMobileClose={() => setMobileOpen(false)}
        />

        {/* Main application workspace */}
        <Box
          component="main"
          sx={{
            flex: 1,
            minWidth: 0,
            width: 0,

            // Small horizontal spacing from the edges
            px: { xs: 1.5, md: 2 },

            // Top spacing only
            pt: 2,
            pb: 0,

            m: 0,
            overflowX: "hidden",
            overflowY: "auto",
            bgcolor: "background.default",
          }}
        >
          {children}
        </Box>
      </Box>
    </Box>
  );
}

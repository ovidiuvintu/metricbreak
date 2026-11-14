
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
        height: "100dvh",
        overflow: "hidden",
        bgcolor: "background.default",
      }}
    >
      <AppToolbar
        onToggleTheme={onToggleTheme}
        onOpenSidebar={() => setMobileOpen(true)}
      />

      <Box
        sx={{
          display: "flex",
          flex: 1,
          minWidth: 0,
          minHeight: 0,
          overflow: "hidden",
        }}
      >
        <AppSidebar
          selected={selected}
          onSelect={handleSelectSection}
          mobileOpen={mobileOpen}
          onMobileClose={() => setMobileOpen(false)}
        />

        <Box
          component="main"
          sx={{
            display: "flex",
            flexDirection: "column",
            flex: 1,
            minWidth: 0,
            minHeight: 0,
            px: { xs: 1, md: 2 },
            py: 1,
            overflow: "hidden",
            bgcolor: "background.default",
          }}
        >
          {children}
        </Box>
      </Box>
    </Box>
  );
}

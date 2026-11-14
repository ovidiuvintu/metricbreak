
import type { ReactNode } from "react";

import { Box, Container } from "@mui/material";

import { AppToolbar } from "./AppToolbar";

interface AppShellProps {
  children: ReactNode;
  onToggleTheme: () => void;
}

export function AppShell({
  children,
  onToggleTheme,
}: AppShellProps) {
  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "flex",
        flexDirection: "column",
        bgcolor: "background.default",
      }}
    >
      <AppToolbar onToggleTheme={onToggleTheme} />

      <Container
        component="main"
        maxWidth={false}
        sx={{
          flexGrow: 1,
          py: 3,
          px: { xs: 2, md: 3 },
        }}
      >
        {children}
      </Container>
    </Box>
  );
}

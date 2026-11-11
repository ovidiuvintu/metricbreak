
import { ThemeProvider } from "@mui/material/styles";
import { theme } from "./app/theme";
import { AppShell } from "./app/AppShell";
import { ProjectDashboard } from "./features/projects/ProjectDashboard";

export default function App() {
  return (
    <ThemeProvider theme={theme}>
      <AppShell>
        <ProjectDashboard />
      </AppShell>
    </ThemeProvider>
  );
}

import { ThemeProvider } from "@mui/material/styles";
import { theme } from "./app/theme";
import { AppShell } from "./app/AppShell";
import { RecordingDashboard } from "./features/recordings/RecordingDashboard";

export default function App() {
  return (
    <ThemeProvider theme={theme}>
      <AppShell>
        <RecordingDashboard />
      </AppShell>
    </ThemeProvider>
  );
}

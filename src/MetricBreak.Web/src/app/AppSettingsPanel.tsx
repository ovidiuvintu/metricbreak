import {
  Box,
  Button,
  Card,
  CardContent,
  FormControlLabel,
  Stack,
  Switch,
  Typography,
} from "@mui/material";
import type { PaletteMode } from "@mui/material";

interface AppSettingsPanelProps {
  themeMode: PaletteMode;
  onToggleTheme: () => void;
  slidingWindow: boolean;
  onSlidingWindowChange: (enabled: boolean) => void;
}

export function AppSettingsPanel({
  themeMode,
  onToggleTheme,
  slidingWindow,
  onSlidingWindowChange,
}: AppSettingsPanelProps) {
  return (
    <Stack spacing={3} sx={{ maxWidth: 560, p: { xs: 1, md: 2 } }}>
      <Box>
        <Typography variant="h4">Settings</Typography>
        <Typography color="text.secondary">
          Application preferences for this browser.
        </Typography>
      </Box>

      <Card variant="outlined">
        <CardContent>
          <Typography variant="subtitle1" gutterBottom>
            Appearance
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Current theme: {themeMode === "light" ? "Light" : "Dark"}
          </Typography>
          <Button variant="outlined" onClick={onToggleTheme}>
            Switch to {themeMode === "light" ? "dark" : "light"} mode
          </Button>
        </CardContent>
      </Card>

      <Card variant="outlined">
        <CardContent>
          <Typography variant="subtitle1" gutterBottom>
            Chart
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            While playback is running, the chart can follow the cursor by sliding
            the visible samples. Turn this off to stay on fixed pages and use
            Previous and Next.
          </Typography>
          <FormControlLabel
            control={
              <Switch
                checked={slidingWindow}
                onChange={(_, checked) => onSlidingWindowChange(checked)}
              />
            }
            label="Slide the chart with playback"
          />
        </CardContent>
      </Card>
    </Stack>
  );
}

import {
  AppBar,
  Box,
  IconButton,
  Toolbar,
  Tooltip,
  Typography,
} from "@mui/material";

import { useTheme } from "@mui/material/styles";

import LightModeOutlinedIcon from "@mui/icons-material/LightModeOutlined";
import DarkModeOutlinedIcon from "@mui/icons-material/DarkModeOutlined";

interface AppToolbarProps {
  onToggleTheme: () => void;
}

export function AppToolbar({
  onToggleTheme,
}: AppToolbarProps) {
  const theme = useTheme();
  const isDark = theme.palette.mode === "dark";

  return (
    <AppBar
      position="static"
      elevation={0}
      sx={{
        bgcolor: "background.paper",
        color: "text.primary",
        borderBottom: 1,
        borderColor: "divider",
      }}
    >
      <Toolbar sx={{ minHeight: 64 }}>
        <Typography
          variant="h6"
          component="div"
          sx={{
            fontWeight: 700,
            color: "primary.main",
          }}
        >
          MetricBreak
        </Typography>

        {/* Push toolbar actions to the far right */}
        <Box sx={{ flexGrow: 1 }} />

        <Tooltip
          title={
            isDark
              ? "Switch to light mode"
              : "Switch to dark mode"
          }
        >
          <IconButton
            onClick={onToggleTheme}
            color="primary"
            aria-label={
              isDark
                ? "Switch to light mode"
                : "Switch to dark mode"
            }
          >
            {isDark ? (
              <LightModeOutlinedIcon />
            ) : (
              <DarkModeOutlinedIcon />
            )}
          </IconButton>
        </Tooltip>
      </Toolbar>
    </AppBar>
  );
}


import { useState } from "react";
import {
  Box,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Tooltip,
  useMediaQuery,
  useTheme,
} from "@mui/material";

import DashboardOutlinedIcon from "@mui/icons-material/DashboardOutlined";
import StorageOutlinedIcon from "@mui/icons-material/StorageOutlined";
import ManageSearchOutlinedIcon from "@mui/icons-material/ManageSearchOutlined";
import AnalyticsOutlinedIcon from "@mui/icons-material/AnalyticsOutlined";
import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";

export type AppSection =
  | "dashboard"
  | "recordings"
  | "investigations"
  | "analysis"
  | "settings";

const navigation = [
  { id: "dashboard", label: "Dashboard", icon: DashboardOutlinedIcon },
  { id: "recordings", label: "Recordings", icon: StorageOutlinedIcon },
  { id: "investigations", label: "Investigations", icon: ManageSearchOutlinedIcon },
  { id: "analysis", label: "Analysis", icon: AnalyticsOutlinedIcon },
  { id: "settings", label: "Settings", icon: SettingsOutlinedIcon },
] as const;

interface AppSidebarProps {
  selected: AppSection;
  onSelect: (section: AppSection) => void;
  mobileOpen: boolean;
  onMobileClose: () => void;
}

export function AppSidebar({
  selected,
  onSelect,
  mobileOpen,
  onMobileClose,
}: AppSidebarProps) {
  const theme = useTheme();
  const mobile = useMediaQuery(theme.breakpoints.down("md"));
  const [collapsed, setCollapsed] = useState(false);

  const width = collapsed ? 72 : 240;

  const content = (
    <Box
      sx={{
        width: mobile ? 240 : width,
        height: "100%",
        display: "flex",
        flexDirection: "column",
        bgcolor: "background.paper",
        transition: theme.transitions.create("width"),
      }}
    >
      {!mobile && (
        <Box sx={{ display: "flex", justifyContent: "flex-end", p: 1 }}>
          <Tooltip title={collapsed ? "Expand sidebar" : "Collapse sidebar"}>
            <IconButton
              onClick={() => setCollapsed((value) => !value)}
              size="small"
              aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            >
              {collapsed ? <ChevronRightIcon /> : <ChevronLeftIcon />}
            </IconButton>
          </Tooltip>
        </Box>
      )}

      <Divider />

      <List sx={{ px: 1, py: 1 }}>
        {navigation.map(({ id, label, icon: Icon }) => (
          <Tooltip
            key={id}
            title={collapsed && !mobile ? label : ""}
            placement="right"
          >
            <ListItemButton
              selected={selected === id}
              onClick={() => {
                onSelect(id);
                if (mobile) onMobileClose();
              }}
              sx={{
                minHeight: 44,
                borderRadius: 1,
                justifyContent:
                  collapsed && !mobile ? "center" : "flex-start",
                px: 1.5,
                mb: 0.5,
                "&.Mui-selected": {
                  bgcolor: "primary.main",
                  color: "primary.contrastText",
                  "&:hover": {
                    bgcolor: "primary.main",
                  },
                  "& .MuiListItemIcon-root": {
                    color: "inherit",
                  },
                },
              }}
            >
              <ListItemIcon
                sx={{
                  minWidth: collapsed && !mobile ? 0 : 38,
                  color: "inherit",
                }}
              >
                <Icon fontSize="small" />
              </ListItemIcon>

              {(!collapsed || mobile) && (
                <ListItemText
                  primary={label}
                  primaryTypographyProps={{
                    fontSize: 14,
                    fontWeight: selected === id ? 600 : 400,
                  }}
                />
              )}
            </ListItemButton>
          </Tooltip>
        ))}
      </List>
    </Box>
  );

  if (mobile) {
    return (
      <Drawer
        variant="temporary"
        open={mobileOpen}
        onClose={onMobileClose}
        ModalProps={{ keepMounted: true }}
      >
        {content}
      </Drawer>
    );
  }

  return (
    <Box
      component="aside"
      sx={{
        width,
        flexShrink: 0,
        borderRight: 1,
        borderColor: "divider",
        transition: theme.transitions.create("width"),
      }}
    >
      {content}
    </Box>
  );
}

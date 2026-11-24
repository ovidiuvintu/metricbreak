
import { useState } from "react";
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  ListSubheader,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";

import DashboardOutlinedIcon from "@mui/icons-material/DashboardOutlined";
import StorageOutlinedIcon from "@mui/icons-material/StorageOutlined";
import ManageSearchOutlinedIcon from "@mui/icons-material/ManageSearchOutlined";
import AnalyticsOutlinedIcon from "@mui/icons-material/AnalyticsOutlined";
import FolderOpenOutlinedIcon from "@mui/icons-material/FolderOpenOutlined";
import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";

import { useWorkspace } from "../workspace/WorkspaceProvider";
import { CreateProjectDialog } from "../features/projects/CreateProjectDialog";
import { OpenProjectDialog } from "../features/projects/OpenProjectDialog";

export type AppSection =
  | "dashboard"
  | "recordings"
  | "investigations"
  | "analysis"
  | "projects"
  | "settings";

const projectNavigation = [
  { id: "recordings", label: "Recordings", icon: StorageOutlinedIcon },
  { id: "investigations", label: "Investigations", icon: ManageSearchOutlinedIcon },
  { id: "analysis", label: "Analysis", icon: AnalyticsOutlinedIcon },
] as const;

const generalNavigation = [
  { id: "dashboard", label: "Dashboard", icon: DashboardOutlinedIcon },
  { id: "projects", label: "Projects", icon: FolderOpenOutlinedIcon },
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
  const [promptOpen, setPromptOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [openOpen, setOpenOpen] = useState(false);
  const { activeProject, setActiveProject } = useWorkspace();
  const showLabels = !collapsed || mobile;
  const workspaceLabel = activeProject?.projectName ?? "Quick session";

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

      <List
        sx={{ px: 1, py: 1 }}
        subheader={showLabels ? (
          <ListSubheader
            disableSticky
            sx={{ bgcolor: "transparent", lineHeight: "32px", px: 1.5 }}
          >
            {workspaceLabel}
          </ListSubheader>
        ) : undefined}
      >
        {!activeProject && (
          <Tooltip title={showLabels ? "" : "Open a project or use quick CSV analysis"} placement="right">
            <ListItemButton
              onClick={() => setPromptOpen(true)}
              sx={{ minHeight: 44, borderRadius: 1, justifyContent: showLabels ? "flex-start" : "center", px: 1.5, mb: 0.5 }}
            >
              <ListItemIcon sx={{ minWidth: showLabels ? 38 : 0 }}>
                <FolderOpenOutlinedIcon fontSize="small" />
              </ListItemIcon>
              {showLabels && (
                <ListItemText
                  primary="Select a project"
                  secondary="Optional for quick CSV analysis"
                  slotProps={{ primary: { sx: { fontSize: 14 } }, secondary: { sx: { fontSize: 12 } } }}
                />
              )}
            </ListItemButton>
          </Tooltip>
        )}
        {projectNavigation.map(({ id, label, icon: Icon }) => (
          <Tooltip
            key={id}
            title={showLabels ? "" : label}
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
                justifyContent: showLabels ? "flex-start" : "center",
                px: 1.5,
                mb: 0.5,
                "&.Mui-selected": {
                  bgcolor: "primary.main",
                  color: "primary.contrastText",
                  "&:hover": { bgcolor: "primary.main" },
                  "& .MuiListItemIcon-root": { color: "inherit" },
                },
              }}
            >
              <ListItemIcon sx={{ minWidth: showLabels ? 38 : 0, color: "inherit" }}>
                <Icon fontSize="small" />
              </ListItemIcon>
              {showLabels && (
                <ListItemText
                  primary={label}
                  slotProps={{ primary: { sx: { fontSize: 14, fontWeight: selected === id ? 600 : 400 } } }}
                />
              )}
            </ListItemButton>
          </Tooltip>
        ))}
      </List>

      <Divider />

      <List sx={{ px: 1, py: 1 }}>
        {generalNavigation.map(({ id, label, icon: Icon }) => (
          <Tooltip key={id} title={showLabels ? "" : label} placement="right">
            <ListItemButton
              selected={selected === id}
              onClick={() => {
                onSelect(id);
                if (mobile) onMobileClose();
              }}
              sx={{
                minHeight: 44,
                borderRadius: 1,
                justifyContent: showLabels ? "flex-start" : "center",
                px: 1.5,
                mb: 0.5,
                "&.Mui-selected": {
                  bgcolor: "primary.main",
                  color: "primary.contrastText",
                  "&:hover": { bgcolor: "primary.main" },
                  "& .MuiListItemIcon-root": { color: "inherit" },
                },
              }}
            >
              <ListItemIcon sx={{ minWidth: showLabels ? 38 : 0, color: "inherit" }}>
                <Icon fontSize="small" />
              </ListItemIcon>
              {showLabels && (
                <ListItemText
                  primary={label}
                  slotProps={{ primary: { sx: { fontSize: 14, fontWeight: selected === id ? 600 : 400 } } }}
                />
              )}
            </ListItemButton>
          </Tooltip>
        ))}
      </List>

      <Dialog open={promptOpen} onClose={() => setPromptOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>Select a project</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary">
            Quick CSV analysis works without a project. Create or open a project when you want to save and organize ongoing work.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPromptOpen(false)}>Cancel</Button>
          <Button onClick={() => { setPromptOpen(false); setOpenOpen(true); }}>Open</Button>
          <Button variant="contained" onClick={() => { setPromptOpen(false); setCreateOpen(true); }}>
            New project
          </Button>
        </DialogActions>
      </Dialog>
      <OpenProjectDialog
        open={openOpen}
        onClose={() => setOpenOpen(false)}
        onOpened={(project) => setActiveProject(project)}
      />
      <CreateProjectDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={(project) => setActiveProject(project)}
      />
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

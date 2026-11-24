import { useState } from "react";
import {
  AppBar,
  Box,
  Breadcrumbs,
  Button,
  Divider,
  IconButton,
  Menu,
  MenuItem,
  Toolbar,
  Tooltip,
  Typography,
} from "@mui/material";

import MenuIcon from "@mui/icons-material/Menu";
import ArrowDropDownIcon from "@mui/icons-material/ArrowDropDown";

import { useTheme } from "@mui/material/styles";

import LightModeOutlinedIcon from "@mui/icons-material/LightModeOutlined";
import DarkModeOutlinedIcon from "@mui/icons-material/DarkModeOutlined";

import { useWorkspace } from "../workspace/WorkspaceProvider";
import { projectApi } from "../features/projects/projectApi";
import type { ProjectSummary } from "../features/projects/projectTypes";
import { CreateProjectDialog } from "../features/projects/CreateProjectDialog";
import { OpenProjectDialog } from "../features/projects/OpenProjectDialog";
import { SaveAsProjectDialog } from "../features/projects/SaveAsProjectDialog";
import { investigationApi } from "../features/investigations/investigationApi";

interface AppToolbarProps {
  onToggleTheme: () => void;
  onOpenSidebar: () => void;
}

export function AppToolbar({
  onToggleTheme,
  onOpenSidebar,
}: AppToolbarProps) {
  const theme = useTheme();
  const isDark = theme.palette.mode === "dark";
  const {
    context,
    activeProject,
    projectInvestigations,
    setActiveProject,
    setActiveInvestigation,
    refreshProjectInvestigations,
    requestOpenInvestigation,
  } = useWorkspace();
  const [projectMenuAnchor, setProjectMenuAnchor] = useState<HTMLElement | null>(null);
  const [investigationMenuAnchor, setInvestigationMenuAnchor] = useState<HTMLElement | null>(null);
  const [openProjects, setOpenProjects] = useState<ProjectSummary[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [openOpen, setOpenOpen] = useState(false);
  const [saveOpen, setSaveOpen] = useState(false);
  const [saveBusy, setSaveBusy] = useState(false);
  const [saveError, setSaveError] = useState("");

  const openProjectMenu = async (anchor: HTMLElement) => {
    setProjectMenuAnchor(anchor);
    try {
      setOpenProjects(await projectApi.getOpenProjects());
    } catch {
      setOpenProjects(activeProject ? [activeProject] : []);
    }
  };

  const saveSessionAsProject = async (projectName: string, rootPath: string) => {
    setSaveBusy(true);
    setSaveError("");
    try {
      const project = await projectApi.createProject({ projectName, rootPath });
      const sessionId = context.sessionId;
      if (sessionId) {
        const exported = await investigationApi.exportSession(sessionId, {
          name: projectName,
          projectId: project.projectId,
        });
        setActiveProject(project, { keepSession: true });
        setActiveInvestigation({
          investigationId: exported.investigationId,
          investigationName: projectName,
          projectId: project.projectId,
        });
        await refreshProjectInvestigations();
      } else {
        setActiveProject(project);
      }
      setSaveOpen(false);
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : "Unable to save this session as a project.");
    } finally {
      setSaveBusy(false);
    }
  };

  const investigationLabel =
    context.investigationName?.trim()
    || projectInvestigations.find((item) => item.investigationId === context.investigationId)?.recordingName
    || null;

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
      <Toolbar sx={{ minHeight: 64, gap: 1 }}>
        <IconButton
          onClick={onOpenSidebar}
          aria-label="Open navigation"
          edge="start"
          sx={{ display: { xs: "inline-flex", md: "none" } }}
        >
          <MenuIcon />
        </IconButton>

        <Breadcrumbs
          aria-label="Workspace"
          sx={{ minWidth: 0, flex: 1, "& .MuiBreadcrumbs-ol": { flexWrap: "nowrap" } }}
        >
          <Typography variant="h6" sx={{ fontWeight: 700, color: "primary.main" }}>
            MetricBreak
          </Typography>
          <Button
            color="inherit"
            size="small"
            endIcon={<ArrowDropDownIcon />}
            onClick={(event) => void openProjectMenu(event.currentTarget)}
            aria-haspopup="menu"
            aria-label={activeProject ? `Active project ${activeProject.projectName}` : "Select a project"}
            sx={{ textTransform: "none", maxWidth: { xs: 140, sm: 240 }, fontWeight: 600 }}
          >
            <Box component="span" sx={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              {activeProject?.projectName ?? "Select project"}
            </Box>
          </Button>
          {investigationLabel && (
            <Button
              color="inherit"
              size="small"
              endIcon={<ArrowDropDownIcon />}
              onClick={(event) => setInvestigationMenuAnchor(event.currentTarget)}
              aria-haspopup="menu"
              aria-label={`Active investigation ${investigationLabel}`}
              sx={{
                textTransform: "none",
                maxWidth: { xs: 120, sm: 280 },
                color: "text.secondary",
                display: { xs: "none", sm: "inline-flex" },
              }}
            >
              <Box component="span" sx={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {investigationLabel}
              </Box>
            </Button>
          )}
        </Breadcrumbs>

        <Tooltip title={isDark ? "Switch to light mode" : "Switch to dark mode"}>
          <IconButton
            onClick={onToggleTheme}
            color="primary"
            aria-label={isDark ? "Switch to light mode" : "Switch to dark mode"}
          >
            {isDark ? <LightModeOutlinedIcon /> : <DarkModeOutlinedIcon />}
          </IconButton>
        </Tooltip>
      </Toolbar>

      <Menu
        anchorEl={projectMenuAnchor}
        open={Boolean(projectMenuAnchor)}
        onClose={() => setProjectMenuAnchor(null)}
      >
        {openProjects.length === 0 && (
          <MenuItem disabled>No open projects</MenuItem>
        )}
        {openProjects.map((project) => (
          <MenuItem
            key={project.projectId}
            selected={project.projectId === activeProject?.projectId}
            onClick={() => {
              setProjectMenuAnchor(null);
              if (project.projectId !== activeProject?.projectId) {
                setActiveProject(project);
              }
            }}
          >
            {project.projectName}
          </MenuItem>
        ))}
        <Divider />
        <MenuItem onClick={() => { setProjectMenuAnchor(null); setOpenOpen(true); }}>
          Open project…
        </MenuItem>
        <MenuItem onClick={() => { setProjectMenuAnchor(null); setCreateOpen(true); }}>
          New project…
        </MenuItem>
        <MenuItem
          disabled={!context.sessionId}
          onClick={() => { setProjectMenuAnchor(null); setSaveError(""); setSaveOpen(true); }}
        >
          Save session as project…
        </MenuItem>
      </Menu>

      <Menu
        anchorEl={investigationMenuAnchor}
        open={Boolean(investigationMenuAnchor)}
        onClose={() => setInvestigationMenuAnchor(null)}
      >
        {projectInvestigations.length === 0 && (
          <MenuItem disabled>No investigations in this project</MenuItem>
        )}
        {projectInvestigations.map((investigation) => (
          <MenuItem
            key={investigation.investigationId}
            selected={investigation.investigationId === context.investigationId}
            onClick={() => {
              setInvestigationMenuAnchor(null);
              if (investigation.investigationId !== context.investigationId) {
                requestOpenInvestigation(investigation.investigationId);
              }
            }}
          >
            {investigation.investigationName?.trim() || investigation.recordingName}
          </MenuItem>
        ))}
      </Menu>

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
      <SaveAsProjectDialog
        open={saveOpen}
        defaultName={context.recordingName?.replace(/\.csv$/i, "") ?? ""}
        busy={saveBusy}
        error={saveError}
        onClose={() => {
          if (!saveBusy) setSaveOpen(false);
        }}
        onSave={(projectName, rootPath) => void saveSessionAsProject(projectName, rootPath)}
      />
    </AppBar>
  );
}


import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardActions,
  CardContent,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import FolderOpenIcon from "@mui/icons-material/FolderOpen";
import RefreshIcon from "@mui/icons-material/Refresh";
import { projectApi } from "./projectApi";
import type { ProjectSummary } from "./projectTypes";
import { CreateProjectDialog } from "./CreateProjectDialog";
import { OpenProjectDialog } from "./OpenProjectDialog";

export function ProjectDashboard() {
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [openOpen, setOpenOpen] = useState(false);
  const [closingId, setClosingId] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setError("");
    try {
      const result = await projectApi.getOpenProjects();
      setProjects(result);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unable to load projects.",
      );
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // Initial loading errors are shown in the dashboard.
    void refresh().catch(() => {});
  }, [refresh]);

  async function closeProject(projectId: string) {
    setClosingId(projectId);
    setError("");

    try {
      await projectApi.closeProject(projectId);
      await refresh();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unable to close project.",
      );
    } finally {
      setClosingId(null);
    }
  }

  return (
    <Stack spacing={3}>
      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: 2,
        }}
      >
        <Box>
          <Typography variant="h4">Project Workspace</Typography>
          <Typography color="text.secondary">
            Manage recorded time-series investigations.
          </Typography>
        </Box>

        <Stack direction="row" spacing={1}>
          <Button
            startIcon={<RefreshIcon />}
            onClick={() => void refresh().catch(() => {})}
          >
            Refresh
          </Button>
          <Button
            variant="outlined"
            startIcon={<FolderOpenIcon />}
            onClick={() => setOpenOpen(true)}
          >
            Open
          </Button>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => setCreateOpen(true)}
          >
            New Project
          </Button>
        </Stack>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <CircularProgress />
      ) : projects.length === 0 ? (
        <Card variant="outlined">
          <CardContent sx={{ py: 5, textAlign: "center" }}>
            <Typography variant="h6">No open projects</Typography>
            <Typography color="text.secondary">
              Create a new project or open an existing one.
            </Typography>
          </CardContent>
        </Card>
      ) : (
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "1fr",
              md: "repeat(2, minmax(0, 1fr))",
            },
            gap: 2,
          }}
        >
          {projects.map((project) => (
            <Card key={project.projectId} variant="outlined">
              <CardContent>
                <Typography variant="h6">
                  {project.projectName}
                </Typography>
                <Typography
                  variant="body2"
                  color="text.secondary"
                  sx={{ overflowWrap: "anywhere", mt: 1 }}
                >
                  {project.rootPath}
                </Typography>
                <Typography sx={{ mt: 2 }}>
                  Investigations: {project.investigationCount}
                </Typography>
              </CardContent>
              <CardActions>
                <Button
                  color="error"
                  disabled={closingId !== null}
                  onClick={() => void closeProject(project.projectId)}
                >
                  {closingId === project.projectId
                    ? "Closing..."
                    : "Close Project"}
                </Button>
              </CardActions>
            </Card>
          ))}
        </Box>
      )}

      <CreateProjectDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={refresh}
      />

      <OpenProjectDialog
        open={openOpen}
        onClose={() => setOpenOpen(false)}
        onOpened={refresh}
      />
    </Stack>
  );
}
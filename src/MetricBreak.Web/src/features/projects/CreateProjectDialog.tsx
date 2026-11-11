
import { useState } from "react";
import {
    Alert,
    Button,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    Stack,
    TextField,
} from "@mui/material";
import { projectApi } from "./projectApi";

interface Props {
    open: boolean;
    onClose: () => void;
    onCreated: () => Promise<void>;
}

export function CreateProjectDialog({
    open,
    onClose,
    onCreated,
}: Props) {
    const [name, setName] = useState("");
    const [rootPath, setRootPath] = useState("");
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);

    function close() {
        if (busy) return;
        setError("");
        onClose();
    }

    async function submit() {
        if (!name.trim() || !rootPath.trim()) {
            setError("Project name and root path are required.");
            return;
        }

        setBusy(true);
        setError("");

        try {
            await projectApi.createProject({
                projectName: name.trim(),
                rootPath: rootPath.trim(),
            });

            setName("");
            setRootPath("");
            await onCreated();
            onClose();
        } catch (err) {
            setError(
                err instanceof Error ? err.message : "Unable to create project.",
            );
        } finally {
            setBusy(false);
        }
    }

    return (
        <Dialog open= { open } onClose = { close } fullWidth maxWidth = "sm" >
            <DialogTitle>Create Project </DialogTitle>
                < DialogContent >
                <Stack spacing={ 2 } sx = {{ pt: 1 }
}>
{ error && <Alert severity="error" > { error } </Alert>}
<TextField
            label="Project Name"
value = { name }
onChange = {(e) => setName(e.target.value)}
disabled = { busy }
fullWidth
autoFocus
    />
    <TextField
            label="Root Directory"
placeholder = "C:\MetricBreak\Projects\MyProject"
value = { rootPath }
onChange = {(e) => setRootPath(e.target.value)}
helperText = "Path on the machine running the MetricBreak API."
disabled = { busy }
fullWidth
    />
    </Stack>
    </DialogContent>
    < DialogActions >
    <Button onClick={ close } disabled = { busy } > Cancel </Button>
        < Button
variant = "contained"
onClick = { submit }
disabled = { busy }
    >
{ busy? "Creating...": "Create Project" }
    </Button>
    </DialogActions>
    </Dialog>
  );
}

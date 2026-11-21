import { useEffect, useState } from "react";
import {
    Button,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    TextField,
} from "@mui/material";

interface BookmarkEditorDialogProps {
    open: boolean;
    title: string;
    initialTitle?: string;
    initialAnnotation?: string;
    busy: boolean;
    onClose: () => void;
    onSave: (title: string, annotation: string) => void;
}

export function BookmarkEditorDialog({
    open,
    title,
    initialTitle = "",
    initialAnnotation = "",
    busy,
    onClose,
    onSave,
}: BookmarkEditorDialogProps) {
    const [bookmarkTitle, setBookmarkTitle] = useState(initialTitle);
    const [annotation, setAnnotation] = useState(initialAnnotation);

    useEffect(() => {
        if (!open) return;
        setBookmarkTitle(initialTitle);
        setAnnotation(initialAnnotation);
    }, [open, initialTitle, initialAnnotation]);

    return (
        <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="xs">
            <DialogTitle>{title}</DialogTitle>
            <DialogContent sx={{ display: "flex", flexDirection: "column", gap: 2, pt: 1 }}>
                <TextField
                    label="Title"
                    value={bookmarkTitle}
                    onChange={(event) => setBookmarkTitle(event.target.value)}
                    autoFocus
                    disabled={busy}
                />
                <TextField
                    label="Annotation"
                    value={annotation}
                    onChange={(event) => setAnnotation(event.target.value)}
                    multiline
                    minRows={3}
                    disabled={busy}
                />
            </DialogContent>
            <DialogActions>
                <Button onClick={onClose} disabled={busy}>Cancel</Button>
                <Button
                    variant="contained"
                    disabled={busy}
                    onClick={() => onSave(bookmarkTitle, annotation)}
                >
                    Save
                </Button>
            </DialogActions>
        </Dialog>
    );
}
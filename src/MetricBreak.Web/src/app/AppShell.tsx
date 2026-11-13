
import type { ReactNode } from "react";
import {
    AppBar,
    Box,
    CssBaseline,
    Divider,
    Drawer,
    List,
    ListItemButton,
    ListItemText,
    Toolbar,
    Typography,
} from "@mui/material";

const drawerWidth = 230;

const navigation = [
    "Workspace",
    "Recordings",
    "Breakpoints",
    "Investigations",
];

interface AppShellProps {
    children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
    return (
        <Box sx={{ display: "flex", minHeight: "100vh" }}>
            <CssBaseline />

            <AppBar
                position="fixed"
                sx={{
                    zIndex: (theme) => theme.zIndex.drawer + 1,
                }}
            >
                <Toolbar>
                    <Typography
                        variant="h6"
                        sx={{ fontWeight: 700 }}
                    >
                        MetricBreak
                    </Typography>

                    <Typography sx={{ ml: 2, opacity: 0.75 }}>
                        Time-Series Debugger
                    </Typography>
                </Toolbar>
            </AppBar>

            <Drawer
                variant="permanent"
                sx={{
                    width: drawerWidth,
                    flexShrink: 0,
                    "& .MuiDrawer-paper": {
                        width: drawerWidth,
                        boxSizing: "border-box",
                    },
                }}
            >
                <Toolbar />
                <Divider />

                <List>
                    {navigation.map((item, index) => (
                        <ListItemButton
                            key={item}
                            selected={index === 0}
                            disabled={index !== 0}
                        >
                            <ListItemText primary={item} />
                        </ListItemButton>
                    ))}
                </List>
            </Drawer>

            <Box
                component="main"
                sx={{
                    flexGrow: 1,
                    p: 3,
                }}
            >
                <Toolbar />
                {children}
            </Box>
        </Box>
    );
}

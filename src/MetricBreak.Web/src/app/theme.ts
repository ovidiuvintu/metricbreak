
import { createTheme } from "@mui/material/styles";

export const theme = createTheme({
    palette: {
        mode: "dark",
        primary: { main: "#90caf9" },
        background: {
            default: "#101418",
            paper: "#1b2229",
        },
    },
    shape: {
        borderRadius: 8,
    },
    typography: {
        fontFamily: "Inter, Roboto, Arial, sans-serif",
    },
});

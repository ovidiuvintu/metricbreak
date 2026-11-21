import type { AppSection } from "./AppSidebar";

export interface AppUrlState {
    section?: AppSection;
    investigationId?: string | null;
    projectId?: string | null;
}

const validSections: AppSection[] = [
    "dashboard",
    "recordings",
    "investigations",
    "analysis",
    "projects",
    "settings",
];

export function readAppUrlState(): AppUrlState {
    const params = new URLSearchParams(window.location.search);
    const sectionParam = params.get("section");
    const section = validSections.includes(sectionParam as AppSection)
        ? (sectionParam as AppSection)
        : undefined;

    return {
        section,
        investigationId: params.get("investigationId"),
        projectId: params.get("projectId"),
    };
}

export function writeAppUrlState(state: AppUrlState): void {
    const params = new URLSearchParams();

    if (state.section && state.section !== "dashboard") {
        params.set("section", state.section);
    }

    const investigationId = state.investigationId?.trim();
    if (investigationId) {
        params.set("investigationId", investigationId);
    }

    const projectId = state.projectId?.trim();
    if (projectId) {
        params.set("projectId", projectId);
    }

    const query = params.toString();
    const nextUrl = query
        ? `${window.location.pathname}?${query}`
        : window.location.pathname;

    window.history.replaceState(null, "", nextUrl);
}
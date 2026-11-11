
import { apiRequest } from "../../shared/api/httpClient";
import type {
    CreateProjectRequest,
    OpenProjectRequest,
    ProjectSummary,
} from "./projectTypes";

export const projectApi = {
    getOpenProjects(): Promise<ProjectSummary[]> {
        return apiRequest<ProjectSummary[]>("/api/projects/open");
    },

    createProject(request: CreateProjectRequest): Promise<ProjectSummary> {
        return apiRequest<ProjectSummary>("/api/projects", {
            method: "POST",
            body: JSON.stringify(request),
        });
    },

    openProject(request: OpenProjectRequest): Promise<ProjectSummary> {
        return apiRequest<ProjectSummary>("/api/projects/open", {
            method: "POST",
            body: JSON.stringify(request),
        });
    },

    closeProject(projectId: string): Promise<void> {
        return apiRequest<void>(
            `/api/projects/${encodeURIComponent(projectId)}/close`,
            { method: "POST" },
        );
    },
};

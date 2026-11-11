
export interface ProjectSummary {
    projectId: string;
    projectName: string;
    rootPath: string;
    investigationCount: number;
}

export interface CreateProjectRequest {
    projectName: string;
    rootPath: string;
}

export interface OpenProjectRequest {
    rootPath: string;
}

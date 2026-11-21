import { useEffect, useState } from "react";

import { investigationApi } from "./investigationApi";
import { loadInvestigationSelection } from "./investigationSelectionStorage";
import type { InvestigationBookmark } from "./investigationTypes";

export function useActiveInvestigationBookmarks(
    recordingName: string | null,
    workspaceInvestigationId: string | null,
    workspaceProjectId: string | null,
    refreshKey = 0,
): InvestigationBookmark[] {
    const [bookmarks, setBookmarks] = useState<InvestigationBookmark[]>([]);

    useEffect(() => {
        if (!recordingName) {
            setBookmarks([]);
            return;
        }

        const selection = loadInvestigationSelection(recordingName);
        const investigationId =
            workspaceInvestigationId?.trim()
            || selection?.investigationId
            || null;

        const projectId =
            workspaceProjectId?.trim()
            || selection?.projectId
            || null;

        if (!investigationId) {
            setBookmarks([]);
            return;
        }

        let cancelled = false;

        void (async () => {
            try {
                const response = await investigationApi.listBookmarks(
                    investigationId,
                    projectId,
                );
                if (!cancelled) {
                    setBookmarks(response.bookmarks);
                }
            } catch {
                if (!cancelled) {
                    setBookmarks([]);
                }
            }
        })();

        return () => {
            cancelled = true;
        };
    }, [recordingName, workspaceInvestigationId, workspaceProjectId, refreshKey]);

    return bookmarks;
}
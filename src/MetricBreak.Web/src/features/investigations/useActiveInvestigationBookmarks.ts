import { useEffect, useState } from "react";

import { investigationApi } from "./investigationApi";
import { loadInvestigationSelection } from "./investigationSelectionStorage";
import type { InvestigationBookmark } from "./investigationTypes";

export function useActiveInvestigationBookmarks(
    recordingName: string | null,
    refreshKey = 0,
): InvestigationBookmark[] {
    const [bookmarks, setBookmarks] = useState<InvestigationBookmark[]>([]);

    useEffect(() => {
        if (!recordingName) {
            setBookmarks([]);
            return;
        }

        const selection = loadInvestigationSelection(recordingName);
        if (!selection) {
            setBookmarks([]);
            return;
        }

        let cancelled = false;

        void (async () => {
            try {
                const response = await investigationApi.listBookmarks(
                    selection.investigationId,
                    selection.projectId,
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
    }, [recordingName, refreshKey]);

    return bookmarks;
}
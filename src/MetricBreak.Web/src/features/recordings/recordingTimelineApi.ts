import { apiRequest } from "../../shared/api/httpClient";
import type { RecordingTimeline } from "./recordingTimelineTypes";

export const recordingTimelineApi = {
  getForSession(sessionId: string): Promise<RecordingTimeline> {
    return apiRequest<RecordingTimeline>(
      `/api/sessions/${encodeURIComponent(sessionId)}/timeline`,
    );
  },
};
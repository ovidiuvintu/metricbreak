
import { apiRequest } from "../../shared/api/httpClient";
import type { SessionState } from "./sessionTypes";

export interface SeekResponse {
  session: SessionState;
}

export interface StepForwardResponse {
  advanced: boolean;
  session: SessionState;
}

export interface StepBackwardResponse {
  movedBackward: boolean;
  session: SessionState;
}

export interface ResetResponse {
  session: SessionState;
}

export const replayApi = {
  seek(
    sessionId: string,
    sequenceNumber: number,
  ): Promise<SeekResponse> {
    if (
      !Number.isSafeInteger(sequenceNumber) ||
      sequenceNumber < 0
    ) {
      throw new Error("Invalid sequence number.");
    }

    return apiRequest<SeekResponse>(
      `/api/sessions/${encodeURIComponent(sessionId)}/seek`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ sequenceNumber }),
      },
    );
  },

  stepForward(
    sessionId: string,
  ): Promise<StepForwardResponse> {
    return apiRequest<StepForwardResponse>(
      `/api/sessions/${encodeURIComponent(sessionId)}/step-forward`,
      { method: "POST" },
    );
  },

  stepBackward(
    sessionId: string,
  ): Promise<StepBackwardResponse> {
    return apiRequest<StepBackwardResponse>(
      `/api/sessions/${encodeURIComponent(sessionId)}/step-backward`,
      { method: "POST" },
    );
  },

  reset(sessionId: string): Promise<ResetResponse> {
    return apiRequest<ResetResponse>(
      `/api/sessions/${encodeURIComponent(sessionId)}/reset`,
      { method: "POST" },
    );
  },
};

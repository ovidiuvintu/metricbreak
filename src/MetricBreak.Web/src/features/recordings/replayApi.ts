
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

  nextChange(sessionId: string, channelId: string): Promise<ChangeNavigationResponse> {
    return changeNavigationRequest(sessionId, channelId, "next-change");
  },

  previousChange(sessionId: string, channelId: string): Promise<ChangeNavigationResponse> {
    return changeNavigationRequest(sessionId, channelId, "previous-change");
  },

  reset(sessionId: string): Promise<ResetResponse> {
    return apiRequest<ResetResponse>(
      `/api/sessions/${encodeURIComponent(sessionId)}/reset`,
      { method: "POST" },
    );
  },

  continue(sessionId: string): Promise<ContinueResponse> {
    return apiRequest<ContinueResponse>(
      `/api/sessions/${encodeURIComponent(sessionId)}/continue`,
      { method: "POST" },
    );
  },
};

export interface ContinueResponse {
  executed: boolean;
  hitBreakpoint: boolean;
  reachedEndOfRecording: boolean;
  stop: unknown | null;
  session: SessionState;
}

export interface ChangeNavigationResponse {
  foundChange: boolean;
  session: SessionState;
}

function changeNavigationRequest(
  sessionId: string,
  channelId: string,
  direction: "next-change" | "previous-change",
): Promise<ChangeNavigationResponse> {
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(channelId)) {
    throw new Error("Invalid channel ID.");
  }
  return apiRequest<ChangeNavigationResponse>(
    `/api/sessions/${encodeURIComponent(sessionId)}/${direction}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ channelId }),
    },
  );
}

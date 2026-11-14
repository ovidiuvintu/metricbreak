
import { apiRequest } from "../../shared/api/httpClient";

export type ComparisonOperator =
  | "GreaterThan"
  | "GreaterThanOrEqual"
  | "LessThan"
  | "LessThanOrEqual"
  | "Equal"
  | "NotEqual";

export interface CreateBreakpointRequest {
  channelId: string;
  operator: ComparisonOperator;
  threshold: number;
}

export interface Breakpoint {
  breakpointId: string;
  channelId: string;
  operator: ComparisonOperator;
  threshold: number;
  isEnabled: boolean;
}

export interface BreakpointsResponse {
  breakpoints: Breakpoint[];
}

const sessionPath = (sessionId: string) =>
  `/api/sessions/${encodeURIComponent(sessionId)}/breakpoints`;

export const breakpointApi = {
  create(
    sessionId: string,
    request: CreateBreakpointRequest,
  ): Promise<Breakpoint> {
    if (!Number.isFinite(request.threshold)) {
      throw new Error("Breakpoint threshold must be finite.");
    }

    return apiRequest<Breakpoint>(sessionPath(sessionId), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });
  },

  list(sessionId: string): Promise<BreakpointsResponse> {
    return apiRequest<BreakpointsResponse>(sessionPath(sessionId));
  },

  setEnabled(
    sessionId: string,
    breakpointId: string,
    isEnabled: boolean,
  ): Promise<void> {
    return apiRequest<void>(
      `${sessionPath(sessionId)}/${encodeURIComponent(breakpointId)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ isEnabled }),
      },
    );
  },

  delete(
    sessionId: string,
    breakpointId: string,
  ): Promise<void> {
    return apiRequest<void>(
      `${sessionPath(sessionId)}/${encodeURIComponent(breakpointId)}`,
      { method: "DELETE" },
    );
  },
};

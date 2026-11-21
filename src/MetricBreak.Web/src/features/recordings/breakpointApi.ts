import { apiRequest } from "../../shared/api/httpClient";

export type ComparisonOperator =
  | "GreaterThan"
  | "GreaterThanOrEqual"
  | "LessThan"
  | "LessThanOrEqual"
  | "Equal"
  | "NotEqual"
  | "Changed"
  | "RisingEdge"
  | "FallingEdge";

export type LogicalOperator = "And" | "Or";

export interface CreateBreakpointRequest {
  channelId: string;
  operator: ComparisonOperator;
  threshold: number;
  value?: string;
}

export interface Breakpoint extends CreateBreakpointRequest {
  breakpointId: string;
  isEnabled: boolean;
}

export interface BreakpointCondition extends CreateBreakpointRequest {}

export interface CreateCompositeBreakpointRequest {
  logicalOperator: LogicalOperator;
  conditions: BreakpointCondition[];
}

export interface CompositeBreakpoint extends CreateCompositeBreakpointRequest {
  breakpointId: string;
  isEnabled: boolean;
}

export interface BreakpointsResponse {
  breakpoints: Breakpoint[];
  compositeBreakpoints: CompositeBreakpoint[];
}

const sessionPath = (sessionId: string) =>
  `/api/sessions/${encodeURIComponent(sessionId)}/breakpoints`;

export const breakpointApi = {
  create(sessionId: string, request: CreateBreakpointRequest): Promise<Breakpoint> {
    if (!Number.isFinite(request.threshold)) {
      throw new Error("Breakpoint threshold must be finite.");
    }
    return apiRequest<Breakpoint>(sessionPath(sessionId), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });
  },

  createComposite(
    sessionId: string,
    request: CreateCompositeBreakpointRequest,
  ): Promise<CompositeBreakpoint> {
    if (request.logicalOperator !== "And" && request.logicalOperator !== "Or") {
      throw new Error("Invalid composite logical operator.");
    }
    if (request.conditions.length < 2) {
      throw new Error("Composite breakpoints require at least two conditions.");
    }
    if (request.conditions.some(condition =>
      !condition.channelId || !Number.isFinite(condition.threshold))) {
      throw new Error("Each condition needs a channel and finite threshold.");
    }
    return apiRequest<CompositeBreakpoint>(`${sessionPath(sessionId)}/composite`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });
  },

  list(sessionId: string): Promise<BreakpointsResponse> {
    return apiRequest<BreakpointsResponse>(sessionPath(sessionId));
  },

  setEnabled(sessionId: string, breakpointId: string, isEnabled: boolean): Promise<void> {
    return apiRequest<void>(
      `${sessionPath(sessionId)}/${encodeURIComponent(breakpointId)}`,
      {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ isEnabled }),
      },
    );
  },

  delete(sessionId: string, breakpointId: string): Promise<void> {
    return apiRequest<void>(
      `${sessionPath(sessionId)}/${encodeURIComponent(breakpointId)}`,
      { method: "DELETE" },
    );
  },

  runTo(
    sessionId: string,
    breakpointId: string,
    direction: "forward" | "backward",
  ): Promise<BreakpointRunResponse> {
    return apiRequest<BreakpointRunResponse>(
      `${sessionPath(sessionId)}/${encodeURIComponent(breakpointId)}/run`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          direction: direction === "forward" ? "Forward" : "Backward",
        }),
      },
    );
  },
};

export interface BreakpointRunResponse {
  executed: boolean;
  hitBreakpoint: boolean;
  reachedEndOfRecording: boolean;
  reachedStartOfRecording?: boolean;
  stop: unknown | null;
  session: import("./sessionTypes").SessionState;
}

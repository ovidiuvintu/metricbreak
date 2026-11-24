import { investigationApi } from "./investigationApi";
import { loadInvestigationSelection } from "./investigationSelectionStorage";
import type { InvestigationBookmark } from "./investigationTypes";

export type QuickBookmarkFailure =
  | "no_recording"
  | "no_selection"
  | "no_sample";

export type QuickBookmarkResult =
  | { ok: true; bookmark: InvestigationBookmark }
  | { ok: false; reason: QuickBookmarkFailure; message: string };

export async function tryQuickBookmark(params: {
  recordingName: string;
  investigationId?: string | null;
  projectId?: string | null;
  sampleSequence: number;
  timestampNanoseconds: number | string;
  title?: string | null;
  annotation?: string | null;
  ensureInvestigation?: () => Promise<{
    investigationId: string;
    projectId: string | null;
  } | null>;
}): Promise<QuickBookmarkResult> {
  if (!params.recordingName.trim()) {
    return {
      ok: false,
      reason: "no_recording",
      message: "No recording is loaded.",
    };
  }

  let investigationId = params.investigationId?.trim()
    || loadInvestigationSelection(params.recordingName)?.investigationId;

  let projectId = params.projectId?.trim()
    ?? loadInvestigationSelection(params.recordingName)?.projectId
    ?? null;

  if (!investigationId && params.ensureInvestigation) {
    const ensured = await params.ensureInvestigation();
    investigationId = ensured?.investigationId;
    if (ensured) projectId = ensured.projectId;
  }

  if (!investigationId) {
    return {
      ok: false,
      reason: "no_selection",
      message: "Save or select an investigation first (Investigations tab).",
    };
  }

  if (!Number.isFinite(params.sampleSequence)) {
    return {
      ok: false,
      reason: "no_sample",
      message: "No sample position is available to bookmark.",
    };
  }

  const bookmark = await investigationApi.createBookmark(
    investigationId,
    {
      sampleSequence: params.sampleSequence,
      timestampNanoseconds:
        typeof params.timestampNanoseconds === "string"
          ? Number(params.timestampNanoseconds)
          : params.timestampNanoseconds,
      title: params.title?.trim() || `Sample ${params.sampleSequence.toLocaleString()}`,
      annotation: params.annotation?.trim() || null,
    },
    projectId,
  );

  return { ok: true, bookmark };
}
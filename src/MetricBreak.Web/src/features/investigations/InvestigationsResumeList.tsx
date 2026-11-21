import { InvestigationsHomePanel } from "./InvestigationsHomePanel";

interface InvestigationsResumeListProps {
    onOpen: (investigationId: string, projectId: string) => Promise<void>;
    busy?: boolean;
    limit?: number;
}

export function InvestigationsResumeList({
    onOpen,
    busy = false,
    limit = 6,
}: InvestigationsResumeListProps) {
    return (
        <InvestigationsHomePanel
            title="Recent investigations"
            onOpen={onOpen}
            busy={busy}
            limit={limit}
        />
    );
}
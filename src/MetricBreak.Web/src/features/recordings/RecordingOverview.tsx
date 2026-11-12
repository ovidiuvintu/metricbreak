
import {
  Box,
  Card,
  CardContent,
  Chip,
  Divider,
  Stack,
  Typography,
} from "@mui/material";

import type { SessionState } from "./sessionTypes";

interface RecordingOverviewProps {
  session: SessionState;
}

export function RecordingOverview({
  session,
}: RecordingOverviewProps) {
  const channelCount = Object.keys(
    session.currentSample.channels,
  ).length;

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h5">
          {session.recordingName}
        </Typography>

        <Chip
          label={session.executionState}
          color="info"
          size="small"
          sx={{ mt: 1 }}
        />
      </Box>

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6" gutterBottom>
            Recording Overview
          </Typography>

          <Divider sx={{ mb: 2 }} />

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                md: "repeat(3, 1fr)",
              },
              gap: 3,
            }}
          >
            <Box>
              <Typography color="text.secondary">
                Total Samples
              </Typography>
              <Typography variant="h6">
                {session.sampleCount.toLocaleString()}
              </Typography>
            </Box>

            <Box>
              <Typography color="text.secondary">
                Current Sequence
              </Typography>
              <Typography variant="h6">
                {session.currentSample.sequence.toLocaleString()}
              </Typography>
            </Box>

            <Box>
              <Typography color="text.secondary">
                Channels
              </Typography>
              <Typography variant="h6">
                {channelCount}
              </Typography>
            </Box>
          </Box>

          <Typography sx={{ mt: 3 }}>
            Pause Reason: {session.pauseReason}
          </Typography>
        </CardContent>
      </Card>
    </Stack>
  );
}

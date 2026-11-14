
import {
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";

import type { CurrentSample } from "./sessionTypes";
import type { ChannelMetadata } from "./channelMetadata";


interface ChannelListProps {
  sample?: CurrentSample | null;
  channels: ChannelMetadata[];
}

export function ChannelList({
  sample,
  channels,
}: ChannelListProps) {
  return (
    <Paper variant="outlined">
      <Typography variant="h6" sx={{ p: 2 }}>
        Current Channel Values
      </Typography>

      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Channel</TableCell>
              <TableCell align="right">Value</TableCell>
              <TableCell>Unit</TableCell>
            </TableRow>
          </TableHead>

          <TableBody>
            {channels.map((channel) => {
              const value =
                sample?.channels?.[channel.channelId];;

              return (
                <TableRow key={channel.channelId}>
                  <TableCell>{channel.name}</TableCell>

                  <TableCell align="right">
                    {value == null ? "N/A" : value.toLocaleString()}
                  </TableCell>

                  <TableCell>
                    {channel.unit ?? "—"}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
}

export type ChannelValueType = "Numeric" | "Boolean" | "Categorical";

export interface ChannelMetadata {
  channelId: string;
  name: string;
  unit: string | null;
  valueType?: ChannelValueType;
}

export interface ChannelExtent {
  minimum: number;
  maximum: number;
}

export function formatChannelExtent(extent: ChannelExtent | undefined): string | null {
  if (!extent || !Number.isFinite(extent.minimum) || !Number.isFinite(extent.maximum)) {
    return null;
  }

  const format = (value: number) =>
    value.toLocaleString(undefined, { maximumFractionDigits: 3 });
  return `${format(extent.minimum)} – ${format(extent.maximum)}`;
}

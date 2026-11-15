export type ChannelValueType = "Numeric" | "Boolean" | "Categorical";

export interface ChannelMetadata {
  channelId: string;
  name: string;
  unit: string | null;
  valueType?: ChannelValueType;
}

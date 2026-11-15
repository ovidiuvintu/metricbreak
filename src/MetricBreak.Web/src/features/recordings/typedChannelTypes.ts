export type TypedChannelValue =
  | { valueType: "Numeric"; value: number | null }
  | { valueType: "Boolean"; value: boolean | null }
  | { valueType: "Categorical"; value: string | null }
  | { valueType: "Missing"; value: null };

export type TypedChannelValues = Record<string, TypedChannelValue>;

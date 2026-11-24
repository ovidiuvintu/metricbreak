export interface NamedChannel {
  channelId: string;
  name: string;
}

/**
 * Maps a saved chart selection onto the channels of the session that is open now.
 * Channel ids are minted again each time a recording is imported, so a reopened
 * investigation is matched by channel name first.
 * Returns an empty list when the save explicitly selected nothing.
 * Returns null when a non-empty save cannot be matched.
 */
export function resolveSavedChannelIds(
  channels: readonly NamedChannel[],
  names: readonly string[] | null | undefined,
  ids: readonly string[] | null | undefined,
): string[] | null {
  const byName = new Map(channels.map((channel) => [channel.name, channel.channelId]));
  const available = new Set(channels.map((channel) => channel.channelId));

  if (names && names.length > 0) {
    const resolved = names.flatMap((name) => {
      const id = byName.get(name);
      return id ? [id] : [];
    });
    if (resolved.length > 0) return resolved;
  }

  const fromIds = (ids ?? []).filter((id) => available.has(id));
  if (fromIds.length > 0) return fromIds;
  if ((names?.length ?? 0) === 0 && (ids?.length ?? 0) === 0) return [];
  return null;
}

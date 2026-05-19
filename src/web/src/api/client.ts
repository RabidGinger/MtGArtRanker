// Light API client. All requests are same-origin in prod (via SWA linked backend)
// or proxied by Vite to http://localhost:5080 in dev.

export interface Printing {
  illustrationId: string;
  scryfallCardId: string;
  oracleId: string;
  cardName: string;
  artist: string;
  setCode: string;
  setName: string | null;
  releasedAt: string | null;
  artCropUrl: string;
  normalImageUrl: string;
  scryfallUri: string;
}

export interface RankingItem {
  illustrationId: string;
  scryfallCardId: string;
  artCropUrl: string;
  normalImageUrl: string;
  scryfallUri: string;
  artist: string;
  setCode: string;
  position: number;
}

export interface Ranking {
  oracleId: string;
  cardName: string;
  updatedAt: string;
  items: RankingItem[];
}

export interface CardSummary {
  oracleId: string;
  name: string;
  scryfallUri: string | null;
}

async function jfetch<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    headers: { "Content-Type": "application/json" },
    ...init,
  });
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`${res.status} ${res.statusText}: ${text}`);
  }
  if (res.status === 204) return undefined as unknown as T;
  return (await res.json()) as T;
}

export const api = {
  searchCards: (q: string) =>
    jfetch<string[]>(`/api/cards/search?q=${encodeURIComponent(q)}`),
  getCard: (idOrName: string) =>
    jfetch<CardSummary>(`/api/cards/${encodeURIComponent(idOrName)}`),
  getPrintings: (idOrName: string) =>
    jfetch<Printing[]>(`/api/cards/${encodeURIComponent(idOrName)}/printings`),
  listRankings: () => jfetch<Ranking[]>(`/api/rankings`),
  getRanking: (oracleId: string) =>
    jfetch<Ranking>(`/api/rankings/${oracleId}`),
  upsertRanking: (oracleId: string, body: { cardName: string; items: RankingItem[] }) =>
    jfetch<Ranking>(`/api/rankings/${oracleId}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  deleteRanking: (oracleId: string) =>
    jfetch<void>(`/api/rankings/${oracleId}`, { method: "DELETE" }),
};
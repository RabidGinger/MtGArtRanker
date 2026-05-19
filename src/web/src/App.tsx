import { useEffect, useState } from "react";
import {
  api,
  type CardSummary,
  type Printing,
  type Ranking,
  type RankingItem,
} from "./api/client";
import { CardSearch } from "./components/CardSearch";
import { RankingList } from "./components/RankingList";
import { SavedRankings } from "./components/SavedRankings";
import "./App.css";

function printingToItem(p: Printing, position: number): RankingItem {
  return {
    illustrationId: p.illustrationId,
    scryfallCardId: p.scryfallCardId,
    artCropUrl: p.artCropUrl,
    normalImageUrl: p.normalImageUrl,
    scryfallUri: p.scryfallUri,
    artist: p.artist,
    setCode: p.setCode,
    position,
  };
}

export default function App() {
  const [savedRankings, setSavedRankings] = useState<Ranking[]>([]);
  const [activeCard, setActiveCard] = useState<CardSummary | null>(null);
  const [items, setItems] = useState<RankingItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);

  async function refresh() {
    try {
      const list = await api.listRankings();
      setSavedRankings(list);
    } catch (e) {
      setError((e as Error).message);
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  async function loadCard(card: CardSummary, existingItemsOnly = false) {
    setLoading(true);
    setError(null);
    setStatus(null);
    setActiveCard(card);
    try {
      const existing = savedRankings.find((r) => r.oracleId === card.oracleId);
      if (existingItemsOnly && existing) {
        setItems(existing.items);
      } else {
        const printings = await api.getPrintings(card.oracleId);
        const existingMap = new Map(existing?.items.map((i) => [i.illustrationId, i]) ?? []);

        // Preserve existing positions for known illustrations; append new ones at the end.
        const known: RankingItem[] = [];
        const fresh: RankingItem[] = [];
        for (const p of printings) {
          const prev = existingMap.get(p.illustrationId);
          if (prev) known.push(prev);
          else fresh.push(printingToItem(p, 0));
        }
        known.sort((a, b) => a.position - b.position);
        const combined = [...known, ...fresh].map((it, i) => ({ ...it, position: i + 1 }));
        setItems(combined);
      }
    } catch (e) {
      setError((e as Error).message);
      setItems([]);
    } finally {
      setLoading(false);
    }
  }

  async function save() {
    if (!activeCard || items.length === 0) return;
    setSaving(true);
    setError(null);
    try {
      await api.upsertRanking(activeCard.oracleId, {
        cardName: activeCard.name,
        items,
      });
      setStatus("Saved.");
      refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSaving(false);
    }
  }

  async function deleteRanking(oracleId: string) {
    try {
      await api.deleteRanking(oracleId);
      if (activeCard?.oracleId === oracleId) {
        setActiveCard(null);
        setItems([]);
      }
      refresh();
    } catch (e) {
      setError((e as Error).message);
    }
  }

  function openSaved(oracleId: string, cardName: string) {
    loadCard({ oracleId, name: cardName, scryfallUri: null });
  }

  return (
    <div className="app">
      <header>
        <h1>MtG Art Ranker</h1>
        <p className="muted">
          Rank Magic: The Gathering art by your personal preference. Data from{" "}
          <a href="https://scryfall.com" target="_blank" rel="noreferrer">
            Scryfall
          </a>
          .
        </p>
      </header>

      <section>
        <h2>Pick a card</h2>
        <CardSearch onSelect={(c) => loadCard(c)} />
      </section>

      {error && <p className="error">{error}</p>}
      {status && <p className="status">{status}</p>}

      {activeCard && (
        <section>
          <h2>
            {activeCard.name}{" "}
            <span className="muted">({items.length} unique arts)</span>
          </h2>
          {loading ? (
            <p>Loading printings…</p>
          ) : (
            <>
              <div className="actions">
                <button onClick={save} disabled={saving || items.length === 0}>
                  {saving ? "Saving…" : "Save ranking"}
                </button>
                <button
                  onClick={() => setActiveCard(null)}
                  className="secondary"
                  disabled={saving}
                >
                  Close
                </button>
              </div>
              <RankingList items={items} onChange={setItems} />
            </>
          )}
        </section>
      )}

      <section>
        <h2>Your saved rankings</h2>
        <SavedRankings
          rankings={savedRankings}
          onOpen={openSaved}
          onDelete={deleteRanking}
        />
      </section>
    </div>
  );
}
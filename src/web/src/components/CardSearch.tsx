import { useEffect, useRef, useState } from "react";
import { api, type CardSummary } from "../api/client";

interface Props {
  onSelect: (card: CardSummary) => void;
}

// Extract a Scryfall card id or name from a pasted Scryfall URL.
function parseScryfallInput(input: string): string {
  const trimmed = input.trim();
  // Match e.g. https://scryfall.com/card/khm/154/kaya-the-inexorable
  const m = trimmed.match(/scryfall\.com\/card\/([^/]+)\/([^/]+)(?:\/([^/?#]+))?/i);
  if (m && m[3]) return decodeURIComponent(m[3]).replace(/-/g, " ");
  return trimmed;
}

export function CardSearch({ onSelect }: Props) {
  const [query, setQuery] = useState("");
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const debounceRef = useRef<number | undefined>(undefined);

  useEffect(() => {
    if (!query || query.length < 2) {
      setSuggestions([]);
      return;
    }
    window.clearTimeout(debounceRef.current);
    debounceRef.current = window.setTimeout(async () => {
      try {
        const res = await api.searchCards(query);
        setSuggestions(res.slice(0, 10));
      } catch {
        setSuggestions([]);
      }
    }, 200);
  }, [query]);

  async function pick(value: string) {
    setLoading(true);
    setError(null);
    try {
      const card = await api.getCard(parseScryfallInput(value));
      onSelect(card);
      setQuery("");
      setSuggestions([]);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="card-search">
      <input
        type="text"
        value={query}
        placeholder="Search by name, paste a Scryfall URL, or enter a card id…"
        onChange={(e) => setQuery(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter" && query.trim()) pick(query);
        }}
        disabled={loading}
      />
      {suggestions.length > 0 && (
        <ul className="suggestions">
          {suggestions.map((s) => (
            <li key={s}>
              <button type="button" onClick={() => pick(s)}>
                {s}
              </button>
            </li>
          ))}
        </ul>
      )}
      {error && <p className="error">{error}</p>}
    </div>
  );
}
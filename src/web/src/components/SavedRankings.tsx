import type { Ranking } from "../api/client";

interface Props {
  rankings: Ranking[];
  onOpen: (oracleId: string, cardName: string) => void;
  onDelete: (oracleId: string) => void;
}

export function SavedRankings({ rankings, onOpen, onDelete }: Props) {
  if (rankings.length === 0) {
    return <p className="muted">No saved rankings yet. Search for a card to begin.</p>;
  }
  return (
    <ul className="saved-rankings">
      {rankings.map((r) => (
        <li key={r.oracleId}>
          <button className="link" onClick={() => onOpen(r.oracleId, r.cardName)}>
            {r.cardName}
          </button>
          <span className="muted"> · {r.items.length} arts</span>
          <span className="muted"> · {new Date(r.updatedAt).toLocaleDateString()}</span>
          <button
            className="danger"
            onClick={() => {
              if (confirm(`Delete ranking for ${r.cardName}?`)) onDelete(r.oracleId);
            }}
          >
            Delete
          </button>
        </li>
      ))}
    </ul>
  );
}
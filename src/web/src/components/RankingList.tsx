import {
  DndContext,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from "@dnd-kit/core";
import {
  SortableContext,
  arrayMove,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import type { RankingItem } from "../api/client";

interface Props {
  items: RankingItem[];
  onChange: (items: RankingItem[]) => void;
}

function reindex(items: RankingItem[]): RankingItem[] {
  return items.map((it, i) => ({ ...it, position: i + 1 }));
}

function SortableRow({
  item,
  onPositionChange,
  total,
}: {
  item: RankingItem;
  onPositionChange: (newPos: number) => void;
  total: number;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } =
    useSortable({ id: item.illustrationId });

  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.6 : 1,
  };

  return (
    <li ref={setNodeRef} style={style} className="ranking-row">
      <span className="drag-handle" {...attributes} {...listeners} aria-label="Drag">
        ⋮⋮
      </span>
      <input
        type="number"
        min={1}
        max={total}
        value={item.position}
        onChange={(e) => {
          const n = parseInt(e.target.value, 10);
          if (!Number.isNaN(n) && n >= 1 && n <= total) onPositionChange(n);
        }}
        className="rank-input"
      />
      <img src={item.artCropUrl} alt="" loading="lazy" />
      <div className="row-meta">
        <div className="artist">{item.artist}</div>
        <div className="set">{item.setCode.toUpperCase()}</div>
        <a href={item.scryfallUri} target="_blank" rel="noreferrer">
          Scryfall ↗
        </a>
      </div>
    </li>
  );
}

export function RankingList({ items, onChange }: Props) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }));

  function handleDragEnd(e: DragEndEvent) {
    const { active, over } = e;
    if (!over || active.id === over.id) return;
    const oldIndex = items.findIndex((i) => i.illustrationId === active.id);
    const newIndex = items.findIndex((i) => i.illustrationId === over.id);
    onChange(reindex(arrayMove(items, oldIndex, newIndex)));
  }

  function handlePositionChange(illustrationId: string, newPos: number) {
    const oldIndex = items.findIndex((i) => i.illustrationId === illustrationId);
    if (oldIndex < 0) return;
    const newIndex = Math.max(0, Math.min(items.length - 1, newPos - 1));
    onChange(reindex(arrayMove(items, oldIndex, newIndex)));
  }

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
      <SortableContext items={items.map((i) => i.illustrationId)} strategy={verticalListSortingStrategy}>
        <ol className="ranking-list">
          {items.map((item) => (
            <SortableRow
              key={item.illustrationId}
              item={item}
              total={items.length}
              onPositionChange={(p) => handlePositionChange(item.illustrationId, p)}
            />
          ))}
        </ol>
      </SortableContext>
    </DndContext>
  );
}
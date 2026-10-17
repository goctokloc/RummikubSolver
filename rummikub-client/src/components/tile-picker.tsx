import { TileFace } from '@/components/rummikub-tile';
import { COLORS, COPIES_PER_TILE, JOKER, type TileDto, VALUES, tileKey, tileName } from '@/lib/rummikub';

type TilePickerProps = {
  /** How many copies of each tile are already in use, by tile key. */
  used: Map<string, number>;
  onPick: (tile: TileDto) => void;
};

/** Every tile in the set, one click to add. A tile is disabled once both copies are in use. */
export function TilePicker({ used, onPick }: TilePickerProps) {
  const cell = (tile: TileDto) => {
    const left = COPIES_PER_TILE - (used.get(tileKey(tile)) ?? 0);
    return (
      <button
        key={tileKey(tile)}
        type="button"
        disabled={left <= 0}
        onClick={() => onPick(tile)}
        aria-label={`Add ${tileName(tile)}`}
        title={left <= 0 ? 'Both copies are in use' : undefined}
        className="rounded-md outline-none transition-transform hover:-translate-y-0.5 focus-visible:ring-3 focus-visible:ring-ring/50 disabled:pointer-events-none disabled:opacity-25 motion-reduce:transition-none"
      >
        <TileFace tile={tile} size="sm" />
      </button>
    );
  };

  return (
    <div className="grid grid-cols-[repeat(13,minmax(0,2.25rem))] gap-1">
      {COLORS.flatMap(color => VALUES.map(value => cell({ color, value })))}
      {cell(JOKER)}
    </div>
  );
}

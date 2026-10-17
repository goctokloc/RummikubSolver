import { Smile, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { type Color, type TileDto, tileName } from '@/lib/rummikub';

const INK: Record<Color, string> = {
  Red: 'text-tile-red',
  Blue: 'text-tile-blue',
  Black: 'text-tile-black',
  Yellow: 'text-tile-yellow',
};

type TileFaceProps = {
  tile: TileDto;
  size?: 'sm' | 'md';
  /** For a joker on a solved board: the tile it stands in for. */
  standsFor?: { color: Color; value: number };
  /** Marks a tile the solver places from the rack. */
  played?: boolean;
  className?: string;
};

export function TileFace({ tile, size = 'md', standsFor, played, className }: TileFaceProps) {
  const ink = tile.joker ? (standsFor ? INK[standsFor.color] : 'text-foreground') : INK[tile.color!];
  return (
    <span
      className={cn(
        'relative inline-flex shrink-0 select-none flex-col items-center justify-center rounded-md border border-tile-edge bg-tile-face',
        'shadow-[0_1px_0_var(--tile-edge),0_3px_4px_-3px_rgb(20_20_19/0.18)]',
        size === 'md' ? 'h-[54px] w-10' : 'aspect-[3/4] w-full',
        played && 'border-clay/60 bg-[#fdf6f2]',
        ink,
        className,
      )}
    >
      {tile.joker ? (
        <Smile
          className={size === 'sm' ? 'size-[55%]' : standsFor ? '-mt-1.5 size-4' : 'size-5'}
          strokeWidth={2.25}
        />
      ) : (
        <span
          className={cn(
            'font-serif font-semibold leading-none tabular-nums',
            size === 'md' ? 'text-[22px]' : 'text-[clamp(11px,2.6vw,15px)]',
          )}
        >
          {tile.value}
        </span>
      )}
      {standsFor && (
        <span className="mt-0.5 font-serif text-[10px] font-semibold leading-none tabular-nums">{standsFor.value}</span>
      )}
      {size === 'md' && (
        <span
          aria-hidden
          className={cn(
            'absolute bottom-[5px] size-1 rounded-full',
            played ? 'bg-clay' : 'bg-transparent',
          )}
        />
      )}
    </span>
  );
}

type RemovableTileProps = { tile: TileDto; onRemove: () => void };

/** A tile that is removed when clicked; the cross appears on hover and keyboard focus. */
export function RemovableTile({ tile, onRemove }: RemovableTileProps) {
  return (
    <button
      type="button"
      onClick={onRemove}
      aria-label={`Remove ${tileName(tile)}`}
      className="group/tile relative rounded-md outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
    >
      <TileFace tile={tile} className="transition-transform group-hover/tile:-translate-y-0.5" />
      <span
        aria-hidden
        className="absolute -top-1.5 -right-1.5 flex size-4 items-center justify-center rounded-full bg-foreground text-background opacity-0 transition-opacity group-hover/tile:opacity-100 group-focus-visible/tile:opacity-100 [@media(hover:none)]:opacity-100"
      >
        <X className="size-2.5" strokeWidth={3} />
      </span>
    </button>
  );
}

import { useEffect, useRef, useState } from 'react';
import { cn } from '@/lib/utils';

const FADE = '1.5rem';
const FADE_PX = 24;

type ScrollRowProps = {
  /** On first render, scroll so the tiles marked `data-played` are in view, the first one taking priority. */
  revealPlayed?: boolean;
  className?: string;
  children: React.ReactNode;
};

/**
 * A single line that scrolls sideways when it is wider than its container. An edge fades while
 * there is more content beyond it, since touch devices hide the scrollbar.
 */
export function ScrollRow({ revealPlayed, className, children }: ScrollRowProps) {
  const ref = useRef<HTMLDivElement>(null);
  const [more, setMore] = useState({ start: false, end: false });

  useEffect(() => {
    const el = ref.current!;
    const played = el.querySelectorAll<HTMLElement>('[data-played]');
    if (revealPlayed && played.length > 0) {
      const box = el.getBoundingClientRect();
      const left = (t: HTMLElement) => t.getBoundingClientRect().left - box.left + el.scrollLeft;
      const first = left(played[0]);
      const last = left(played[played.length - 1]) + played[played.length - 1].offsetWidth;
      el.scrollLeft = Math.min(first - FADE_PX, Math.max(0, last - el.clientWidth + FADE_PX));
    }
    // Only on mount: a new result remounts the row, and later scrolling belongs to the user.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const el = ref.current!;
    const update = () => {
      const max = el.scrollWidth - el.clientWidth;
      setMore({ start: el.scrollLeft > 1, end: el.scrollLeft < max - 1 });
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(el);
    for (const child of el.children) observer.observe(child);
    el.addEventListener('scroll', update, { passive: true });
    return () => {
      observer.disconnect();
      el.removeEventListener('scroll', update);
    };
  }, []);

  const mask =
    more.start || more.end
      ? `linear-gradient(to right, ${more.start ? 'transparent' : '#000'}, #000 ${FADE}, #000 calc(100% - ${FADE}), ${more.end ? 'transparent' : '#000'})`
      : undefined;

  return (
    <div
      ref={ref}
      style={{ maskImage: mask, WebkitMaskImage: mask }}
      className={cn(
        // Vertical padding keeps tile shadows and the hover lift from being clipped by the scroll box.
        '-my-1 min-w-0 overflow-x-auto overscroll-x-contain py-1 [scrollbar-width:thin] [@media(hover:none)]:[scrollbar-width:none]',
        className,
      )}
    >
      {children}
    </div>
  );
}

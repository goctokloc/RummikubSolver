import { useMemo, useState } from 'react';
import { Check, LoaderCircle, X } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Separator } from '@/components/ui/separator';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { RemovableTile, TileFace } from '@/components/rummikub-tile';
import { ScrollRow } from '@/components/scroll-row';
import { TilePicker } from '@/components/tile-picker';
import {
  type GroupInputDto,
  type GroupType,
  type Objective,
  type SolveResponse,
  type TileDto,
  countTiles,
  markPlayedSlots,
  solve,
  subtractTiles,
  toInputGroups,
} from '@/lib/rummikub';

type Target = 'rack' | 'group';
type View = 'current' | 'solved';

export default function App() {
  const [hand, setHand] = useState<TileDto[]>([]);
  const [board, setBoard] = useState<GroupInputDto[]>([]);
  const [draft, setDraft] = useState<TileDto[]>([]);
  const [draftType, setDraftType] = useState<GroupType>('Set');
  const [target, setTarget] = useState<Target>('rack');

  const [initialMeldDone, setInitialMeldDone] = useState(true);
  const [objective, setObjective] = useState<Objective>('TileCount');
  const [keepJokers, setKeepJokers] = useState(false);

  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<SolveResponse | null>(null);
  const [errors, setErrors] = useState<string[]>([]);
  const [view, setView] = useState<View>('current');

  const used = useMemo(
    () => countTiles([...hand, ...draft, ...board.flatMap(g => g.tiles)]),
    [hand, draft, board],
  );
  const playedSlots = useMemo(
    () => (result ? markPlayedSlots(result.board, result.playedTiles) : []),
    [result],
  );

  // Any edit to the position makes an earlier answer stale.
  const edit = (apply: () => void) => {
    apply();
    setResult(null);
    setErrors([]);
    setView('current');
  };

  const pick = (tile: TileDto) =>
    edit(() => (target === 'rack' ? setHand(h => [...h, tile]) : setDraft(d => [...d, tile])));

  const commitDraft = () =>
    edit(() => {
      setBoard(b => [...b, { type: draftType, tiles: draft }]);
      setDraft([]);
    });

  const runSolve = async () => {
    setLoading(true);
    setErrors([]);
    const outcome = await solve({ hand, board, options: { initialMeldDone, objective, keepJokers } });
    if (outcome.ok) {
      setResult(outcome.result);
      setView('solved');
    } else {
      setResult(null);
      setErrors(outcome.errors);
    }
    setLoading(false);
  };

  const applyMove = () => {
    if (!result) return;
    const played = result.playedTiles;
    edit(() => {
      setBoard(toInputGroups(result.board));
      setHand(h => subtractTiles(h, played));
      if (played.length > 0) setInitialMeldDone(true);
    });
  };

  return (
    <TooltipProvider>
      <div className="mx-auto max-w-[1180px] px-4 pt-10 pb-16 sm:px-8 lg:pt-16">
        <header className="mb-10 max-w-[38rem]">
          <h1 className="font-heading text-[2.5rem] leading-[1.1] font-medium tracking-[-0.015em] sm:text-5xl">
            Rummikub solver
          </h1>
          <p className="mt-4 text-[1.0625rem] leading-relaxed text-muted-foreground">
            Lay out the table and your rack, then find the move that gets the most tiles out of your hand,
            rearranging the table where it helps.
          </p>
        </header>

        <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_380px] lg:gap-8">
          <main className="flex min-w-0 flex-col gap-6">
            <Tabs value={view} onValueChange={v => setView(v as View)} className="gap-0">
              <Card className="gap-0 py-0 ring-border">
                <CardHeader className="flex flex-wrap items-center justify-between gap-3 border-b py-4">
                  <CardTitle className="font-heading text-xl">Table</CardTitle>
                  <TabsList variant="line">
                    <TabsTrigger value="current">Now</TabsTrigger>
                    <TabsTrigger value="solved" disabled={!result}>
                      After best move
                    </TabsTrigger>
                  </TabsList>
                </CardHeader>

                <TabsContent value="current" className="min-h-56 p-5 sm:p-6">
                  {board.length === 0 ? (
                    <EmptyNote>
                      The table is empty. Build a group in the panel on the right, or leave it empty to find your
                      opening meld.
                    </EmptyNote>
                  ) : (
                    <div className="flex flex-wrap gap-x-6 gap-y-5">
                      {board.map((g, i) => (
                        <div key={i} className="group/row flex max-w-full items-center gap-1">
                          <GroupTiles>
                            {g.tiles.map((t, j) => (
                              <TileFace key={j} tile={t} />
                            ))}
                          </GroupTiles>
                          <Tooltip>
                            <TooltipTrigger asChild>
                              <Button
                                variant="ghost"
                                size="icon-xs"
                                aria-label={`Remove group ${i + 1}`}
                                className="ml-1 text-muted-foreground opacity-0 group-hover/row:opacity-100 focus-visible:opacity-100 [@media(hover:none)]:opacity-100"
                                onClick={() => edit(() => setBoard(b => b.filter((_, j) => j !== i)))}
                              >
                                <X />
                              </Button>
                            </TooltipTrigger>
                            <TooltipContent>Remove group</TooltipContent>
                          </Tooltip>
                        </div>
                      ))}
                    </div>
                  )}
                </TabsContent>

                <TabsContent value="solved" className="min-h-56 p-5 sm:p-6">
                  {result && (
                    <div className="flex flex-col gap-6">
                      <div className="flex flex-wrap items-center justify-between gap-4">
                        <div className="flex flex-wrap items-center gap-3">
                          <p className="font-heading text-lg">
                            {result.tilesPlayed === 0
                              ? 'No tiles can be played this turn.'
                              : `Play ${result.tilesPlayed} ${result.tilesPlayed === 1 ? 'tile' : 'tiles'} from your rack.`}
                          </p>
                          {result.optimal ? (
                            <Badge variant="secondary">
                              <Check /> Best possible
                            </Badge>
                          ) : (
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <Badge variant="outline">Best found</Badge>
                              </TooltipTrigger>
                              <TooltipContent className="max-w-60">
                                The search hit its limit before it could prove nothing better exists.
                              </TooltipContent>
                            </Tooltip>
                          )}
                        </div>
                        {result.tilesPlayed > 0 && (
                          <Button variant="outline" onClick={applyMove}>
                            Apply move
                          </Button>
                        )}
                      </div>

                      {result.board.length > 0 && (
                        <div className="flex flex-wrap gap-x-6 gap-y-5">
                          {result.board.map((g, i) => (
                            <div key={i} className="max-w-full">
                              <GroupTiles revealPlayed>
                                {g.slots.map((s, j) => (
                                  <TileFace
                                    key={j}
                                    tile={s.joker ? { joker: true } : s}
                                    standsFor={s.joker ? s : undefined}
                                    played={playedSlots[i][j]}
                                  />
                                ))}
                              </GroupTiles>
                            </div>
                          ))}
                        </div>
                      )}

                      {result.tilesPlayed > 0 && (
                        <p className="flex items-center gap-2 text-sm text-muted-foreground">
                          <span className="size-1.5 rounded-full bg-clay" aria-hidden />
                          Marked tiles come from your rack.
                        </p>
                      )}
                    </div>
                  )}
                </TabsContent>
              </Card>
            </Tabs>

            <section aria-labelledby="rack-heading" className="rounded-xl bg-rack p-5 sm:p-6">
              <div className="mb-4 flex items-baseline justify-between gap-4">
                <h2 id="rack-heading" className="font-heading text-xl font-medium">
                  Your rack
                  <span className="ml-2 font-sans text-sm font-normal text-muted-foreground tabular-nums">
                    {hand.length} {hand.length === 1 ? 'tile' : 'tiles'}
                  </span>
                </h2>
                {hand.length > 0 && (
                  <Button variant="ghost" size="sm" onClick={() => edit(() => setHand([]))}>
                    Clear rack
                  </Button>
                )}
              </div>
              {hand.length === 0 ? (
                <EmptyNote>Pick tiles in the panel to add them to your rack.</EmptyNote>
              ) : (
                <div className="flex flex-wrap gap-1.5">
                  {hand.map((t, i) => (
                    <RemovableTile key={i} tile={t} onRemove={() => edit(() => setHand(h => h.filter((_, j) => j !== i)))} />
                  ))}
                </div>
              )}
            </section>
          </main>

          <aside className="lg:sticky lg:top-6">
            <Card className="ring-border">
              <CardHeader>
                <CardTitle className="font-heading text-xl">Add tiles</CardTitle>
                <CardDescription>Click a tile to add it. Each tile comes in two copies.</CardDescription>
              </CardHeader>

              <CardContent className="flex flex-col gap-5">
                <ToggleGroup
                  type="single"
                  variant="outline"
                  spacing={0}
                  value={target}
                  onValueChange={v => v && setTarget(v as Target)}
                  className="w-full"
                  aria-label="Where picked tiles go"
                >
                  <ToggleGroupItem value="rack" className="flex-1">
                    To my rack
                  </ToggleGroupItem>
                  <ToggleGroupItem value="group" className="flex-1">
                    To a new group
                  </ToggleGroupItem>
                </ToggleGroup>

                <TilePicker used={used} onPick={pick} />

                {target === 'group' && (
                  <div className="flex flex-col gap-3 rounded-lg border border-dashed border-input p-3">
                    <div className="flex items-center justify-between gap-3">
                      <ToggleGroup
                        type="single"
                        variant="outline"
                        size="sm"
                        spacing={0}
                        value={draftType}
                        onValueChange={v => v && setDraftType(v as GroupType)}
                        aria-label="Group type"
                      >
                        <ToggleGroupItem value="Set" className="px-3">Set</ToggleGroupItem>
                        <ToggleGroupItem value="Run" className="px-3">Run</ToggleGroupItem>
                      </ToggleGroup>
                      <span className="text-xs text-muted-foreground">
                        {draftType === 'Set' ? 'Same number, different colors' : 'Same color, consecutive numbers'}
                      </span>
                    </div>
                    {draft.length === 0 ? (
                      <p className="py-3 text-center text-sm text-muted-foreground">Pick tiles for this group.</p>
                    ) : (
                      <div className="flex flex-wrap gap-1.5">
                        {draft.map((t, i) => (
                          <RemovableTile key={i} tile={t} onRemove={() => setDraft(d => d.filter((_, j) => j !== i))} />
                        ))}
                      </div>
                    )}
                    <div className="flex gap-2">
                      <Button className="flex-1" variant="secondary" disabled={draft.length === 0} onClick={commitDraft}>
                        Add group to table
                      </Button>
                      {draft.length > 0 && (
                        <Button variant="ghost" onClick={() => setDraft([])}>
                          Clear
                        </Button>
                      )}
                    </div>
                  </div>
                )}

                <Separator />

                <div className="flex flex-col gap-4">
                  <OptionRow
                    id="initial-meld"
                    label="Opening meld already made"
                    hint="Turn off to require a first play of at least 30 points from your rack alone."
                    checked={initialMeldDone}
                    onChange={v => edit(() => setInitialMeldDone(v))}
                  />
                  <OptionRow
                    id="keep-jokers"
                    label="Hold on to jokers"
                    hint="Keep jokers in your rack when playing them wouldn't score more."
                    checked={keepJokers}
                    onChange={v => edit(() => setKeepJokers(v))}
                  />
                  <div className="flex items-center justify-between gap-4">
                    <Label id="objective-label" className="font-normal">Aim for</Label>
                    <ToggleGroup
                      type="single"
                      variant="outline"
                      size="sm"
                      spacing={0}
                      value={objective}
                      onValueChange={v => v && edit(() => setObjective(v as Objective))}
                      aria-labelledby="objective-label"
                    >
                      <ToggleGroupItem value="TileCount" className="px-3">Most tiles</ToggleGroupItem>
                      <ToggleGroupItem value="Points" className="px-3">Most points</ToggleGroupItem>
                    </ToggleGroup>
                  </div>
                </div>
              </CardContent>

              <CardFooter className="flex-col items-stretch gap-3 bg-transparent">
                <Button size="lg" className="h-11 text-[0.9375rem]" onClick={runSolve} disabled={loading || hand.length === 0}>
                  {loading && <LoaderCircle className="animate-spin" />}
                  {loading ? 'Finding best move…' : 'Find best move'}
                </Button>
                {hand.length === 0 && !loading && (
                  <p className="text-center text-xs text-muted-foreground">Add tiles to your rack first.</p>
                )}
                {errors.length > 0 && (
                  <Alert variant="destructive">
                    <AlertTitle>Couldn't solve this position</AlertTitle>
                    <AlertDescription>
                      {errors.length === 1 ? (
                        <p>{errors[0]}</p>
                      ) : (
                        <ul className="list-disc pl-4">
                          {errors.map((e, i) => (
                            <li key={i}>{e}</li>
                          ))}
                        </ul>
                      )}
                    </AlertDescription>
                  </Alert>
                )}
              </CardFooter>
            </Card>
          </aside>
        </div>
      </div>
    </TooltipProvider>
  );
}

function GroupTiles({ revealPlayed, children }: { revealPlayed?: boolean; children: React.ReactNode }) {
  return (
    <ScrollRow revealPlayed={revealPlayed}>
      <div className="flex w-max gap-1">{children}</div>
    </ScrollRow>
  );
}

function EmptyNote({ children }: { children: React.ReactNode }) {
  return <p className="max-w-[28rem] text-sm leading-relaxed text-muted-foreground">{children}</p>;
}

type OptionRowProps = {
  id: string;
  label: string;
  hint: string;
  checked: boolean;
  onChange: (value: boolean) => void;
};

function OptionRow({ id, label, hint, checked, onChange }: OptionRowProps) {
  return (
    <div className="flex items-start justify-between gap-4">
      <div className="flex flex-col gap-1">
        <Label htmlFor={id} className="font-normal">{label}</Label>
        <p className="text-xs leading-snug text-muted-foreground">{hint}</p>
      </div>
      <Switch id={id} checked={checked} onCheckedChange={onChange} className="mt-0.5 data-unchecked:bg-[#c4bfb1]" />
    </div>
  );
}

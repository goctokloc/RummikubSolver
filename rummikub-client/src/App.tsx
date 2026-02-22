import { useState } from 'react';
import { Play, Plus, RefreshCw, Layers } from 'lucide-react';
import './index.css';

type TileDto = { color?: string; value?: number; isJoker: boolean };
type GroupDto = { type: string; tiles: TileDto[] };
type SolveRequestDto = { hand: TileDto[]; board: GroupDto[] };
type SolveResponseDto = { playedTiles: TileDto[]; newBoard: GroupDto[]; tilesPlayedCount: number };

export default function App() {
  const [hand, setHand] = useState<TileDto[]>([]);
  const [board, setBoard] = useState<GroupDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<SolveResponseDto | null>(null);

  const colors = ['Red', 'Blue', 'Black', 'Yellow'];
  const values = Array.from({ length: 13 }, (_, i) => i + 1);

  const [tColor, setTColor] = useState('Red');
  const [tValue, setTValue] = useState(1);
  const [tIsJoker, setTIsJoker] = useState(false);

  // Group Builder Form
  const [gType, setGType] = useState('Set');
  const [gTiles, setGTiles] = useState<TileDto[]>([]);

  const addTileToHand = () => {
    setHand([...hand, { color: tIsJoker ? undefined : tColor, value: tIsJoker ? undefined : tValue, isJoker: tIsJoker }]);
  };

  const addTileToGroupBuilder = () => {
    setGTiles([...gTiles, { color: tIsJoker ? undefined : tColor, value: tIsJoker ? undefined : tValue, isJoker: tIsJoker }]);
  };

  const commitGroupToBoard = () => {
    if (gTiles.length === 0) return;
    setBoard([...board, { type: gType, tiles: [...gTiles] }]);
    setGTiles([]);
  };

  const removeHandTile = (idx: number) => {
    setHand(hand.filter((_, i) => i !== idx));
  };

  const removeBoardGroup = (idx: number) => {
    setBoard(board.filter((_, i) => i !== idx));
  };

  const solve = async () => {
    setLoading(true);
    setResult(null);
    try {
      const payload: SolveRequestDto = { hand, board };
      const response = await fetch('http://localhost:5155/api/solver/solve', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      const data = await response.json();
      setResult(data);
    } catch (e) {
      console.error(e);
      alert('Failed to connect to Rummikub Solver API');
    }
    setLoading(false);
  };

  const renderTile = (t: TileDto, idx: number, removeFn?: (i: number) => void) => (
    <div key={idx} className="tile-wrapper">
      <div className={`tile ${t.isJoker ? 'joker' : t.color?.toLowerCase()}`}>
        {t.isJoker ? '☻' : t.value}
      </div>
      {removeFn && (
        <button onClick={() => removeFn(idx)} className="remove-btn">✕</button>
      )}
    </div>
  );

  return (
    <div className="app-container">
      <header>
        <h1>Rummikub AI Solver</h1>
        <p className="subtitle">Extremely optimized Exact Cover native algorithms</p>
      </header>

      <div className="workspace">
        <div className="board-area">
          {/* Hand Area */}
          <div className="glass-panel hand-area">
            <h3>Your Hand ({hand.length} tiles)</h3>
            <div className="hand-tiles">
              {hand.length === 0 ? <span style={{ color: '#64748b' }}>Hand is empty</span> : hand.map((t, i) => renderTile(t, i, removeHandTile))}
            </div>
          </div>

          {/* Board Area */}
          <div className="glass-panel" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
            <div style={{ padding: '1.5rem', borderBottom: '1px solid var(--border-subtle)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h3>Board configuration</h3>
              <span className="badge">{board.length} Groups</span>
            </div>
            <div className="board-grid">
              {board.length === 0 ? (
                <span style={{ color: '#64748b' }}>Board is empty</span>
              ) : (
                board.map((g, i) => (
                  <div key={i} style={{ position: 'relative' }}>
                    <div className="group-container">
                      {g.tiles.map((t, idx) => renderTile(t, idx))}
                    </div>
                    <button onClick={() => removeBoardGroup(i)} className="remove-btn" style={{ opacity: 1 }}>✕</button>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Result Area */}
          {result && (
            <div className="glass-panel" style={{ padding: '1.5rem', border: '1px solid #10b981' }}>
              <h2 style={{ color: '#10b981', marginBottom: '1rem' }}>Solve Complete!</h2>
              <p style={{ marginBottom: '1rem', color: '#94a3b8' }}>
                Tiles played from hand: <strong style={{ color: 'white' }}>{result.tilesPlayedCount}</strong>
              </p>

              <h4 style={{ marginBottom: '0.5rem' }}>Optimized Board State</h4>
              <div className="board-grid" style={{ minHeight: 'auto', padding: '1rem 0' }}>
                {result.newBoard.map((g, i) => (
                  <div key={i} className="group-container">
                    {g.tiles.map((t, idx) => renderTile(t, idx))}
                    <div style={{ display: 'flex', alignItems: 'center', marginLeft: '8px', fontSize: '0.8rem', color: '#64748b' }}>
                      {g.type}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Builder Panel */}
        <div className="glass-panel controls-panel">
          <div className="controls-header">Tile Builder</div>

          <div className="form-group" style={{ flexDirection: 'row', alignItems: 'center', gap: '0.5rem' }}>
            <input
              type="checkbox"
              checked={tIsJoker}
              onChange={e => setTIsJoker(e.target.checked)}
              id="joker-switch"
            />
            <label htmlFor="joker-switch" style={{ color: '#a855f7', fontWeight: 'bold' }}>Is Joker</label>
          </div>

          {!tIsJoker && (
            <>
              <div className="form-group">
                <label>Color</label>
                <select value={tColor} onChange={e => setTColor(e.target.value)}>
                  {colors.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              </div>

              <div className="form-group">
                <label>Value</label>
                <select value={tValue} onChange={e => setTValue(parseInt(e.target.value))}>
                  {values.map(v => <option key={v} value={v}>{v}</option>)}
                </select>
              </div>
            </>
          )}

          <div className="button-group" style={{ marginTop: '0.5rem' }}>
            <button className="btn btn-primary" onClick={addTileToHand}>
              Hand <Plus size={18} />
            </button>
            <button className="btn btn-primary" style={{ background: '#475569' }} onClick={addTileToGroupBuilder}>
              Group <Plus size={18} />
            </button>
          </div>

          {/* Pending Group Builder */}
          {gTiles.length > 0 && (
            <div style={{ marginTop: '1.5rem', background: 'rgba(0,0,0,0.2)', padding: '1rem', borderRadius: '12px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
                <label>Pending Group ({gType})</label>
                <span style={{ cursor: 'pointer', color: '#ef4444' }} onClick={() => setGTiles([])}>Clear</span>
              </div>
              <select style={{ width: '100%', marginBottom: '1rem' }} value={gType} onChange={e => setGType(e.target.value)}>
                <option value="Set">Set</option>
                <option value="Run">Run</option>
              </select>
              <div className="hand-tiles" style={{ minHeight: 'auto', marginBottom: '1rem' }}>
                {gTiles.map((t, i) => renderTile(t, i))}
              </div>
              <button className="btn btn-primary" style={{ width: '100%', background: '#6366f1' }} onClick={commitGroupToBoard}>
                <Layers size={18} /> Add to Board
              </button>
            </div>
          )}

          <button
            className="btn btn-success"
            onClick={solve}
            disabled={loading}
          >
            {loading ? <RefreshCw className="spinner" size={20} /> : <Play size={20} />}
            {loading ? 'Solving...' : 'Optimize Turn'}
          </button>
        </div>
      </div>
    </div>
  );
}

import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { isAxiosError } from 'axios';
import { CalendarDays, Package, Search, Truck, X } from 'lucide-react';
import { Link } from 'react-router-dom';
import { agentApi, agentError } from '../../api/agentApi';
import type { FarmerSummary, Need } from '../../api/agentApi';

export const farmerPath = (id: number, tab = '') => '/agent/farmers/' + id + (tab ? '/' + tab : '');
export const dateLabel = (value: string) => new Date(value.slice(0, 10) + 'T12:00:00').toLocaleDateString('en-GB', {
  day: 'numeric', month: 'short', year: 'numeric',
});

export const needs = {
  HarvestDue: { title: 'Harvest due', tone: 'amber', tab: 'planting-batches', icon: CalendarDays },
  LowSeed: { title: 'Low seed', tone: 'red', tab: 'seed-stock', icon: Package },
  OrderToConfirm: { title: 'Order to confirm', tone: 'blue', tab: 'orders', icon: Truck },
};

export function useAgentQuery<T>(load: (signal: AbortSignal) => Promise<T>) {
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<{ load: typeof load; attempt: number; data?: T; error?: unknown }>();
  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal).then(
      data => { if (!controller.signal.aborted) setResult({ load, attempt, data }); },
      error => { if (!controller.signal.aborted) setResult({ load, attempt, error }); },
    );
    return () => controller.abort();
  }, [load, attempt]);
  const current = result?.load === load && result.attempt === attempt ? result : undefined;
  return {
    data: current?.data,
    error: current?.error,
    status: isAxiosError(current?.error) ? current.error.response?.status : undefined,
    retry: () => setAttempt(value => value + 1),
  };
}

export function QueryMessage({ error, retry }: { error: unknown; retry: () => void }) {
  return error
    ? <div className="aw-card aw-empty" role="alert">{agentError(error)} <button className="aw-button" onClick={retry}>Retry</button></div>
    : <p className="aw-card aw-empty" role="status">Loading workspace…</p>;
}

export function Avatar({ name, tone = 'green' }: { name: string; tone?: string }) {
  return <span aria-hidden="true" className={'aw-avatar aw-' + tone}>{name.trim().split(/\s+/).slice(0, 2).map(part => part[0]).join('')}</span>;
}

export function Badge({ children, tone = 'green' }: { children: ReactNode; tone?: string }) {
  return <span className={'aw-badge aw-' + tone}>{children}</span>;
}

export function NeedBadge({ need }: { need: Need }) {
  const style = needs[need.type];
  return <Badge tone={style?.tone}>{style?.title ?? need.message}</Badge>;
}

export function SearchField({ value, onChange, label }: { value: string; onChange: (value: string) => void; label: string }) {
  return <label className="aw-search"><Search size={16} /><input type="search" value={value} onChange={event => onChange(event.target.value)} placeholder={label} aria-label={label} /></label>;
}

export function Modal({ title, children, onClose }: { title: string; children: ReactNode; onClose: () => void }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = ref.current;
    dialog?.showModal();
    return () => dialog?.close();
  }, []);
  return <dialog ref={ref} className="aw-modal" aria-label={title}
    onCancel={event => { event.preventDefault(); onClose(); }}>
    <header><h2>{title}</h2><button type="button" aria-label="Close dialog" className="aw-icon-button" onClick={onClose}><X size={18} /></button></header>
    {children}
  </dialog>;
}

export function FarmerTable({ farmers }: { farmers: FarmerSummary[] }) {
  return <section className="aw-card">
    <h2>My farmers</h2><p className="aw-muted">You can act for farmers assigned to you.</p>
    {farmers.length === 0 ? <p className="aw-empty">No farmers to display.</p> :
      <div className="aw-table-wrap"><table className="aw-table">
        <thead><tr>{['Farmer', 'Farm · area', 'Phone', 'Active batches', 'Seed stock', 'Orders', 'Action'].map(label => <th key={label} scope="col">{label}</th>)}</tr></thead>
        <tbody>{farmers.map(farmer => <tr key={farmer.farmerId}>
          <td><div className="aw-person"><Avatar name={farmer.name} /><strong>{farmer.name}</strong></div></td>
          <td>{farmer.farm} · {farmer.district}</td>
          <td>{farmer.phone || 'No phone'}</td>
          <td>{farmer.activeBatches}</td>
          <td>{farmer.lowStockCount > 0 ? <Badge tone="amber">{farmer.lowStockCount} low</Badge> : <span className="aw-muted">—</span>}</td>
          <td>{farmer.ordersToConfirm ? farmer.ordersToConfirm + ' to confirm' : '—'}</td>
          <td><Link className="aw-text-link" to={farmerPath(farmer.farmerId)}>Act as farmer</Link></td>
        </tr>)}</tbody>
      </table></div>}
  </section>;
}

export function matchesFarmer(farmer: FarmerSummary, search: string) {
  return [farmer.name, farmer.farm, farmer.district, farmer.phone].join(' ').toLowerCase().includes(search.trim().toLowerCase());
}

export function FarmerPicker({ current, onClose, onSelect }: { current?: number; onClose: () => void; onSelect: (id: number) => void }) {
  const { data, error, retry } = useAgentQuery(agentApi.farmers);
  const [search, setSearch] = useState('');
  const [selection, setSelection] = useState(current);
  const filtered = data?.filter(farmer => matchesFarmer(farmer, search));
  const canSelect = selection !== current && filtered?.some(farmer => farmer.farmerId === selection);
  return <Modal title={current ? 'Switch farmer' : 'Open a farmer’s workspace'} onClose={onClose}>
    <p className="aw-muted">Choose a farmer assigned to you.</p>
    <SearchField value={search} onChange={setSearch} label="Search your farmers…" />
    {!filtered ? <QueryMessage error={error} retry={retry} /> :
      <div className="aw-picker" role="radiogroup" aria-label="Assigned farmers">
        {filtered.length === 0 && <p className="aw-empty">No matching farmers.</p>}
        {filtered.map(farmer => <label key={farmer.farmerId} className="aw-pick">
          <input type="radio" name="assigned-farmer" checked={selection === farmer.farmerId} onChange={() => setSelection(farmer.farmerId)} />
          <Avatar name={farmer.name} /><span><strong>{farmer.name}</strong><small>{farmer.farm}</small></span>
          {current === farmer.farmerId && <Badge>Current</Badge>}
        </label>)}
      </div>}
    <footer><button className="aw-button" onClick={onClose}>Cancel</button>
      <button className="aw-button aw-primary" disabled={!canSelect} onClick={() => { if (selection !== undefined) onSelect(selection); }}>{current ? 'Switch' : 'Open workspace'}</button></footer>
  </Modal>;
}


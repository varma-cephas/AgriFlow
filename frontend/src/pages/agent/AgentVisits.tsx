import { useCallback, useState } from 'react';
import type { FormEvent } from 'react';
import { CalendarDays } from 'lucide-react';
import { agentApi, agentError } from '../../api/agentApi';
import type { FarmVisit, VisitFilter } from '../../api/agentApi';
import { Badge, dateLabel, Modal, QueryMessage, useAgentQuery } from './agentUi';

type VisitAction = { visit: FarmVisit; kind: 'complete' | 'cannot-verify' };

export function AgentVisits() {
  const [filter, setFilter] = useState<VisitFilter>('Requested');
  const [action, setAction] = useState<VisitAction>();
  const [notice, setNotice] = useState('');
  const load = useCallback((signal: AbortSignal) => agentApi.visits(filter, signal), [filter]);
  const { data, error, retry } = useAgentQuery(load);

  return <main className="aw-content">
    <header className="aw-page-heading aw-person"><span className="aw-symbol aw-green"><CalendarDays size={23} /></span>
      <div><h1>Farm visits</h1><p className="aw-muted">Review assigned verification visits and record the outcome.</p></div>
    </header>
    {notice && <p className="aw-notice aw-green" role="status">{notice}</p>}
    <div className="aw-tabs" aria-label="Filter visits">
      {(['Requested', 'Done', 'All'] as const).map(status =>
        <button key={status} aria-pressed={filter === status} onClick={() => setFilter(status)}>{status === 'Requested' ? 'Requested' : status === 'Done' ? 'Completed' : 'All visits'}</button>)}
    </div>
    {!data ? <QueryMessage error={error} retry={retry} /> :
      <section className="aw-visit-list" aria-label="Visit requests">
        {!data.length && <p className="aw-card aw-empty">No visits match this filter.</p>}
        {data.map(visit => <article className="aw-card" key={visit.id}>
          <div className="aw-heading-row"><h2>{visit.farmer.name}</h2><Badge tone={visit.status === 'Requested' ? 'amber' : visit.status === 'Done' ? 'green' : 'red'}>{visit.status === 'Done' ? 'Completed' : visit.status}</Badge></div>
          <p className="aw-muted">{visit.farmer.farm} · {Object.values(visit.location).filter(Boolean).join(' · ')}</p>
          <dl className="aw-details">
            <div><dt>Due by</dt><dd>{dateLabel(visit.visitBy)}</dd></div>
            <div><dt>Requested by</dt><dd>{visit.requestedBy.name}</dd></div>
            <div><dt>Land UPI</dt><dd>{visit.landUpi || '—'}</dd></div>
            <div><dt>Phone</dt><dd>{visit.farmer.phone || 'No phone'}</dd></div>
            {visit.completedAt && <div><dt>Completed</dt><dd>{dateLabel(visit.completedAt)}</dd></div>}
          </dl>
          {visit.note && <p className="aw-visit-note">{visit.note}</p>}
          {visit.status === 'Requested' && <div className="aw-actions">
            <button className="aw-button aw-primary" onClick={() => setAction({ visit, kind: 'complete' })}>Complete visit</button>
            <button className="aw-button" onClick={() => setAction({ visit, kind: 'cannot-verify' })}>Cannot verify</button>
          </div>}
        </article>)}
      </section>}
    {action && <VisitDialog action={action} onClose={() => setAction(undefined)} onSaved={message => {
      setAction(undefined);
      setNotice(message);
      retry();
    }} />}
  </main>;
}

function VisitDialog({ action, onClose, onSaved }: { action: VisitAction; onClose: () => void; onSaved: (message: string) => void }) {
  const [date, setDate] = useState('');
  const [description, setDescription] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const complete = action.kind === 'complete';
  const close = () => { if (!saving) onClose(); };

  async function save(event: FormEvent) {
    event.preventDefault();
    if (saving || !description.trim()) return;
    setSaving(true);
    setError('');
    try {
      if (complete) await agentApi.completeVisit(action.visit.id, date, description.trim());
      else await agentApi.cannotVerify(action.visit.id, description.trim());
      onSaved(complete ? 'Visit completed and farmer verified.' : 'Visit marked as unable to verify.');
    } catch (failure) {
      setError(agentError(failure));
      setSaving(false);
    }
  }

  return <Modal title={complete ? 'Complete farm visit' : 'Unable to verify farm'} onClose={close}>
    <p className="aw-muted">{action.visit.farmer.name} · {action.visit.farmer.farm}</p>
    <form className="aw-visit-form" onSubmit={save}>
      {complete && <label className="aw-field">Visit date<input type="date" value={date} onChange={event => setDate(event.target.value)} required disabled={saving} /></label>}
      <label className="aw-field">{complete ? 'Findings' : 'Reason'}
        <textarea required maxLength={complete ? 2000 : 500} value={description} onChange={event => setDescription(event.target.value)} disabled={saving} rows={4} />
      </label>
      {error && <p className="aw-notice aw-red" role="alert">{error}</p>}
      <footer><button type="button" className="aw-button" disabled={saving} onClick={close}>Cancel</button>
        <button className="aw-button aw-primary" disabled={saving || !description.trim()}>{saving ? 'Saving…' : complete ? 'Complete visit' : 'Record outcome'}</button></footer>
    </form>
  </Modal>;
}

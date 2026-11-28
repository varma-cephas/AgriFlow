import { useCallback, useState } from 'react';
import { CalendarDays, CircleAlert, Home, LockKeyhole, LogOut, Package, Sprout, Store, Truck, UserPlus, Users } from 'lucide-react';
import { Link, Navigate, NavLink, Outlet, Route, Routes, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { agentApi } from '../../api/agentApi';
import type { FarmerSummary } from '../../api/agentApi';
import { useAuth } from '../../context/AuthContext';
import { Avatar, Badge, dateLabel, FarmerPicker, farmerPath, FarmerTable, matchesFarmer, Modal, NeedBadge, needs, QueryMessage, SearchField, useAgentQuery } from './agentUi';
import { AgentVisits } from './AgentVisits';
import { AgentRegistration } from './AgentRegistration';
import './agent.css';

const navigation = [
  { title: 'Overview', path: 'dashboard', icon: Home },
  { title: 'My farmers', path: 'farmers', icon: Users },
  { title: 'Register farmer', path: 'register', icon: UserPlus },
  { title: 'Marketplace', path: 'marketplace', icon: Store },
  { title: 'Farm visits', path: 'visits', icon: CalendarDays },
];

const tabs = [
  { path: '', title: 'Overview', icon: Users },
  { path: 'seed-stock', title: 'Seed stock', icon: Package, summary: 'Individual seed lots and stock changes require a seed stock API.' },
  { path: 'planting-batches', title: 'Planting batches', icon: Sprout, summary: 'Batch details and harvest recording require a planting batch API.' },
  { path: 'listings', title: 'Listings', icon: Store, summary: 'Farmer listings require a listings API.' },
  { path: 'orders', title: 'Sales & orders', icon: Truck, summary: 'Individual orders and fulfilment actions require an orders API.' },
];

const metrics = [
  { key: 'assignedFarmers', label: 'Assigned farmers', note: 'In your workspace', icon: Users, tone: 'green', path: 'farmers' },
  { key: 'activeBatches', label: 'Active batches', note: 'Across your farmers', icon: Sprout, tone: 'blue', path: 'farmers' },
  { key: 'harvestsDueIn14Days', label: 'Harvests due', note: 'Within 14 days', icon: CalendarDays, tone: 'amber', path: 'farmers' },
  { key: 'ordersToHandle', label: 'Orders to handle', note: 'Placed or reserved', icon: Truck, tone: 'purple', path: 'farmers' },
] as const;

function WorkspaceShell() {
  const { user, logout } = useAuth();
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [, , section, farmerId, tab] = pathname.split('/');
  const current = navigation.find(item => item.path === section);
  return <div className="aw">
    <aside className="aw-sidebar">
      <Link className="aw-brand" to="/agent/dashboard"><Sprout size={26} /> AgriFlow</Link>
      <div className="aw-sidebar-label">Agent workspace</div>
      <nav aria-label="Agent navigation">{navigation.map(item =>
        <NavLink key={item.path} to={'/agent/' + item.path}><item.icon size={18} /> {item.title}</NavLink>)}</nav>
      <div className="aw-profile">
        <Avatar name={user?.fullName ?? 'Field agent'} />
        <span><strong>{user?.fullName}</strong><small>Field agent</small></span>
        <button type="button" className="aw-icon-button" aria-label="Sign out" onClick={() => { logout(); navigate('/login', { replace: true }); }}><LogOut size={17} /></button>
      </div>
    </aside>
    <div className="aw-main">
      <header className="aw-topbar">
        <div className="aw-breadcrumb"><Link to="/agent/dashboard">Agent workspace</Link><span>/</span>{current?.title ?? 'Workspace'}{farmerId && <><span>/</span>{tabs.find(item => item.path === (tab ?? ''))?.title}</>}</div>
        <form className="aw-header-search" role="search" onSubmit={event => {
          event.preventDefault();
          navigate({ pathname: '/agent/farmers', search: new URLSearchParams({ search: search.trim() }).toString() });
        }}>
          <SearchField label="Search assigned farmers…" value={search} onChange={setSearch} />
        </form>
        <Link className="aw-icon-button" to="/agent/visits" aria-label="Farm visits"><CalendarDays size={18} /></Link>
        <time>{new Intl.DateTimeFormat('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' }).format(new Date())}</time>
      </header>
      <Outlet />
    </div>
  </div>;
}

function Dashboard() {
  const dashboard = useAgentQuery(agentApi.dashboard);
  const loadVisits = useCallback((signal: AbortSignal) => agentApi.visits('Requested', signal), []);
  const visits = useAgentQuery(loadVisits);
  const [picker, setPicker] = useState(false);
  const navigate = useNavigate();
  const data = dashboard.data;
  const nextVisit = visits.data?.[0];

  return <main className="aw-content">
    {!data ? <QueryMessage error={dashboard.error} retry={dashboard.retry} /> : <>
      <section className="aw-hero">
        <div>
          <p className="aw-eyebrow">Field agent{data.agent.district && ' · ' + data.agent.district}</p>
          <h1>Welcome, {data.agent.name.split(' ')[0]}</h1>
          <p>You support {data.kpis.assignedFarmers} {data.kpis.assignedFarmers === 1 ? 'farmer' : 'farmers'}.</p>
          <div className="aw-actions">
            <button className="aw-button" onClick={() => setPicker(true)}><Users size={16} /> Open a farmer’s workspace</button>
            <Link className="aw-button aw-on-hero" to="/agent/register"><UserPlus size={16} /> Register a farmer</Link>
          </div>
        </div>
        <Link className="aw-next" to="/agent/visits">
          <span className="aw-symbol aw-green"><CalendarDays size={20} /></span>
          <span><strong>{nextVisit ? 'Next visit · ' + nextVisit.farmer.name : 'Farm visits'}</strong>
          <small>{nextVisit ? 'Due ' + dateLabel(nextVisit.visitBy) : visits.error ? 'Open the visit queue to retry' : visits.data ? 'No pending visits' : 'Checking your visit queue…'}</small></span>
        </Link>
      </section>
      <div className="aw-metrics">
        {metrics.map(metric => <Link className="aw-card aw-metric" key={metric.key} to={'/agent/' + metric.path}>
          <span className={'aw-symbol aw-' + metric.tone}><metric.icon size={20} /></span>
          <strong>{data.kpis[metric.key]}</strong><span>{metric.label}</span><small>{metric.note}</small>
        </Link>)}
      </div>
      <div className="aw-dashboard-grid">
        <section className="aw-card">
          <h2>Your farmers at a glance</h2><p className="aw-muted">Season progress and what each farmer needs next</p>
          {!data.farmers.length && <p className="aw-empty">No assigned farmers yet.</p>}
          {data.farmers.slice(0, 5).map((farmer, index) => <div className="aw-glance" key={farmer.farmerId}>
            <Link className="aw-person" to={farmerPath(farmer.farmerId)}>
              <Avatar name={farmer.name} tone={['green', 'amber', 'blue', 'purple'][index % 4]} />
              <span><strong>{farmer.name}</strong><small>{farmer.farm} · {farmer.district}</small></span>
            </Link>
            <div className="aw-progress"><small>{farmer.seasonProgress}% season progress</small><progress max={100} value={farmer.seasonProgress} aria-label={farmer.name + ' season progress'} /></div>
            {farmer.needs[0] ? <NeedBadge need={farmer.needs[0]} /> : <Badge>No alerts</Badge>}
          </div>)}
        </section>
        <section className="aw-card">
          <div className="aw-heading-row"><h2>Needs your attention</h2><Badge tone="amber">{data.needsAttention.length} items</Badge></div>
          {!data.needsAttention.length && <p className="aw-empty">You’re all caught up.</p>}
          {data.needsAttention.map(({ farmerId, farmer, need }) => {
            const style = needs[need.type];
            const Icon = style?.icon ?? CircleAlert;
            return <Link className="aw-attention" key={farmerId + need.type} to={farmerPath(farmerId, style?.tab)}>
              <span className={'aw-symbol aw-' + (style?.tone ?? 'green')}><Icon size={17} /></span>
              <span><strong>{need.message}</strong><small>{farmer}</small></span><span className="aw-attention-view">View</span>
            </Link>;
          })}
        </section>
      </div>
      <FarmerTable farmers={data.farmers} />
    </>}
    {picker && <FarmerPicker onClose={() => setPicker(false)} onSelect={id => navigate(farmerPath(id))} />}
  </main>;
}

function Farmers() {
  const { data, error, retry } = useAgentQuery(agentApi.farmers);
  const [params, setParams] = useSearchParams();
  const search = params.get('search') ?? '';
  return <main className="aw-content">
    <div className="aw-heading-row aw-page-heading"><div><h1>My farmers</h1><p className="aw-muted">Find a farmer and open their workspace.</p></div>
      <SearchField label="Search your farmers…" value={search} onChange={value => setParams(value ? { search: value } : {}, { replace: true })} /></div>
    {data ? <FarmerTable farmers={data.filter(farmer => matchesFarmer(farmer, search))} /> : <QueryMessage error={error} retry={retry} />}
  </main>;
}

function FarmerWorkspace() {
  const { farmerId = '', tab = '' } = useParams();
  const navigate = useNavigate();
  const [picker, setPicker] = useState(false);
  const [dismissed, setDismissed] = useState(false);
  const load = useCallback((signal: AbortSignal) => agentApi.farmer(farmerId, signal), [farmerId]);
  const { data, error, status, retry } = useAgentQuery(load);
  const active = tabs.find(item => item.path === tab);

  if (!/^\d+$/.test(farmerId) || !active) return <Navigate to="/agent/farmers" replace />;
  if (status === 403) return <main className="aw-content">
    <section className="aw-card aw-empty"><LockKeyhole size={22} /><h1>Farmer access restricted</h1><Link className="aw-button" to="/agent/farmers">Back to my farmers</Link></section>
    {!dismissed && <Modal title="You can’t act for this farmer" onClose={() => setDismissed(true)}>
      <p className="aw-muted">This farmer is outside your assignments. Ask an administrator to assign you access.</p>
      <footer><button className="aw-button" onClick={() => setDismissed(true)}>Close</button><Link className="aw-button aw-primary" to="/agent/farmers">My farmers</Link></footer>
    </Modal>}
  </main>;
  if (!data) return <main className="aw-content"><QueryMessage error={error} retry={retry} /></main>;

  return <>
    <div className="aw-acting">
      <Avatar name={data.name} /><span><strong>You are acting for {data.name} · {data.farm}</strong><small>You’re viewing this farmer’s records as their assigned agent.</small></span>
      <button className="aw-button" onClick={() => setPicker(true)}>Switch farmer</button>
      <Link className="aw-text-link" to="/agent/farmers"><LogOut size={16} /> Exit</Link>
    </div>
    <main className="aw-content">
      <nav className="aw-tabs" aria-label="Farmer workspace">{tabs.map(item =>
        <NavLink key={item.title} to={farmerPath(data.farmerId, item.path)} end>{item.title}</NavLink>)}</nav>
      <header className="aw-page-heading aw-person"><span className="aw-symbol aw-green"><active.icon size={23} /></span>
        <div><h1>{data.name}’s {active.title.toLowerCase()}</h1><p className="aw-muted">{data.farm} · {data.district}</p></div>
      </header>
      {tab === '' ? <FarmerOverview farmer={data} /> : <section className="aw-card">
        <h2>{active.title} summary</h2>
        {tab === 'seed-stock' && <p>{data.lowStockCount} low stock items</p>}
        {tab === 'planting-batches' && <p>{data.activeBatches} active batches</p>}
        {tab === 'orders' && <p>{data.ordersToConfirm} orders to confirm</p>}
        <p className="aw-muted">{active.summary}</p>
      </section>}
    </main>
    {picker && <FarmerPicker current={data.farmerId} onClose={() => setPicker(false)} onSelect={id => {
      setPicker(false);
      navigate(farmerPath(id, tab));
    }} />}
  </>;
}

function FarmerOverview({ farmer }: { farmer: FarmerSummary }) {
  return <section className="aw-card">
    <h2>Farm summary</h2>
    <dl className="aw-details">
      <div><dt>Phone</dt><dd>{farmer.phone || 'No phone'}</dd></div>
      <div><dt>Active batches</dt><dd>{farmer.activeBatches}</dd></div>
      <div><dt>Low stock items</dt><dd>{farmer.lowStockCount}</dd></div>
      <div><dt>Orders to confirm</dt><dd>{farmer.ordersToConfirm}</dd></div>
      <div><dt>Season progress</dt><dd>{farmer.seasonProgress}%</dd></div>
    </dl>
    <div className="aw-actions">{farmer.needs.map(need => <Link key={need.type} to={farmerPath(farmer.farmerId, needs[need.type]?.tab)}><NeedBadge need={need} /></Link>)}</div>
  </section>;
}

export function AgentWorkspace() {
  return <Routes>
    <Route element={<WorkspaceShell />}>
      <Route index element={<Navigate to="dashboard" replace />} />
      <Route path="dashboard" element={<Dashboard />} />
      <Route path="farmers" element={<Farmers />} />
      <Route path="farmers/:farmerId/:tab?" element={<FarmerWorkspaceRoute />} />
      <Route path="register" element={<AgentRegistration />} />
      <Route path="visits" element={<AgentVisits />} />
      <Route path="marketplace" element={<main className="aw-content"><section className="aw-card"><h1>Marketplace</h1><p className="aw-muted">Marketplace listings require a marketplace API.</p></section></main>} />
      <Route path="*" element={<Navigate to="/agent/dashboard" replace />} />
    </Route>
  </Routes>;
}

function FarmerWorkspaceRoute() {
  const { farmerId } = useParams();
  return <FarmerWorkspace key={farmerId} />;
}

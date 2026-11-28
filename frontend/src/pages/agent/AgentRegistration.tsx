import { useState } from 'react';
import { LockKeyhole, Package, Truck, UserPlus, Users } from 'lucide-react';
import { Link } from 'react-router-dom';
import { RWANDA_LOCATIONS } from '../../data/rwandaLocations';

export function AgentRegistration() {
  const [province, setProvince] = useState('');
  const [district, setDistrict] = useState('');
  return <main className="aw-content">
    <header className="aw-page-heading aw-person">
      <span className="aw-symbol aw-green"><UserPlus size={24} /></span>
      <div><h1>Register a farmer</h1><p className="aw-muted">For farmers who need an agent to manage their AgriFlow records.</p></div>
    </header>
    <div className="aw-registration">
      <form className="aw-register-form" onSubmit={event => event.preventDefault()}>
        <section className="aw-card">
          <h2>Farmer details</h2>
          <div className="aw-fields">
            <label className="aw-field">Full name <input required autoComplete="off" placeholder="Farmer’s full name" /></label>
            <label className="aw-field">Phone (optional)<input type="tel" autoComplete="off" placeholder="If they have one" /></label>
            <label className="aw-field">Community / village<input required placeholder="Enter village" /></label>
            <label className="aw-field">Preferred language<select defaultValue="Kinyarwanda"><option>Kinyarwanda</option><option>English</option><option>French</option><option>Swahili</option></select></label>
          </div>
        </section>
        <section className="aw-card">
          <h2>Farm</h2>
          <div className="aw-fields">
            <label className="aw-field">Farm name<input required placeholder="Enter farm name" /></label>
            <label className="aw-field">Province<select required value={province} onChange={event => { setProvince(event.target.value); setDistrict(''); }}>
              <option value="">Select province</option>{Object.keys(RWANDA_LOCATIONS).map(name => <option key={name}>{name}</option>)}
            </select></label>
            <label className="aw-field">District<select required value={district} disabled={!province} onChange={event => setDistrict(event.target.value)}>
              <option value="">Select district</option>{Object.keys(RWANDA_LOCATIONS[province] ?? {}).map(name => <option key={name}>{name}</option>)}
            </select></label>
            <label className="aw-field">Farm size (acres)<input type="number" min="0" step="any" /></label>
          </div>
        </section>
        <section className="aw-card">
          <h2>Consent</h2><p className="aw-muted">Record permission to manage the farmer’s records on their behalf.</p>
          <label className="aw-consent"><input type="checkbox" required /> The farmer gave me permission in person to manage their AgriFlow records.</label>
        </section>
        <div className="aw-actions aw-form-actions"><Link className="aw-button" to="/agent/farmers">Cancel</Link>
          <button className="aw-button aw-primary" disabled aria-describedby="registration-status"><UserPlus size={16} /> Register farmer</button></div>
      </form>
      <aside className="aw-registration-aside">
        <section className="aw-card">
          <h2>What happens next</h2>
          {[
            { icon: Users, title: 'Assigned to you', text: 'The farmer appears in My farmers.' },
            { icon: Package, title: 'You record for them', text: 'Manage seed stock, plantings and harvests on their behalf.' },
            { icon: Truck, title: 'They reach buyers', text: 'List harvests for businesses on the marketplace.' },
          ].map(item => <div className="aw-attention" key={item.title}><span className="aw-symbol aw-green"><item.icon size={17} /></span><span><strong>{item.title}</strong><small>{item.text}</small></span></div>)}
        </section>
        <div className="aw-notice aw-blue" id="registration-status"><LockKeyhole size={18} /><div><strong>Registration unavailable</strong><p>Saving requires an agent registration endpoint.</p></div></div>
      </aside>
    </div>
  </main>;
}

import { useEffect, useState } from 'react';
import { Search, BookOpen, Scale, ShieldCheck, ArrowUpRight, SlidersHorizontal, Landmark, FileText, GitCompareArrows, ExternalLink, ChevronRight, X } from 'lucide-react';

const API = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
const TOPICS = ['Immigration', 'Social grants', 'Policing', 'Education', 'Healthcare', 'Jobs', 'Housing', 'Corruption', 'Economy', 'Energy', 'Transport'];

function sourceHref(url) { return url?.startsWith('http') ? url : `${API.replace(/\/$/, '').replace(/\/api$/, '')}${url || ''}`; }

async function api(path) {
  const response = await fetch(`${API}${path}`);
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.message || 'The request could not be completed.');
  return data;
}

export default function App() {
  const [parties, setParties] = useState([]);
  const [manifestos, setManifestos] = useState([]);
  const [party, setParty] = useState('');
  const [keyword, setKeyword] = useState('');
  const [topic, setTopic] = useState('');
  const [year, setYear] = useState('');
  const [results, setResults] = useState([]);
  const [compareResults, setCompareResults] = useState([]);
  const [compareTopic, setCompareTopic] = useState('Social grants');
  const [selectedParty, setSelectedParty] = useState(null);
  const [searched, setSearched] = useState(false);
  const [compareOpen, setCompareOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    Promise.all([api('/parties'), api('/manifestos')]).then(([p, m]) => { setParties(p); setManifestos(m); })
      .catch(() => setError('Could not connect to the API. Start PostgreSQL and the backend, then refresh.'));
  }, []);

  async function search(e) {
    e?.preventDefault(); setLoading(true); setError(''); setSearched(true); setSelectedParty(null);
    const params = new URLSearchParams();
    if (keyword.trim()) params.set('keyword', keyword.trim());
    if (party.trim()) params.set('party', party.trim());
    if (topic) params.set('topic', topic);
    if (year) params.set('year', year);
    if (![...params.keys()].length) { setError('Enter a party name or keyword, or choose a policy area.'); setLoading(false); return; }
    try { const data = await api(`/policies/search?${params}`); setResults(data.results || []); }
    catch (err) { setError(err.message); setResults([]); }
    finally { setLoading(false); }
  }

  async function openParty(p) {
    if (!p) return;
    setError(''); setLoading(true);
    try { setSelectedParty(await api(`/parties/${p.id}`)); setSearched(false); window.scrollTo({ top: 0, behavior: 'smooth' }); }
    catch (err) { setError(err.message); }
    finally { setLoading(false); }
  }

  async function compare(e) {
    e?.preventDefault(); setLoading(true); setError(''); setCompareOpen(true);
    try { const data = await api(`/policies/compare?topic=${encodeURIComponent(compareTopic)}${year ? `&year=${year}` : ''}`); setCompareResults(data.results || []); }
    catch (err) { setError(err.message); setCompareResults([]); }
    finally { setLoading(false); }
  }

  function resetHome() { setSelectedParty(null); setSearched(false); setCompareOpen(false); }

  return <div className="app-shell">
    <header className="topbar"><a className="brand" href="#top" onClick={resetHome}><span className="brand-mark"><Landmark size={21}/></span><span>MANIFESTO<span className="brand-light">EXPLORER</span></span></a><span className="top-tag">SOUTH AFRICA · VOTER INFORMATION</span></header>
    <main id="top">
      <section className="hero"><div className="hero-copy"><div className="eyebrow"><span/> INDEPENDENT POLICY RESEARCH</div><h1>Know what<br/>they <em>stand for.</em></h1><p>Explore political party manifestos and find their stated positions on the issues that matter to you.</p><div className="hero-points"><span><BookOpen size={16}/> Original manifesto sources</span><span><Scale size={16}/> Compare policy positions</span><span><ShieldCheck size={16}/> Transparent references</span></div></div><div className="hero-art"><div className="orbit orbit-one"/><div className="orbit orbit-two"/><div className="art-card"><div className="art-card-top"><span>THE PEOPLE'S VOICE</span><span>01 / SA</span></div><Landmark size={78} strokeWidth={1.1}/><div className="art-lines"><i/><i/><i/></div><div className="art-card-bottom">READ. QUESTION. DECIDE.</div></div><div className="floating-note">Your vote.<br/><b>Your choice.</b></div></div></section>

      <section className="search-section"><div className="section-kicker">START EXPLORING</div><h2>What do you want to know?</h2><p className="section-intro">Search by party, policy keyword, topic or election year. Results include only human-verified policy records.</p>
        <form className="search-panel" onSubmit={search}>
          <div className="field"><label htmlFor="party">POLITICAL PARTY</label><div className="input-wrap"><Landmark size={18}/><input id="party" list="party-list" placeholder="e.g. African National Congress" value={party} onChange={e=>setParty(e.target.value)}/><datalist id="party-list">{parties.map(p=><option key={p.id} value={p.name}/>)}</datalist></div></div>
          <div className="field"><label htmlFor="keyword">KEYWORD OR PHRASE</label><div className="input-wrap"><Search size={18}/><input id="keyword" placeholder="e.g. immigration, grants, policing" value={keyword} onChange={e=>setKeyword(e.target.value)}/></div></div>
          <div className="field"><label htmlFor="topic">POLICY AREA</label><div className="input-wrap"><SlidersHorizontal size={18}/><select id="topic" value={topic} onChange={e=>setTopic(e.target.value)}><option value="">All topics</option>{TOPICS.map(t=><option key={t}>{t}</option>)}</select></div></div>
          <div className="field year-field"><label htmlFor="year">ELECTION YEAR</label><div className="input-wrap"><select id="year" value={year} onChange={e=>setYear(e.target.value)}><option value="">Any year</option>{[2024,2019,2014,2009,2004,1999,1994].map(y=><option key={y}>{y}</option>)}</select></div></div>
          <button className="search-button" type="submit" disabled={loading}>{loading?'Searching…':<>Explore policies <ArrowUpRight size={18}/></>}</button>
        </form>
        <div className="quick-topics"><span>POPULAR TOPICS</span>{['Immigration','Social grants','Policing','Jobs','Education'].map(t=><button key={t} onClick={()=>{setTopic(t);setSearched(false);setSelectedParty(null);}}>{t}</button>)}</div>
      </section>
      {error && <div className="notice error">{error}</div>}

      {selectedParty && <section className="results-section detail-section"><button className="back-link" onClick={resetHome}>← Back to explorer</button><div className="results-heading"><div><div className="section-kicker">PARTY PROFILE</div><h2>{selectedParty.name} <small>{selectedParty.abbreviation}</small></h2><p className="section-intro">{selectedParty.description || 'Party profile and published manifesto records.'}</p>{selectedParty.websiteUrl && <a className="source-link" href={sourceHref(selectedParty.websiteUrl)} target="_blank" rel="noreferrer">Official party website <ExternalLink size={14}/></a>}</div></div>
        {selectedParty.manifestos.length ? selectedParty.manifestos.map(m=><article className="manifesto-card" key={m.id}><div className="result-meta"><span>{m.sourceName || 'Published manifesto'}</span><span>{m.electionYear}</span></div><h3>{m.title}</h3>{m.documentUrl && <a className="source-link" href={sourceHref(m.documentUrl)} target="_blank" rel="noreferrer">Read original manifesto <ArrowUpRight size={14}/></a>}<div className="policy-list">{m.policies.length ? m.policies.map(p=><div className="policy-row" key={p.id}><span className="topic-label">{p.topic}</span><h4>{p.title}</h4><p>{p.summary}</p>{p.pageReference && <small>{p.pageReference}</small>}</div>) : <p>No verified policy extracts have been published for this manifesto yet.</p>}</div></article>) : <div className="empty-state"><BookOpen size={28}/><h3>No manifesto records yet</h3><p>This party is in the directory, but no manifesto has been ingested and verified yet.</p></div>}
      </section>}

      {searched && !selectedParty && <section className="results-section"><div className="results-heading"><div><div className="section-kicker">SEARCH RESULTS</div><h2>Verified policy findings</h2></div><span className="result-count">{results.length} result{results.length===1?'':'s'}</span></div>{loading ? <p>Searching published policy records…</p> : results.length ? <div className="results-grid">{results.map(item=><article className="result-card" key={item.id}><div className="result-meta"><button className="text-link" onClick={()=>openParty(parties.find(p=>p.name===item.partyName))}>{item.partyAbbreviation || item.partyName}</button><span>{item.electionYear}</span></div><div className="topic-label">{item.topic}</div><h3>{item.title}</h3><p>{item.summary}</p><details><summary>Read source passage</summary><p className="full-text">{item.evidenceQuote || item.fullText}</p></details><div className="source-row"><FileText size={15}/><span>{item.sourceName || item.manifestoTitle}{item.pageReference ? ` · ${item.pageReference}` : ''}</span><span className="verification">Verified</span></div>{item.sourceUrl && <a className="source-link" href={sourceHref(item.sourceUrl)} target="_blank" rel="noreferrer">View original source <ArrowUpRight size={14}/></a>}</article>)}</div> : <div className="empty-state"><Search size={28}/><h3>No verified policies found</h3><p>Try another keyword or remove a filter. A party can appear in the directory before its manifesto policies have been reviewed and published.</p></div>}</section>}

      <section className="compare-section"><div className="compare-intro"><div className="section-kicker">SIDE-BY-SIDE RESEARCH</div><h2>Compare what parties say.</h2><p>View verified policy extracts from different parties under the same topic. This is a source comparison, not a recommendation or ranking.</p></div><form className="compare-form" onSubmit={compare}><div className="field"><label htmlFor="compare-topic">POLICY AREA TO COMPARE</label><div className="input-wrap"><GitCompareArrows size={18}/><select id="compare-topic" value={compareTopic} onChange={e=>setCompareTopic(e.target.value)}>{TOPICS.map(t=><option key={t}>{t}</option>)}</select></div></div><button className="search-button" type="submit" disabled={loading}>Compare parties <ArrowUpRight size={18}/></button></form>
        {compareOpen && <div className="compare-results"><div className="results-heading"><h3>{compareTopic}: party positions</h3><button className="text-link" onClick={()=>setCompareOpen(false)}><X size={15}/> Close</button></div>{compareResults.length ? <div className="results-grid">{compareResults.map(p=><article className="result-card" key={p.id}><div className="result-meta"><strong>{p.partyName}</strong><span>{p.electionYear}</span></div><h3>{p.title}</h3><p>{p.summary}</p><details><summary>View evidence</summary><p className="full-text">{p.evidenceQuote || p.fullText}</p></details><div className="source-row"><FileText size={15}/><span>{p.sourceName || p.manifestoTitle}{p.pageReference ? ` · ${p.pageReference}` : ''}</span><span className="verification">Verified</span></div>{p.sourceUrl && <a className="source-link" href={sourceHref(p.sourceUrl)} target="_blank" rel="noreferrer">Original source <ExternalLink size={13}/></a>}</article>)}</div> : <div className="empty-state"><Scale size={28}/><h3>No verified comparisons yet</h3><p>There are no verified records for this topic in the database yet. Upload and review source manifestos to populate comparisons.</p></div>}</div>}
      </section>

      <section className="directory-section"><div className="results-heading"><div><div className="section-kicker">PARTY DIRECTORY</div><h2>Explore political parties.</h2></div><span className="result-count">{parties.length} parties</span></div><div className="party-grid">{parties.map(p=><button className="party-card" key={p.id} onClick={()=>openParty(p)}><span className="party-monogram">{(p.abbreviation || p.name).slice(0,3).toUpperCase()}</span><span className="party-info"><strong>{p.name}</strong><small>{p.manifestoCount} manifesto(s) · {p.verifiedPolicyCount} verified policies</small></span><ChevronRight size={18}/></button>)}</div></section>

      <section className="how-section"><div><div className="section-kicker">BUILT FOR CLARITY</div><h2>Find the policy.<br/><em>Check the source.</em></h2></div><div className="how-cards"><div><span>01</span><h3>Search</h3><p>Enter a party name or a subject such as immigration, grants or policing.</p></div><div><span>02</span><h3>Understand</h3><p>Read a plain-language summary alongside the policy text and topic.</p></div><div><span>03</span><h3>Verify</h3><p>Follow the reference back to the original manifesto and check the context.</p></div></div></section>
    </main><footer><span>MANIFESTO EXPLORER</span><span>Independent voter information · Not affiliated with any political party</span></footer>
  </div>;
}

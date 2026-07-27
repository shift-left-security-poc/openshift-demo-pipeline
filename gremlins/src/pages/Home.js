import '../App.css';
import { Link } from 'react-router-dom';


const TEAM_NAME = 'The Gremlins';
const TEAM_MISSION = 'We break things before the bad guys do — chaos is our craft, resilience is our product.';
const TEAM_MOTTO = '/* TODO: insert witty motto here */';

const TEAM_MEMBERS = [
  { name: 'Gremlin-1 [PLACEHOLDER]', role: 'Lead Chaos Engineer' },
  { name: 'Gremlin-2 [PLACEHOLDER]', role: 'Security Breaker' },
  { name: 'Gremlin-3 [PLACEHOLDER]', role: 'Pipeline Gremlin' },
  { name: 'Gremlin-4 [PLACEHOLDER]', role: 'Shift-Left Advocate' },
];

const CONTACT = {
  email: 'gremlins@example.internal',
  slack: '#team-gremlins',
  wiki: 'https://wiki.example.internal/gremlins',
};

function Home() {
  return (
    <div className="App">
      <header className="App-hero">
        <div className="gremlin-icon" role="img" aria-label="gremlin">👾</div>
        <h1 className="team-name">{TEAM_NAME}</h1>
        <p className="team-motto">{TEAM_MOTTO}</p>
        <nav className="App-nav">
          <Link to="/blog" className="nav-link">📝 Read the blog / write a post</Link>
        </nav>
      </header>

      <main className="App-main">
        <section className="section mission-section">
          <h2>🎯 Our Mission</h2>
          <p className="mission-text">{TEAM_MISSION}</p>
        </section>

        <section className="section members-section">
          <h2>👥 Team Members</h2>
          <div className="members-grid">
            {TEAM_MEMBERS.map((m, i) => (
              <div className="member-card" key={i}>
                <div className="member-avatar">👾</div>
                <div className="member-name">{m.name}</div>
                <div className="member-role">{m.role}</div>
              </div>
            ))}
          </div>
        </section>

        <section className="section contact-section">
          <h2>📬 Contact Us</h2>
          <ul className="contact-list">
            <li>✉️ <a href={`mailto:${CONTACT.email}`}>{CONTACT.email}</a></li>
            <li>💬 Slack: <code>{CONTACT.slack}</code></li>
            <li>📖 Wiki: <a href={CONTACT.wiki} target="_blank" rel="noopener noreferrer">{CONTACT.wiki}</a></li>
          </ul>
        </section>
      </main>

      <footer className="App-footer">
        Proudly built on GitHub Actions · Hosted on OpenShift &amp; Azure
      </footer>
    </div>
  );
}

export default Home;

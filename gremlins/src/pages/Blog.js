import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import '../App.css';

const API_BASE_URL = process.env.REACT_APP_BLOG_API_URL || '';
const API_KEY = process.env.REACT_APP_BLOG_API_KEY || '';

const EMPTY_FORM = { title: '', content: '', author: '' };

function Blog() {
  const [posts, setPosts] = useState([]);
  const [form, setForm] = useState(EMPTY_FORM);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [successMessage, setSuccessMessage] = useState('');

  const loadPosts = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const response = await fetch(`${API_BASE_URL}/api/posts`, {
        headers: { 'X-API-Key': API_KEY },
      });
      if (!response.ok) {
        throw new Error(`Failed to load posts (HTTP ${response.status})`);
      }
      const data = await response.json();
      setPosts(data);
    } catch (err) {
      setError(err.message || 'Failed to load posts.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadPosts();
  }, [loadPosts]);

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    setSuccessMessage('');

    try {
      const response = await fetch(`${API_BASE_URL}/api/posts`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-API-Key': API_KEY,
        },
        body: JSON.stringify(form),
      });

      if (!response.ok) {
        const body = await response.json().catch(() => ({}));
        throw new Error(body.error || `Failed to publish post (HTTP ${response.status})`);
      }

      setForm(EMPTY_FORM);
      setSuccessMessage('Your post was published!');
      await loadPosts();
    } catch (err) {
      setError(err.message || 'Failed to publish post.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="App">
      <header className="App-hero">
        <div className="gremlin-icon" role="img" aria-label="gremlin">👾</div>
        <h1 className="team-name">Gremlins Blog</h1>
        <p className="team-motto">Anyone can post — go on, break something.</p>
        <nav className="App-nav">
          <Link to="/" className="nav-link">⬅ Back to team page</Link>
        </nav>
      </header>

      <main className="App-main">
        <section className="section">
          <h2>✍️ Write a post</h2>
          <form className="blog-form" onSubmit={handleSubmit}>
            <label htmlFor="author">Your name</label>
            <input
              id="author"
              name="author"
              type="text"
              value={form.author}
              onChange={handleChange}
              required
            />

            <label htmlFor="title">Title</label>
            <input
              id="title"
              name="title"
              type="text"
              value={form.title}
              onChange={handleChange}
              required
            />

            <label htmlFor="content">Content</label>
            <textarea
              id="content"
              name="content"
              rows={5}
              value={form.content}
              onChange={handleChange}
              required
            />

            <button type="submit" disabled={submitting}>
              {submitting ? 'Publishing…' : 'Publish post'}
            </button>
          </form>

          {error && <p className="blog-error" role="alert">{error}</p>}
          {successMessage && <p className="blog-success">{successMessage}</p>}
        </section>

        <section className="section">
          <h2>📚 Posts</h2>
          {loading ? (
            <p>Loading posts…</p>
          ) : posts.length === 0 ? (
            <p>No posts yet. Be the first!</p>
          ) : (
            <ul className="blog-post-list">
              {posts
                .slice()
                .sort((a, b) => new Date(b.createdAtUtc) - new Date(a.createdAtUtc))
                .map((post) => (
                  <li key={post.id} className="blog-post-card">
                    <h3>{post.title}</h3>
                    <p className="blog-post-meta">
                      by {post.author} · {new Date(post.createdAtUtc).toLocaleString()}
                    </p>
                    <p>{post.content}</p>
                  </li>
                ))}
            </ul>
          )}
        </section>
      </main>

      <footer className="App-footer">
        Proudly built on GitHub Actions · Hosted on OpenShift &amp; Azure
      </footer>
    </div>
  );
}

export default Blog;

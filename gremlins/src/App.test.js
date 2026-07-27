import { render, screen } from '@testing-library/react';
import App from './App';

test('renders the team name on the home page', () => {
  render(<App />);
  const heading = screen.getByText(/The Gremlins/i);
  expect(heading).toBeInTheDocument();
});

test('renders a link to the blog page', () => {
  render(<App />);
  const blogLink = screen.getByText(/write a post/i);
  expect(blogLink).toBeInTheDocument();
});

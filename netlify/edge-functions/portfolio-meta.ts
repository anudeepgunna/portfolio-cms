/* ─────────────────────────────────────────────────────────────────────────────
 * Per-portfolio social preview cards.
 *
 * The site is Blazor WebAssembly, so the served HTML is an empty shell — the
 * page only exists after the runtime downloads and executes. Social crawlers
 * (Slack, WhatsApp, LinkedIn, iMessage, Twitter/X, Facebook) do not run
 * JavaScript, so every shared portfolio previewed as the bare "Portfolio"
 * title with no description and no image.
 *
 * This edge function runs ahead of the static response for /p/:username,
 * fetches that portfolio from the API, and rewrites the <head> with real
 * Open Graph and Twitter tags carrying that person's name, role and intro.
 * Browsers get the identical shell they always did — only the metadata
 * changes — so the Blazor app boots exactly as before.
 * ───────────────────────────────────────────────────────────────────────────*/

import type { Config, Context } from 'https://edge.netlify.com';

const API_BASE =
  Deno.env.get('API_BASE_URL') ?? 'https://portfolio-cms-api-4027.onrender.com';

interface Section {
  type: string;
  title: string;
  subTitle: string | null;
  content: string;
}

interface Portfolio {
  username: string;
  theme: {
    primaryColor: string;
    secondaryColor: string;
    accentColor: string;
    backgroundColor: string;
    textColor: string;
  } | null;
  sections: Section[];
  projects: { title: string }[];
}

const escapeHtml = (s: string) =>
  s.replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');

const clamp = (s: string, n: number) =>
  s.length <= n ? s : `${s.slice(0, n - 1).trimEnd()}…`;

/**
 * Shared preview image. Per-user images would need PNG rasterisation at the
 * edge — crawlers reject data: URIs and SVG for og:image — so the picture is
 * branded and constant while the title and description are per portfolio,
 * which is what actually differentiates a shared link.
 */
const OG_IMAGE = '/og-default.png';

export default async function handler(request: Request, context: Context) {
  const url = new URL(request.url);
  const username = decodeURIComponent(url.pathname.replace(/^\/p\//, '')).trim();

  // Always serve the real static response; we only ever rewrite its <head>.
  const response = await context.next();

  if (!username || username.includes('/')) return response;

  const type = response.headers.get('content-type') ?? '';
  if (!type.includes('text/html')) return response;

  let portfolio: Portfolio | null = null;
  try {
    // Render's free tier cold-starts. Cap the wait so a sleeping API delays
    // the page by a couple of seconds at most instead of hanging the request.
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 4000);

    const apiRes = await fetch(
      `${API_BASE}/api/portfolios/${encodeURIComponent(username)}`,
      { signal: controller.signal, headers: { accept: 'application/json' } },
    );
    clearTimeout(timer);

    if (apiRes.ok) portfolio = await apiRes.json();
  } catch {
    // Unreachable or slow API — fall through and serve the untouched shell.
  }

  if (!portfolio) return response;

  const hero = portfolio.sections.find((s) => s.type === 'Hero');
  const about = portfolio.sections.find((s) => s.type === 'About');

  const title = (hero?.title || `${portfolio.username}'s portfolio`).trim();
  const subtitle = (hero?.subTitle || '').trim();

  const projectCount = portfolio.projects.length;
  const description = clamp(
    (hero?.content || about?.content || '').trim() ||
      `${title}${subtitle ? ` — ${subtitle}` : ''}. ${projectCount} project${projectCount === 1 ? '' : 's'}.`,
    200,
  );

  const pageTitle = subtitle ? `${title} — ${subtitle}` : title;
  const image = `${url.origin}${OG_IMAGE}`;

  const meta = `
<title>${escapeHtml(pageTitle)}</title>
<meta name="description" content="${escapeHtml(description)}"/>
<link rel="canonical" href="${escapeHtml(url.origin)}/p/${encodeURIComponent(portfolio.username)}"/>
<meta property="og:type" content="profile"/>
<meta property="og:site_name" content="FolioBay"/>
<meta property="og:title" content="${escapeHtml(pageTitle)}"/>
<meta property="og:description" content="${escapeHtml(description)}"/>
<meta property="og:url" content="${escapeHtml(url.origin)}/p/${encodeURIComponent(portfolio.username)}"/>
<meta property="og:image" content="${escapeHtml(image)}"/>
<meta property="og:image:width" content="1200"/>
<meta property="og:image:height" content="630"/>
<meta name="twitter:card" content="summary_large_image"/>
<meta name="twitter:title" content="${escapeHtml(pageTitle)}"/>
<meta name="twitter:description" content="${escapeHtml(description)}"/>
<meta name="twitter:image" content="${escapeHtml(image)}"/>
`.trim();

  const html = await response.text();

  // Drop the shell's placeholder <title> so crawlers do not see two.
  const rewritten = html
    .replace(/<title>[\s\S]*?<\/title>/i, '')
    .replace(/<\/head>/i, `${meta}\n</head>`);

  return new Response(rewritten, {
    status: response.status,
    headers: {
      ...Object.fromEntries(response.headers),
      'content-type': 'text/html; charset=utf-8',
      // Short shared cache: previews stay fresh after an edit without
      // hammering the API on every crawl.
      'cache-control': 'public, max-age=0, s-maxage=300',
    },
  });
}

export const config: Config = { path: '/p/*' };

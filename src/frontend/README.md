# CasaSim Frontend

Vite + React 19 + TypeScript frontend for the CasaSim property listing platform.

## Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Start Vite dev server (port 5173, proxies `/api` to `localhost:5000`) |
| `npm run build` | TypeScript check + production build to `dist/` |
| `npm run preview` | Preview the production build locally |
| `npm run lint` | ESLint check |

## Stack

- **React 19** with TypeScript 6 (strict mode)
- **React Router 7** (BrowserRouter with Layout)
- **Tailwind CSS 4** via Vite plugin
- **Vite 8** with React plugin

## Structure

```
src/
├── main.tsx                  # Entry point
├── App.tsx                   # Router setup
├── index.css                 # Global styles + Tailwind
├── components/
│   └── Layout.tsx            # Shared layout wrapper
└── pages/
    ├── HomePage.tsx          # / — landing page
    ├── SearchPage.tsx        # /search — property search
    └── PropertyDetailPage.tsx # /property/:id — property details
```

## Development

The dev server proxies `/api/*` requests to the backend at `http://localhost:5000`.

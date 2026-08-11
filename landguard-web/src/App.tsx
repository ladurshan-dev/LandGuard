import { CssBaseline, ThemeProvider } from '@mui/material';
import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { AppRoutes } from './routes/AppRoutes';
import { landguardTheme } from './theme/landguardTheme';

/**
 * Composition root: BrowserRouter (routing) wraps ThemeProvider (the
 * LandGuard design system, Stage 2) wraps AuthProvider (auth state) wraps
 * AppRoutes (the route table) - the same nesting order the Authentication
 * Foundation brief specifies, with ThemeProvider added around it rather
 * than between AuthProvider and AppRoutes, since theming has no
 * dependency on auth state and every screen (including /login, which
 * renders before any auth state exists) needs it. CssBaseline resets the
 * browser's default styles the way MUI's own templates do, and now also
 * applies the theme's palette (background/text colors) globally - this is
 * the single place the whole app is re-skinned from, so pages Stage 2
 * does not otherwise touch (the three dashboards, every property page)
 * pick up the new palette/typography automatically without their own JSX
 * changing.
 *
 * This replaces the default Vite/React starter markup (the counter demo,
 * react.svg/vite.svg/hero.png) entirely - none of it was part of
 * LandGuard, and its `import './App.css'` was already a dead import
 * (App.css does not exist in this project), so this incidentally fixes a
 * build that would have failed on that missing file the first time
 * `vite build` actually tried to resolve it.
 */
function App() {
  return (
    <BrowserRouter>
      <ThemeProvider theme={landguardTheme}>
        <CssBaseline />
        <AuthProvider>
          <AppRoutes />
        </AuthProvider>
      </ThemeProvider>
    </BrowserRouter>
  );
}

export default App;

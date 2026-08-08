import { CssBaseline } from '@mui/material';
import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { AppRoutes } from './routes/AppRoutes';

/**
 * Composition root: BrowserRouter (routing) wraps AuthProvider (auth
 * state) wraps AppRoutes (the route table) - exactly the nesting order
 * the Authentication Foundation brief specifies, and the only
 * BrowserRouter anywhere in the app. CssBaseline resets the browser's
 * default styles the way MUI's own templates do, so the login and
 * dashboard screens aren't fighting the leftover Vite-template CSS in
 * index.css.
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
      <CssBaseline />
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;

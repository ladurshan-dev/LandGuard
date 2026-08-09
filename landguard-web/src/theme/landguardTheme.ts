import { createTheme } from '@mui/material/styles';

/**
 * Raw LandGuard brand colors (Stage 2 - visual redesign, see the attached
 * design reference approved for LoginPage/DashboardLayout). Exported for
 * the handful of places (the dashboard sidebar's dark surface, its
 * gold-tinted active/hover states, the login page's brand panel) that
 * need a literal brand color MUI's semantic palette slots
 * (primary/secondary/background/text) don't have a dedicated slot for.
 * Everywhere else should read colors from `theme.palette` instead of this
 * object, so there is exactly one place a brand color could drift from
 * the approved reference.
 */
export const landguardColors = {
  green: '#1B6B3A',
  greenMid: '#2E8B57',
  greenLight: '#3DAA6D',
  greenPale: '#EBF7F0',
  gold: '#C8922A',
  goldLight: '#E8B84B',
  goldPale: '#FDF3DC',
  cream: '#FAF7F2',
  charcoal: '#1C2B1E',
  charcoal2: '#2D3E30',
  textMid: '#3D5240',
  muted: '#7A9180',
  grayLine: '#E0E8E2',
} as const;

/**
 * No external font dependency for now (per explicit instruction) - a
 * built-in serif stack for display headings (the reference's DM Serif
 * Display role) and the platform's own default UI sans stack for body
 * text (the reference's DM Sans role). Both are already installed on
 * every target OS, so there is no network request and no new dependency.
 */
const headingFontFamily = 'Georgia, "Iowan Old Style", "Palatino Linotype", "Book Antiqua", Palatino, serif';

const bodyFontFamily =
  '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif';

/**
 * The single LandGuard design system - palette, typography, shape and a
 * handful of component defaults, applied once via <ThemeProvider> in
 * App.tsx. Every existing MUI component across the app (Chip color="success",
 * Button variant="contained", etc.) already keys off theme.palette, so
 * this one file is what re-skins the whole application, including pages
 * whose own JSX Stage 2 does not touch.
 */
export const landguardTheme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: landguardColors.green,
      light: landguardColors.greenLight,
      contrastText: '#FFFFFF',
    },
    secondary: {
      main: landguardColors.gold,
      light: landguardColors.goldLight,
      contrastText: landguardColors.charcoal,
    },
    background: {
      default: landguardColors.cream,
      paper: '#FFFFFF',
    },
    text: {
      primary: landguardColors.charcoal,
      secondary: landguardColors.muted,
    },
    divider: landguardColors.grayLine,
    success: { main: '#16A34A' },
    warning: { main: '#B45309' },
    error: { main: '#DC2626' },
  },
  shape: {
    borderRadius: 12,
  },
  typography: {
    fontFamily: bodyFontFamily,
    h1: { fontFamily: headingFontFamily, fontWeight: 400 },
    h2: { fontFamily: headingFontFamily, fontWeight: 400 },
    h3: { fontFamily: headingFontFamily, fontWeight: 400 },
    h4: { fontFamily: headingFontFamily, fontWeight: 400 },
    h5: { fontFamily: headingFontFamily, fontWeight: 400 },
    button: { textTransform: 'none', fontWeight: 600 },
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 10 },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: 16,
          boxShadow: '0 2px 16px rgba(27,107,58,0.08)',
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: { borderRadius: 10 },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: { fontWeight: 600 },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: { boxShadow: 'none' },
      },
    },
  },
});
